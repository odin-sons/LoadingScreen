namespace LoadingScreen.Model;

public enum ModLogLevel {
  Error,
  Warning,
  Info,
  Debug
}

public enum LogSeverity {
  Fatal,
  Error,
  Warning,
  Info,
  Debug
}

public static class ModLogFilter {
  public static bool Allows(ModLogLevel configured, ModLogLevel message) => message <= configured;

  public static bool Allows(ModLogLevel configured, LogSeverity severity) => Allows(configured, Threshold(severity));

  public static ModLogLevel Threshold(LogSeverity severity) => severity switch {
    LogSeverity.Fatal or LogSeverity.Error => ModLogLevel.Error,
    LogSeverity.Warning => ModLogLevel.Warning,
    LogSeverity.Info => ModLogLevel.Info,
    _ => ModLogLevel.Debug
  };
}
