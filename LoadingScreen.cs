namespace LoadingScreen;

using System;
using System.Globalization;
using System.IO;
using System.Reflection;

using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using static PluginConfig;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class LoadingScreen : BaseUnityPlugin {
  public const string PluginGuid = "Odin_Sons.LoadingScreen";
  public const string PluginName = "LoadingScreen";
  public const string PluginVersion = "0.2.0";

  static ManualLogSource _logger;

  void Awake() {
    _logger = Logger;
    BindConfig(Config);

    if (IsModEnabled.Value) {
      string pluginDir = Path.Combine(Path.GetDirectoryName(Config.ConfigFilePath), PluginName);
      try {
        CustomAssets.Initialize(pluginDir);
        ScreenConfigLoader.Load(pluginDir);
      } catch (Exception exception) {
        LogError($"Could not load the loading screen config: {exception}");
      }

      Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);
    }
  }

  void Update() {
    ServerImages.Tick();
  }

  public static void LogInfo(object obj) {
    _logger.LogInfo($"[{DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo)}] {obj}");
  }

  public static void LogWarning(object obj) {
    _logger.LogWarning($"[{DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo)}] {obj}");
  }

  public static void LogError(object obj) {
    _logger.LogError($"[{DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo)}] {obj}");
  }
}
