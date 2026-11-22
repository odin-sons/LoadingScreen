namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class UiTranslationsTests {
  [Theory]
  [InlineData("Russian", "Нажмите любую клавишу, чтобы продолжить", "с")]
  [InlineData("zh-TW", "按任意鍵繼續", "秒")]
  [InlineData("Portuguese_Brazilian", "Pressione qualquer tecla para continuar", "s")]
  [InlineData("Abenaki", "Ôlmi", "s")]
  public void UsesBuiltInGameLanguages(string language, string prompt, string unit) {
    UiTranslations translations = new();
    Assert.Equal(prompt, translations.Get(language, "continuePrompt"));
    Assert.Equal(unit, translations.Get(language, "secondsUnit"));
  }

  [Fact]
  public void CoversEachShippedLanguageAndEachUiKey() {
    UiTranslations translations = new();
    Assert.Equal(36, UiTranslations.Languages.Count);
    foreach (string language in UiTranslations.Languages) {
      foreach (string key in UiTranslations.Keys) {
        Assert.False(string.IsNullOrWhiteSpace(translations.Get(language, key)));
      }
    }
  }

  [Fact]
  public void OverridesOneKeyWithoutReplacingOtherKeysOrLanguages() {
    UiTranslations translations = UiTranslations.Parse("""
        Russian:
          continuePrompt: "Продолжить"
        en:
          secondsUnit: sec
        """);

    Assert.Equal("Продолжить", translations.Get("Russian", "continuePrompt"));
    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.Equal("Press any key to continue", translations.Get("Unknown", "continuePrompt"));
    Assert.Equal("sec", translations.Get("English", "secondsUnit"));
  }

  [Fact]
  public void UnknownLanguageAndInvalidOverridesFallBackPerKey() {
    UiTranslations translations = UiTranslations.Parse("""
        Russian:
          continuePrompt: ""
          secondsUnit: [invalid]
          unknownKey: unused
        Unknown:
          continuePrompt: unused
        """);

    Assert.Equal("Нажмите любую клавишу, чтобы продолжить", translations.Get("Russian", "continuePrompt"));
    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.Equal("Press any key to continue", translations.Get("Unknown", "continuePrompt"));
    Assert.NotEmpty(translations.Warnings);
  }

  [Fact]
  public void ProgressUsesIntegerValuesAndLocalizedUnit() {
    UiTranslations translations = UiTranslations.Parse("""
        German:
          gameStartProgress: "Lädt: {percent}% ({seconds} {unit})"
        """);

    Assert.Equal("Lädt: 42% (7 s)", translations.FormatProgress("German", 42, 7));
    Assert.Equal("<b>42%</b>\n<size=-4>(7с)</size>", translations.FormatProgress("Russian", 42, 7));
  }

  [Fact]
  public void InvalidYamlFallsBackToBuiltIns() {
    UiTranslations translations = UiTranslations.Parse("Russian: [unfinished");

    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.NotEmpty(translations.Warnings);
  }
}
