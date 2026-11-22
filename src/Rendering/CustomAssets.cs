namespace LoadingScreen;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using global::LoadingScreen.Model;

using HarmonyLib;

using UnityEngine;

public static class CustomAssets {
  public static List<string> LoadingTips { get; } = [];
  public static List<string> LoadingImageFiles { get; } = [];
  static int _lastTipIndex = -1;
  static string _tipFolder;
  static string _loadedLanguageCode;

  public static string CurrentLanguageCode =>
      LanguageText.Code(PluginConfig.TextLanguage?.Value, Localization.instance?.GetSelectedLanguage());

  const int MaxCachedImages = 16;

  static readonly Dictionary<string, Sprite> _loadingImageCache = [];
  const int SampleSize = 64;
  static readonly Dictionary<Sprite, ColorValue[]> _imageSamples = [];
  static readonly LinkedList<string> _loadingImageUsage = new();
  static readonly Dictionary<Object, HashSet<Sprite>> _pinnedSprites = [];

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

  public static Sprite ReadLoadingImage(string imageFile) {
    if (string.IsNullOrEmpty(imageFile)) {
      return null;
    }

    if (_loadingImageCache.TryGetValue(imageFile, out Sprite sprite) && sprite) {
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

    Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false) {
      name = $"loadingscreen.texture-{Path.GetFileName(imageFile)}"
    };

    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    _loadImageMethod.Invoke(obj: null, [texture, data, false]);
    Color32[] pixels = texture.GetPixels32();
    ColorValue[] samples = new ColorValue[SampleSize * SampleSize];
    long[] red = new long[samples.Length], green = new long[samples.Length],
        blue = new long[samples.Length], alpha = new long[samples.Length];
    int[] counts = new int[samples.Length];
    for (int y = 0; y < texture.height; y++) {
      int row = System.Math.Min(SampleSize - 1, y * SampleSize / texture.height) * SampleSize;
      for (int x = 0; x < texture.width; x++) {
        int index = row + System.Math.Min(SampleSize - 1, x * SampleSize / texture.width);
        Color32 pixel = pixels[y * texture.width + x];
        red[index] += pixel.r;
        green[index] += pixel.g;
        blue[index] += pixel.b;
        alpha[index] += pixel.a;
        counts[index]++;
      }
    }
    for (int i = 0; i < samples.Length; i++) {
      if (counts[i] == 0) {
        int sourceX = System.Math.Min(texture.width - 1, (i % SampleSize) * texture.width / SampleSize);
        int sourceY = System.Math.Min(texture.height - 1, (i / SampleSize) * texture.height / SampleSize);
        Color32 pixel = pixels[sourceY * texture.width + sourceX];
        samples[i] = new(pixel.r, pixel.g, pixel.b, pixel.a);
        continue;
      }
      samples[i] = new((byte) (red[i] / counts[i]), (byte) (green[i] / counts[i]),
          (byte) (blue[i] / counts[i]), (byte) (alpha[i] / counts[i]));
    }
    texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
    Log.Config.Debug(LogContext.Message("Decoded loading screen image.",
        ("file", LogContext.File(imageFile, ScreenConfigLoader.PluginDir)),
        ("width", texture.width), ("height", texture.height), ("elapsedMs", stopwatch.ElapsedMilliseconds)));

    sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), Vector2.zero, 1);
    sprite.name = $"loadingscreen.sprite-{Path.GetFileName(imageFile)}";
    _imageSamples[sprite] = samples;

    _loadingImageCache[imageFile] = sprite;
    TouchCachedImage(imageFile);
    EvictCachedImages();

    return sprite;
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

  static void EvictCachedImages() {
    LinkedListNode<string> node = _loadingImageUsage.First;

    while (_loadingImageUsage.Count > MaxCachedImages && node != null) {
      LinkedListNode<string> next = node.Next;

      if (!_loadingImageCache.TryGetValue(node.Value, out Sprite sprite) || !IsPinned(sprite)) {
        _loadingImageUsage.Remove(node);
        _loadingImageCache.Remove(node.Value);
        DestroyIfOrphaned(sprite);
      }

      node = next;
    }
  }

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
