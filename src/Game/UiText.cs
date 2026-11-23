namespace LoadingScreen;

using System;
using System.IO;

using global::LoadingScreen.Model;

public static class UiText {
  const string FileName = UiTranslations.FileName;
  static UiTranslations _translations = new();

  public static string Get(string key) =>
      _translations.Get(Localization.instance?.GetSelectedLanguage(), key);

  public static string FormatProgress(int percent, int seconds) =>
      _translations.FormatProgress(Localization.instance?.GetSelectedLanguage(), percent, seconds);

  public static string FormatConfigProblems(string files) =>
      _translations.FormatConfigProblems(Localization.instance?.GetSelectedLanguage(), files);

  public static void Load(string folder) {
    string path = Path.Combine(folder, FileName);
    try {
      UiTranslations loaded = UiTranslations.Parse(File.Exists(path) ? File.ReadAllText(path) : string.Empty);
      _translations = loaded;
      foreach (string warning in loaded.Warnings) Log.Config.Warning(warning);
    } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
      Log.Config.Warning(LogContext.Message("Could not read translation overrides.",
          ("file", FileName), ("error", exception.Message)));
      Log.Config.Debug(exception);
    }
  }
}
