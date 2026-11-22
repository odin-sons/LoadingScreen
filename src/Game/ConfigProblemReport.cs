namespace LoadingScreen;

using BepInEx.Logging;

using global::LoadingScreen.Model;

public static class ConfigProblemReport {
  const LogLevel ProblemLevels = LogLevel.Fatal | LogLevel.Error | LogLevel.Warning;

  static ConfigProblemTally _tally;
  static string _consoleDetails;
  static string _menuDetails;

  public static void Begin() {
    _tally = new();
    foreach (ModLogSource source in Log.All) {
      if (source.Source != null) source.Source.LogEvent += OnLogEvent;
    }
  }

  public static void End() {
    foreach (ModLogSource source in Log.All) {
      if (source.Source != null) source.Source.LogEvent -= OnLogEvent;
    }
    string details = _tally is { Total: > 0 } ? _tally.Details() : null;
    _tally = null;
    _consoleDetails = details;
    _menuDetails = details;
    Tick();
  }

  public static void Tick() {
    if (_consoleDetails == null && _menuDetails == null) return;

    if (_consoleDetails != null && Console.instance) {
      Console.instance.AddString(UiText.FormatConfigProblems(_consoleDetails));
      if (Console.IsVisible()) _menuDetails = null;
      _consoleDetails = null;
    }

    if (_menuDetails != null && FejdStartup.instance && !Console.IsVisible() && UnifiedPopup.IsAvailable()) {
      UnifiedPopup.Push(new WarningPopup(LoadingScreen.PluginName, UiText.FormatConfigProblems(_menuDetails),
          UnifiedPopup.Pop, localizeText: false));
      _menuDetails = null;
    }
  }

  static void OnLogEvent(object sender, LogEventArgs eventArgs) {
    if ((eventArgs.Level & ProblemLevels) != 0) _tally?.Add(eventArgs.Data?.ToString());
  }
}
