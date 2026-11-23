namespace LoadingScreen.Model;

public static class BuiltinTipsSelection {
  public static bool ShouldUse(ConfigLoadResult userConfig, bool enabled) =>
      enabled && userConfig.IsValid && !userConfig.HasDeclaredEntries;
}
