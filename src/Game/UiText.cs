namespace LoadingScreen;

using System;
using System.IO;

using global::LoadingScreen.Model;

public static class UiText {
  const string FileName = "translations.yaml";
  static UiTranslations _translations = new();

  public static string Get(string key) =>
      _translations.Get(Localization.instance?.GetSelectedLanguage(), key);

  public static string FormatProgress(int percent, int seconds) =>
      _translations.FormatProgress(Localization.instance?.GetSelectedLanguage(), percent, seconds);

  public static void Load(string folder) {
    string path = Path.Combine(folder, FileName);
    try {
      UiTranslations loaded = UiTranslations.Parse(File.Exists(path) ? File.ReadAllText(path) : string.Empty);
      _translations = loaded;
      foreach (string warning in loaded.Warnings) LoadingScreen.LogWarning($"{FileName}: {warning}");
    } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
      LoadingScreen.LogWarning($"Could not read {FileName}: {exception.Message}");
    }
  }
}
