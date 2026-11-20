namespace LoadingScreen;

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using global::LoadingScreen.Screens;

public static class ScreenConfigLoader {
  public const string ConfigFileName = "loadingscreen.yaml";

  public static ScreenConfig Current { get; private set; } = new();
  public static EntrySelector Selector { get; private set; } = new(new ScreenConfig());
  public static string PluginDir { get; private set; }

  public static string ResolveImagePath(string relativePath) {
    return Path.Combine(PluginDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
  }

  static int _lastWarningCount;

  public static void Reload() {
    if (PluginDir == null) {
      return;
    }

    CustomAssets.ClearImageCache();
    CustomAssets.Initialize(PluginDir);
    Load(PluginDir);
  }

  public static string Summary() {
    return $"{ConfigFileName}: {Current.General.Entries.Count} general, "
        + $"{Current.Teleport.Values.Sum(list => list.Entries.Count)} teleport "
        + $"({string.Join(", ", Current.Teleport.Keys)}), "
        + $"{Current.Death.Values.Sum(list => list.Entries.Count)} death entries "
        + $"({string.Join(", ", Current.Death.Keys)}), {Current.Layouts.Count} layouts, "
        + $"{_lastWarningCount} warnings (see BepInEx/LogOutput.log); "
        + $"{CustomAssets.LoadingImageFiles.Count} plain images, {CustomAssets.LoadingTips.Count} tips.";
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

  [MethodImpl(MethodImplOptions.NoInlining)]
  static bool LoadWithYaml(string pluginDir) {
    string path = Path.Combine(pluginDir, ConfigFileName);

    if (!File.Exists(path)) {
      ExampleConfig.Write(path);
    }

    ConfigParser parser = new(relativePath => File.Exists(ResolveImagePath(relativePath)), DeathCauses.Names);
    ConfigLoadResult result = parser.Parse(File.ReadAllText(path));

    foreach (string warning in result.Warnings) {
      LoadingScreen.LogWarning($"{ConfigFileName}: {warning}");
    }

    if (!result.IsValid && !Current.IsEmpty) {
      LoadingScreen.LogWarning($"{ConfigFileName} is invalid; keeping the previously loaded config.");
      return false;
    }

    Current = result.Config;
    Selector = new(Current, entry => File.Exists(ResolveImagePath(entry.Image)));

    _lastWarningCount = result.Warnings.Count;
    LoadingScreen.LogInfo(Summary());

    return true;
  }
}
