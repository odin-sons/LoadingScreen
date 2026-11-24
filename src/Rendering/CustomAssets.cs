namespace LoadingScreen;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using global::LoadingScreen.Model;

using HarmonyLib;

using UnityEngine;
using UnityEngine.Rendering;

public static class CustomAssets {
  public static List<string> LoadingTips { get; } = [];
  public static List<string> LoadingImageFiles { get; } = [];
  static int _lastTipIndex = -1;
  static string _tipFolder;
  static string _loadedLanguageCode;

  public static string CurrentLanguageCode =>
      LanguageText.Code(PluginConfig.TextLanguage?.Value, Localization.instance?.GetSelectedLanguage());

  public const long MaxCachedBytes = 256L * 1024 * 1024;

  static readonly Dictionary<string, Sprite> _loadingImageCache = [];
  const int SampleSize = 64;
  static readonly Dictionary<Sprite, ColorValue[]> _imageSamples = [];
  static readonly LinkedList<string> _loadingImageUsage = new();
  static readonly Dictionary<Object, HashSet<Sprite>> _pinnedSprites = [];

  // Reflection: ImageConversionModule targets netstandard 2.1, which net48 cannot reference directly.
  static readonly MethodInfo _loadImageMethod =
      AccessTools.Method(
          typeof(ImageConversion),
          nameof(ImageConversion.LoadImage),
          [typeof(Texture2D), typeof(byte[]), typeof(bool)]);

  public static void Initialize(string pluginDir) {
    _tipFolder = pluginDir;
    _loadedLanguageCode = null;
    RefreshTips();

    LoadingImageFiles.Clear();
    LoadingImageFiles.AddRange(ReadLoadingImageFiles(pluginDir, ".png"));
    LoadingImageFiles.AddRange(ReadLoadingImageFiles(pluginDir, ".jpg"));
  }

  static void RefreshTips() {
    string code = CurrentLanguageCode;
    if (_tipFolder == null || _loadedLanguageCode == code) return;

    LoadingTips.Clear();
    string fileName = LanguageText.TipFileName(code, name => File.Exists(Path.Combine(_tipFolder, name)));
    LoadingTips.AddRange(ReadLoadingTips(Path.Combine(_tipFolder, fileName)));
    _lastTipIndex = -1;
    _loadedLanguageCode = code;
  }

  public static IEnumerable<string> ReadLoadingTips(string path) {
    if (File.Exists(path)) {
      string[] loadingTips = File.ReadAllLines(path);
      Log.Config.Info(LogContext.Message("Loaded custom loading tips.",
          ("file", LogContext.File(path, _tipFolder)), ("count", loadingTips.Length)));

      return loadingTips;
    } else {
      Log.Config.Info(LogContext.Message("Creating an empty custom tips file.",
          ("file", LogContext.File(path, _tipFolder))));
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.Create(path).Dispose();

      return [];
    }
  }

  public static IEnumerable<string> ReadLoadingImageFiles(string path, string extension) {
    Directory.CreateDirectory(path);

    string[] loadingImageFiles = Directory.GetFiles(path, $"*{extension}", SearchOption.TopDirectoryOnly);

    Log.Config.Info(LogContext.Message("Found custom loading images.",
        ("folder", LogContext.File(path, _tipFolder)), ("extension", extension), ("count", loadingImageFiles.Length)));

    return loadingImageFiles;
  }

  public static long PixelCount(Sprite sprite) =>
      sprite && sprite.texture ? (long) sprite.texture.width * sprite.texture.height : 0;

