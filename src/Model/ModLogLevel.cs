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
  Message,
  Info,
  Debug
}

public static class ModLogFilter {
  public static bool Allows(ModLogLevel configured, ModLogLevel message) => message <= configured;

  public static bool Allows(ModLogLevel configured, LogSeverity severity, int problems = 0) =>
      Allows(configured, Threshold(severity, problems));

  public static ModLogLevel Threshold(LogSeverity severity, int problems = 0) => severity switch {
    LogSeverity.Fatal or LogSeverity.Error => ModLogLevel.Error,
    LogSeverity.Warning => ModLogLevel.Warning,
    LogSeverity.Message => problems > 0 ? ModLogLevel.Warning : ModLogLevel.Info,
    LogSeverity.Info => ModLogLevel.Info,
    _ => ModLogLevel.Debug
  };
}
