namespace LoadingScreen;

using BepInEx.Configuration;
using ConditionalConfigSyncAPI;
using global::LoadingScreen.Model;

internal sealed class ConfigSynchronization {
  readonly ConfigSync _conditionalSync;
  readonly ServerSync.ConfigSync _serverSync;

  public ConfigSynchronization() {
    _conditionalSync =
        new ConfigSync(
            LoadingScreen.PluginGuid,
            LoadingScreen.PluginName,
            LoadingScreen.PluginVersion,
            warningLogger: warning => LoadingScreen.LogWarning(LogContext.Message(
                "Conditional config synchronization warning.", ("feature", "ConditionalConfigSync"),
                ("detail", warning))));

    if (!_conditionalSync.TryActivate()) {
      _serverSync =
          new ServerSync.ConfigSync(LoadingScreen.PluginGuid) {
            DisplayName = LoadingScreen.PluginName,
            CurrentVersion = LoadingScreen.PluginVersion,
          };
    }
  }

  public void Register<T>(ConfigEntry<T> entry, bool clientControlled = false) {
    if (_serverSync == null) {
      _conditionalSync.RegisterConfigEntry(
          entry,
          clientControlled ? SyncMode.AlwaysClientControlled : SyncMode.Conditional);
    } else {
      _serverSync.AddConfigEntry(entry).SynchronizedConfig = !clientControlled;
    }
  }

  public void RegisterLock(ConfigEntry<bool> entry) {
    if (_serverSync == null) {
      _conditionalSync.RegisterLockingConfigEntry(entry);
    } else {
      _serverSync.AddLockingConfigEntry(entry);
    }
  }
}
