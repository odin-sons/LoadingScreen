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

  void Awake() {
    Log.Attach(Logger);
    BindConfig(Config, new ConfigSynchronization());
    LoadingAudio.Initialize(gameObject);
    WorldSceneLoad.Host = this;

    try {
      string imageFolder = ImageFolderPath.Resolve(ImageFolder.Value, Paths.BepInExRootPath);
      CustomAssets.Initialize(imageFolder);
      ScreenConfigLoader.Load(imageFolder);
    } catch (Exception exception) {
      Log.Plugin.Fatal(LogContext.Message("Loading screen configuration failed to initialize.",
          ("folder", LogContext.File(ImageFolder.Value, Paths.BepInExRootPath)), ("error", exception.Message)));
      Log.Plugin.Fatal(exception);
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

    try {
      Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);
    } catch (Exception exception) {
      Log.Plugin.Fatal(LogContext.Message("Harmony patching failed; the loading screen is disabled.",
          ("operation", "patch"), ("harmony", PluginGuid), ("error", exception.Message)));
      Log.Plugin.Fatal(exception);
    }
  }

  void Update() {
    if (!IsModEnabled.Value) return;
    ServerImages.Tick();
    ConfigProblemReport.Tick();
    ScreenConfigLoader.TickBiomeWarnings();
  }

  static void WarnIfDebugIsFiltered() {
    if (PluginConfig.LogLevel.Value != ModLogLevel.Debug) return;
    DiskLogListener disk = BepInEx.Logging.Logger.Listeners.OfType<DiskLogListener>().FirstOrDefault();
    if (disk == null || (disk.DisplayedLogLevel & BepInEx.Logging.LogLevel.Debug) != 0) return;
    Log.Plugin.WarningOnce("log-level-debug-filtered", LogContext.Message(
        "Debug messages are hidden by BepInEx; add Debug to LogLevels under [Logging.Disk] in BepInEx/config/BepInEx.cfg.",
        ("setting", "Log level"), ("bepinex", disk.DisplayedLogLevel)));
  }
}
