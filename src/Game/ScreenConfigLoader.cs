namespace LoadingScreen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using global::LoadingScreen.Model;

public static class ScreenConfigLoader {
  public const string ConfigFileName = "loadingscreen.yaml";
  public const string AnimationsFileName = "animations.yaml";

  public static ScreenConfig Current { get; private set; } = new();
  public static EntrySelector Selector { get; private set; } = new(new ScreenConfig(), debug: LoadingScreen.LogDebug);
  public static string PluginDir { get; private set; }
  public static ScreenConfig LocalConfig { get; private set; } = new();
  public static bool IsUsingServerConfig { get; private set; }

  static Func<string, string> _resolver = LocalImagePath;
  static int _lastWarningCount;
  static bool _biomeWarningsChecked;
  static readonly HashSet<string> ReportedUnknownBiomes = new(StringComparer.OrdinalIgnoreCase);

  public static void TickBiomeWarnings() {
    if (_biomeWarningsChecked || !BiomeResolver.Instance.IsReady) return;
    if (BiomeResolver.Instance.IsInstalled && !Player.m_localPlayer) return;

    IEnumerable<string> names = Current.Teleport.Keys.Concat(
        Current.AllLists.SelectMany(list => list.Entries)
            .SelectMany(entry => entry.Conditions)
            .Where(condition => condition.Kind == ConditionKind.KnownBiome)
            .Select(condition => condition.Name));

    foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase)) {
      if (!BiomeResolver.Instance.TryResolve(name, out _) && ReportedUnknownBiomes.Add(name)) {
        LoadingScreen.LogWarning($"Unknown biome '{name}' in {ConfigFileName}; use a vanilla biome or the EWD biome: identifier.");
      }
    }

    _biomeWarningsChecked = true;
  }

  public static string ResolveImagePath(string relativePath) {
    return relativePath == null ? null : _resolver(relativePath);
  }

  public static bool IsImageAvailable(string relativePath) {
    string path = ResolveImagePath(relativePath);
    return path != null && File.Exists(path);
  }

  static bool IsEntryUsable(Entry entry) {
    if (entry.Image != null && !IsImageAvailable(entry.Image)) {
      LoadingScreen.LogDebug($"Skipping entry {entry.Image}: image is unavailable.");
      return false;
    }
    if (!Conditions.AreMet(entry.Conditions, PlayerConditions.Instance)) {
      LoadingScreen.LogDebug($"Skipping entry {entry.Image ?? "(no image)"}: conditions are not met.");
      return false;
    }
    return true;
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
        + $"{Current.Sleep.Entries.Count} sleep, "
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
    UiText.Load(pluginDir);

    try {
      return LoadWithYaml(pluginDir);
    } catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException) {
      LoadingScreen.LogError(
          $"Could not load YamlDotNet ({exception.Message}). Install the ValheimModding-YamlDotNet package; "
              + "situational screens are disabled.");
      return false;
    }
  }

  public static ConfigLoadResult ParseServerYaml(string yaml, string animationsYaml, Func<string, bool> isListed) {
    try {
      ConfigLoadResult result = ParseWithYaml(yaml, isListed);
      ApplyAnimations(result, animationsYaml);
      return result;
    } catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException) {
      LoadingScreen.LogError($"Could not load YamlDotNet ({exception.Message}); server config ignored.");
      return null;
    }
  }

  public static string ReadLocalYaml() {
    string path = PluginDir == null ? null : Path.Combine(PluginDir, ConfigFileName);
    return path != null && File.Exists(path) ? File.ReadAllText(path) : string.Empty;
  }

  public static string ReadLocalAnimationsYaml() {
    string path = PluginDir == null ? null : Path.Combine(PluginDir, AnimationsFileName);
    return path != null && File.Exists(path) ? File.ReadAllText(path) : string.Empty;
  }

  static void ApplyAnimations(ConfigLoadResult result, string yaml) {
    AnimationCatalogLoadResult animations = AnimationPresetParser.Parse(yaml);
    result.Config.Defaults.Animations = animations.Catalog;
    result.Warnings.AddRange(animations.Warnings.Select(warning => $"{AnimationsFileName}: {warning}"));
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
    ApplyAnimations(result, ReadLocalAnimationsYaml());

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
    LoadingManager.Session.Reset();
    _resolver = resolver;
    Current = config;
    _biomeWarningsChecked = false;
    Selector = new(
        Current,
        IsEntryUsable,
        biomes: BiomeResolver.Instance,
        debug: LoadingScreen.LogDebug);
    TickBiomeWarnings();
    LoadingManager.RefreshAppearance();
    ImageScaleUtils.RefreshAppearance();
    SceneLoaderPatch.RefreshAppearance();
  }
}
