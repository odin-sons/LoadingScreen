namespace LoadingScreen;

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using global::LoadingScreen.Model;

public static class ScreenConfigLoader {
  public const string ConfigFileName = "loadingscreen.yaml";

  public static ScreenConfig Current { get; private set; } = new();
  public static EntrySelector Selector { get; private set; } = new(new ScreenConfig());
  public static string PluginDir { get; private set; }
  public static ScreenConfig LocalConfig { get; private set; } = new();
  public static bool IsUsingServerConfig { get; private set; }

  static Func<string, string> _resolver = LocalImagePath;
  static int _lastWarningCount;

  public static string ResolveImagePath(string relativePath) {
    return relativePath == null ? null : _resolver(relativePath);
  }

  public static bool IsImageAvailable(string relativePath) {
    string path = ResolveImagePath(relativePath);
    return path != null && File.Exists(path);
  }

  public static string LocalImagePath(string relativePath) {
    return PluginDir == null ? null : Path.Combine(PluginDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
  }

  public static void UseServerConfig(ScreenConfig config, Func<string, string> resolver) {
    IsUsingServerConfig = true;
    Activate(config, resolver);
    LoadingScreen.LogInfo($"Using the server's loading screens. {Summary()}");
  }

  public static void UseLocalConfig() {
    if (!IsUsingServerConfig) {
      return;
    }

    IsUsingServerConfig = false;
    Activate(LocalConfig, LocalImagePath);
    LoadingScreen.LogInfo("Using the local loading screens again.");
  }

  public static void Reload() {
    if (PluginDir == null) {
      return;
    }

    CustomAssets.ClearImageCache();
    CustomAssets.Initialize(PluginDir);
    Load(PluginDir);
  }

  public static string Summary() {
    return $"{(IsUsingServerConfig ? "server config" : ConfigFileName)}: {Current.General.Entries.Count} general, "
        + $"{Current.Teleport.Values.Sum(list => list.Entries.Count)} teleport "
        + $"({string.Join(", ", Current.Teleport.Keys)}), "
        + $"{Current.Death.Values.Sum(list => list.Entries.Count)} death entries "
        + $"({string.Join(", ", Current.Death.Keys)}), {Current.Layouts.Count} layouts, "
        + $"{_lastWarningCount} warnings in {ConfigFileName} (see BepInEx/LogOutput.log); "
        + $"{CustomAssets.LoadingImageFiles.Count} plain images, {CustomAssets.LoadingTips.Count} tips."
        + (IsUsingServerConfig ? $" {ServerImages.Status()}" : string.Empty);
  }

  public static bool Load(string pluginDir) {
    PluginDir = pluginDir;

    try {
      return LoadWithYaml(pluginDir);
    } catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException) {
      LoadingScreen.LogError(
          $"Could not load YamlDotNet ({exception.Message}). Install the ValheimModding-YamlDotNet package; "
              + "situational screens are disabled.");
      return false;
    }
  }

  public static ConfigLoadResult ParseServerYaml(string yaml, Func<string, bool> isListed) {
    try {
      return ParseWithYaml(yaml, isListed);
    } catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException) {
      LoadingScreen.LogError($"Could not load YamlDotNet ({exception.Message}); server config ignored.");
      return null;
    }
  }

  public static string ReadLocalYaml() {
    string path = PluginDir == null ? null : Path.Combine(PluginDir, ConfigFileName);
    return path != null && File.Exists(path) ? File.ReadAllText(path) : string.Empty;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static ConfigLoadResult ParseWithYaml(string yaml, Func<string, bool> imageExists) {
    return new ConfigParser(imageExists, DeathCauses.Names).Parse(yaml);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool LoadWithYaml(string pluginDir) {
    string path = Path.Combine(pluginDir, ConfigFileName);

    if (!File.Exists(path)) {
      ExampleConfig.Write(path);
    }

    ConfigLoadResult result =
        ParseWithYaml(File.ReadAllText(path), relativePath => File.Exists(LocalImagePath(relativePath)));

    foreach (string warning in result.Warnings) {
      LoadingScreen.LogWarning($"{ConfigFileName}: {warning}");
    }

    if (!result.IsValid && !LocalConfig.IsEmpty) {
      LoadingScreen.LogWarning($"{ConfigFileName} is invalid; keeping the previously loaded config.");
      return false;
    }

    LocalConfig = result.Config;
    _lastWarningCount = result.Warnings.Count;

    if (!IsUsingServerConfig) {
      Activate(LocalConfig, LocalImagePath);
    }

    LoadingScreen.LogInfo(Summary());
    ServerImages.OnLocalConfigLoaded();

    return true;
  }

  static void Activate(ScreenConfig config, Func<string, string> resolver) {
    LoadingAudio.StopAllSounds();
    _resolver = resolver;
    Current = config;
    Selector = new(
        Current,
        entry => (entry.Image == null || IsImageAvailable(entry.Image))
            && Conditions.AreMet(entry.Conditions, PlayerConditions.Instance));
    LoadingManager.RefreshAppearance();
    ImageScaleUtils.RefreshAppearance();
    SceneLoaderPatch.RefreshAppearance();
  }
}
