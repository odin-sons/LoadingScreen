namespace LoadingScreen.Model;

public enum ModLogLevel {
  Error,
  Warning,
  Info,
  Debug
}

public static class ModLogFilter {
  public static bool Allows(ModLogLevel configured, ModLogLevel message) => message <= configured;
}