  public static Sprite ReadLoadingImage(string imageFile, long maxPixels = long.MaxValue) {
    if (string.IsNullOrEmpty(imageFile)) {
      return null;
    }

    if (_loadingImageCache.TryGetValue(imageFile, out Sprite sprite) && sprite) {
      if (PixelCount(sprite) > maxPixels) return null;
      TouchCachedImage(imageFile);
      return sprite;
    }

    if (!File.Exists(imageFile)) {
      Log.Config.WarningOnce($"missing-image:{imageFile}", LogContext.Message(
          "Could not load loading screen image because the file is missing.",
          ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir))));
      return null;
    }

    byte[] data = File.ReadAllBytes(imageFile);

    if (!ImageHeader.TryRead(data, out int headerWidth, out int headerHeight)
        || !ImageHeader.IsAllowed(headerWidth, headerHeight)) {
      Log.Config.WarningOnce($"invalid-image:{imageFile}", LogContext.Message(
          "Rejected loading screen image header.", ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir)),
          ("width", headerWidth), ("height", headerHeight), ("formats", "PNG,JPEG"),
          ("maxSidePixels", ImageHeader.MaxSide), ("maxPixels", ImageHeader.MaxPixels)));
      return null;
    }

    if ((long) headerWidth * headerHeight > maxPixels) {
      Log.Config.WarningOnce($"screen-budget:{imageFile}", LogContext.Message(
          "Skipped loading screen image because the screen already uses its image memory budget.",
          ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir)),
          ("width", headerWidth), ("height", headerHeight), ("remainingPixels", maxPixels)));
      return null;
    }

    Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false) {
      name = $"loadingscreen.texture-{Path.GetFileName(imageFile)}"
    };

    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    if (_loadImageMethod.Invoke(obj: null, [texture, data, true]) is not true) {
      Object.Destroy(texture);
      Log.Config.WarningOnce($"undecodable-image:{imageFile}", LogContext.Message(
          "Could not decode loading screen image.", ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir))));
      return null;
    }
    ColorValue[] samples = SampleColors(texture);
    Log.Config.Debug(LogContext.Message("Decoded loading screen image.",
        ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir)),
        ("width", texture.width), ("height", texture.height), ("elapsedMs", stopwatch.ElapsedMilliseconds)));

    sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), Vector2.zero, 1);
    sprite.name = $"loadingscreen.sprite-{Path.GetFileName(imageFile)}";
    if (samples != null) _imageSamples[sprite] = samples;

    _loadingImageCache[imageFile] = sprite;
    TouchCachedImage(imageFile);
    EvictCachedImages(sprite);

    return sprite;
  }

  static ColorValue[] SampleColors(Texture2D texture) {
    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;

    RenderTexture previous = RenderTexture.active;
    RenderTexture current = null;
    Texture2D readback = null;
    try {
      Texture source = texture;
      int width = texture.width, height = texture.height;
      do {
        width = width > SampleSize * 2 ? width / 2 : SampleSize;
        height = height > SampleSize * 2 ? height / 2 : SampleSize;
        RenderTexture next = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture last = current;
        current = next;
        if (last) RenderTexture.ReleaseTemporary(last);
        next.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, next);
        source = next;
      } while (width != SampleSize || height != SampleSize);

      RenderTexture.active = current;
      readback = new(SampleSize, SampleSize, TextureFormat.RGBA32, mipChain: false);
      readback.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0, recalculateMipMaps: false);
      return readback.GetPixels32().Select(pixel => new ColorValue(pixel.r, pixel.g, pixel.b, pixel.a)).ToArray();
    } catch (System.Exception exception) {
      Log.Config.Debug(LogContext.Message("Could not sample loading screen image colours.", ("error", exception.Message)));
      return null;
    } finally {
      RenderTexture.active = previous;
      if (current) RenderTexture.ReleaseTemporary(current);
      if (readback) Object.Destroy(readback);
    }
  }

  static void TouchCachedImage(string imageFile) {
    _loadingImageUsage.Remove(imageFile);
    _loadingImageUsage.AddLast(imageFile);
  }

  public static void Pin(Object owner, IEnumerable<Sprite> sprites) {
    List<Sprite> released = [];

    foreach (Object destroyed in _pinnedSprites.Keys.Where(key => !key).ToList()) {
      released.AddRange(_pinnedSprites[destroyed]);
      _pinnedSprites.Remove(destroyed);
    }

    if (_pinnedSprites.TryGetValue(owner, out HashSet<Sprite> previous)) {
      released.AddRange(previous);
    }

    HashSet<Sprite> pinned = new(sprites.Where(sprite => sprite));

    if (owner && pinned.Count > 0) {
      _pinnedSprites[owner] = pinned;
    } else {
      _pinnedSprites.Remove(owner);
    }

    foreach (Sprite sprite in released) {
      DestroyIfOrphaned(sprite);
    }
  }

  static bool IsPinned(Sprite sprite) {
    return _pinnedSprites.Values.Any(set => set.Contains(sprite));
  }

  static void DestroyIfOrphaned(Sprite sprite) {
    if (sprite && !IsPinned(sprite) && !_loadingImageCache.ContainsValue(sprite)) {
      _imageSamples.Remove(sprite);
      Object.Destroy(sprite.texture);
      Object.Destroy(sprite);
    }
  }

  public static bool TryGetImageSamples(Sprite sprite, out ColorValue[] samples, out int size) {
    size = SampleSize;
    return _imageSamples.TryGetValue(sprite, out samples);
  }

  static void EvictCachedImages(Sprite keep) {
    LinkedListNode<string> node = _loadingImageUsage.First;

    while (CachedBytes() > MaxCachedBytes && node != null) {
      LinkedListNode<string> next = node.Next;

      if (!_loadingImageCache.TryGetValue(node.Value, out Sprite sprite) || (sprite != keep && !IsPinned(sprite))) {
        _loadingImageUsage.Remove(node);
        _loadingImageCache.Remove(node.Value);
        DestroyIfOrphaned(sprite);
      }

      node = next;
    }
  }

  static long CachedBytes() => _loadingImageCache.Values.Sum(PixelCount) * 4;

  public static void ClearImageCache() {
    List<Sprite> sprites = [.. _loadingImageCache.Values];
    _loadingImageCache.Clear();
    _loadingImageUsage.Clear();

    foreach (Sprite sprite in sprites) {
      DestroyIfOrphaned(sprite);
    }
  }

  public static bool GetRandomLoadingTip(out string tipText) {
    RefreshTips();
    if (LoadingTips.Count > 0) {
      int choiceCount = LoadingTips.Count - (TipSelection.HasPrevious(LoadingTips.Count, _lastTipIndex) ? 1 : 0);
      _lastTipIndex = TipSelection.NextIndex(LoadingTips.Count, _lastTipIndex, Random.Range(0, choiceCount));
      tipText = LoadingTips[_lastTipIndex];
      return true;
    }

    tipText = default;
    return false;
  }

  static int _loadingImageIndex = -1;

  public static bool GetRandomLoadingImage(out Sprite loadingImageSprite) {
    loadingImageSprite = ReadLoadingImage(NextLoadingImagePath());
    return loadingImageSprite;
  }

  public static string NextLoadingImagePath() {
    if (LoadingImageFiles.Count > 0) {
      if (_loadingImageIndex < 0 || _loadingImageIndex >= LoadingImageFiles.Count) {
        Utils.Shuffle(LoadingImageFiles);
        _loadingImageIndex = 0;
      }

      return LoadingImageFiles[_loadingImageIndex++];
    }

    return null;
  }
}
