namespace LoadingScreen;

using System;
using System.Globalization;
using System.IO;
using System.Reflection;

using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using global::LoadingScreen.Model;

using static PluginConfig;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(ConditionalConfigSyncAPI.ConfigSync.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class LoadingScreen : BaseUnityPlugin {
  public const string PluginGuid = "Odin_Sons.LoadingScreen";
  public const string PluginName = "LoadingScreen";
  public const string PluginVersion = "0.4.0";

  static ManualLogSource _logger;

  void Awake() {
    _logger = Logger;
    BindConfig(Config, new ConfigSynchronization(Logger));

    if (IsModEnabled.Value) {
      try {
        string imageFolder = ImageFolderPath.Resolve(ImageFolder.Value, Paths.BepInExRootPath);
        CustomAssets.Initialize(imageFolder);
        ScreenConfigLoader.Load(imageFolder);
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
