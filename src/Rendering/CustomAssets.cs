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
      LoadingScreen.LogInfo($"Found {loadingTips.Length} custom tips in file: {path}");

      return loadingTips;
    } else {
      LoadingScreen.LogInfo($"Creating new empty custom tips file: {path}");
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.Create(path).Dispose();

      return [];
    }
  }

  public static IEnumerable<string> ReadLoadingImageFiles(string path, string extension) {
    Directory.CreateDirectory(path);

    string[] loadingImageFiles = Directory.GetFiles(path, $"*{extension}", SearchOption.TopDirectoryOnly);

    LoadingScreen.LogInfo(
        $"Found {loadingImageFiles.Length} custom loading images ({extension}) in directory: {path}");

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
      LoadingScreen.LogError($"Could not find custom loading image: {imageFile}");
      return null;
    }

    byte[] data = File.ReadAllBytes(imageFile);

    if (!ImageHeader.TryRead(data, out int headerWidth, out int headerHeight)
        || !ImageHeader.IsAllowed(headerWidth, headerHeight)) {
      LoadingScreen.LogError(
          $"Skipping {Path.GetFileName(imageFile)}: not a PNG or JPEG file, or larger than "
              + $"{ImageHeader.MaxSide} px per side or {ImageHeader.MaxPixels / 1000000} megapixels.");
      return null;
    }

    Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false) {
      name = $"loadingscreen.texture-{Path.GetFileName(imageFile)}"
    };

    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    _loadImageMethod.Invoke(obj: null, [texture, data, true]);
    LoadingScreen.LogInfo(
        $"Loaded image {Path.GetFileName(imageFile)} ({texture.width}x{texture.height}) "
            + $"in {stopwatch.ElapsedMilliseconds} ms.");

    sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), Vector2.zero, 1);
    sprite.name = $"loadingscreen.sprite-{Path.GetFileName(imageFile)}";

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
      Object.Destroy(sprite.texture);
      Object.Destroy(sprite);
    }
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
    if (LoadingImageFiles.Count > 0) {
      if (_loadingImageIndex < 0 || _loadingImageIndex >= LoadingImageFiles.Count) {
        Utils.Shuffle(LoadingImageFiles);
        _loadingImageIndex = 0;
      }

      loadingImageSprite = ReadLoadingImage(LoadingImageFiles[_loadingImageIndex]);
      _loadingImageIndex++;

      return loadingImageSprite;
    }

    loadingImageSprite = default;
    return false;
  }
}
