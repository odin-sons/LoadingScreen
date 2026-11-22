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
  public const string PluginVersion = "0.6.0";

  static ManualLogSource _logger;

  void Awake() {
    _logger = Logger;
    BindConfig(Config, new ConfigSynchronization());
    LoadingAudio.Initialize(gameObject);

    try {
      string imageFolder = ImageFolderPath.Resolve(ImageFolder.Value, Paths.BepInExRootPath);
      CustomAssets.Initialize(imageFolder);
      ScreenConfigLoader.Load(imageFolder);
    } catch (Exception exception) {
      LogError($"Could not load the loading screen config: {exception}");
    }

    IsModEnabled.SettingChanged += (_, _) => {
      if (IsModEnabled.Value) LoadingManager.Enable();
      ServerImages.OnSettingsChanged();
      if (!IsModEnabled.Value) LoadingManager.Disable();
    };
    ShareScreensWithPlayers.SettingChanged += (_, _) => ServerImages.OnSettingsChanged();
    Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);
  }

  void Update() {
    if (!IsModEnabled.Value) return;
    ServerImages.Tick();
  }

  public static void LogInfo(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Info))
      _logger.LogInfo(FormatLog(obj));
  }

  public static void LogWarning(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Warning))
      _logger.LogWarning(FormatLog(obj));
  }

  public static void LogError(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Error))
      _logger.LogError(FormatLog(obj));
  }

  public static void LogDebug(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Debug))
      _logger.LogDebug(FormatLog(obj));
  }

  static string FormatLog(object obj) => $"[{DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo)}] {obj}";
}
