namespace LoadingScreen;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using global::LoadingScreen.Model;

using static PluginConfig;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(ConditionalConfigSyncAPI.ConfigSync.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("expand_world_data", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class LoadingScreen : BaseUnityPlugin {
  public const string PluginGuid = "Odin_Sons.LoadingScreen";
  public const string PluginName = "LoadingScreen";
  public const string PluginVersion = "0.6.0";

  static ManualLogSource _logger;
  static readonly LogOnce ReportedWarnings = new();

  void Awake() {
    _logger = Logger;
    BindConfig(Config, new ConfigSynchronization());
    LoadingAudio.Initialize(gameObject);

    try {
      string imageFolder = ImageFolderPath.Resolve(ImageFolder.Value, Paths.BepInExRootPath);
      CustomAssets.Initialize(imageFolder);
      ScreenConfigLoader.Load(imageFolder);
    } catch (Exception exception) {
      LogError(LogContext.Message("Loading screen configuration failed to initialize.",
          ("folder", LogContext.File(ImageFolder.Value, Paths.BepInExRootPath)), ("error", exception.Message)));
      LogError(exception);
    }

    IsModEnabled.SettingChanged += (_, _) => {
      if (IsModEnabled.Value) LoadingManager.Enable();
      ServerImages.OnSettingsChanged();
      if (!IsModEnabled.Value) LoadingManager.Disable();
    };
    ShareScreensWithPlayers.SettingChanged += (_, _) => ServerImages.OnSettingsChanged();
    PluginConfig.LogLevel.SettingChanged += (_, _) => WarnIfDebugIsFiltered();
    WarnIfDebugIsFiltered();
    ReadableText.SettingChanged += (_, _) => LoadingManager.RefreshAppearance();
    MinimumTextSize.SettingChanged += (_, _) => LoadingManager.RefreshAppearance();
    Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);
  }

  void Update() {
    if (!IsModEnabled.Value) return;
    ServerImages.Tick();
    ScreenConfigLoader.TickBiomeWarnings();
  }

  public static void LogInfo(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Info))
      _logger.LogInfo(obj);
  }

  public static void LogWarning(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Warning))
      _logger.LogWarning(obj);
  }

  public static void LogWarningOnce(string key, string message) {
    if (ReportedWarnings.First(key)) LogWarning(message);
  }

  public static void LogError(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Error))
      _logger.LogError(obj);
  }

  public static void LogDebug(object obj) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, ModLogLevel.Debug))
      _logger.LogDebug(obj);
  }

  static void WarnIfDebugIsFiltered() {
    if (PluginConfig.LogLevel.Value != ModLogLevel.Debug) return;
    DiskLogListener disk = BepInEx.Logging.Logger.Listeners.OfType<DiskLogListener>().FirstOrDefault();
    if (disk == null || (disk.DisplayedLogLevel & BepInEx.Logging.LogLevel.Debug) != 0) return;
    LogWarningOnce("log-level-debug-filtered", LogContext.Message(
        "Debug messages are hidden by BepInEx; add Debug to LogLevels under [Logging.Disk] in BepInEx/config/BepInEx.cfg.",
        ("setting", "Log level"), ("bepinex", disk.DisplayedLogLevel)));
  }
}
