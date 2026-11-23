namespace LoadingScreen;

using System.Collections.Generic;

using BepInEx.Logging;

using global::LoadingScreen.Model;

public static class Log {
  public static readonly ModLogSource Plugin = new(null);
  public static readonly ModLogSource Config = Create("Config");
  public static readonly ModLogSource Audio = Create("Audio");
  public static readonly ModLogSource Server = Create("Server");
  public static readonly ModLogSource Render = Create("Render");

  public static IReadOnlyList<ModLogSource> All { get; } = [Plugin, Config, Audio, Server, Render];

  public static void Attach(ManualLogSource plugin) => Plugin.Source = plugin;

  static ModLogSource Create(string subsystem) =>
      new(BepInEx.Logging.Logger.CreateLogSource($"{LoadingScreen.PluginName}.{subsystem}"));
}

public sealed class ModLogSource {
  static readonly LogOnce ReportedWarnings = new();

  internal ModLogSource(ManualLogSource source) => Source = source;

  public ManualLogSource Source { get; internal set; }

  public void Fatal(object data) => Write(LogLevel.Fatal, LogSeverity.Fatal, data);

  public void Error(object data) => Write(LogLevel.Error, LogSeverity.Error, data);

  public void Warning(object data) => Write(LogLevel.Warning, LogSeverity.Warning, data);

  public void WarningOnce(string key, string message) {
    if (ReportedWarnings.First(key)) Warning(message);
  }

  public void Message(object data, int problems) => Write(LogLevel.Message, LogSeverity.Message, data, problems);

  public void Info(object data) => Write(LogLevel.Info, LogSeverity.Info, data);

  public void Debug(object data) => Write(LogLevel.Debug, LogSeverity.Debug, data);

  void Write(LogLevel level, LogSeverity severity, object data, int problems = 0) {
    if (ModLogFilter.Allows(PluginConfig.LogLevel?.Value ?? ModLogLevel.Warning, severity, problems))
      Source?.Log(level, data);
  }
}
