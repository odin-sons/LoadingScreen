namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class UiTranslationsTests {
  [Theory]
  [InlineData(100000)]
  [InlineData(33)]
  public void DeepNestingFallsBackToBuiltInText(int depth) {
    UiTranslations translations = UiTranslations.Parse("English: " + new string('[', depth) + new string(']', depth));
    Assert.Equal("Press any key to continue", translations.Get("English", "continuePrompt"));
    Assert.Contains(translations.Warnings, warning => warning.Contains("ignored"));
  }

  [Fact]
  public void AliasesAndOversizedFilesAreIgnored() {
    Assert.Contains(UiTranslations.Parse("a: &x 1\nb: *x\n").Warnings, warning => warning.Contains("aliases"));
    Assert.Contains(UiTranslations.Parse("# " + new string('x', UiTranslations.MaxBytes)).Warnings,
        warning => warning.Contains("maxBytes"));
  }

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
  public void VolumeSliderLabelsFallBackToEnglishAndAcceptOverrides() {
    UiTranslations translations = UiTranslations.Parse("""
        Russian:
          musicVolumeSlider: "Музыка экрана загрузки"
        """);

    Assert.Equal("Музыка экрана загрузки", translations.Get("Russian", "musicVolumeSlider"));
    Assert.Equal("Loading screen voice", translations.Get("Russian", "voiceVolumeSlider"));
    Assert.Equal("Loading screen music", translations.Get("German", "musicVolumeSlider"));
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
    Assert.Contains(translations.Warnings, warning => warning.StartsWith(
        "Unknown translation key ignored. file=translations.yaml line=") && warning.Contains("key=unknownKey"));
    Assert.Contains(translations.Warnings, warning =>
        warning.StartsWith("Unknown translation language ignored.") && warning.Contains("language=Unknown"));
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
  public void ConfigProblemsFallBackToEnglishAndAcceptOverrides() {
    UiTranslations translations = UiTranslations.Parse("""
        German:
          configProblems: "Probleme in {files}."
        """);

    Assert.Equal("LoadingScreen found problems in animations.yaml: 1. Details are in BepInEx/LogOutput.log.",
        translations.FormatConfigProblems("Russian", "animations.yaml: 1"));
    Assert.Equal("Probleme in animations.yaml: 1.", translations.FormatConfigProblems("German", "animations.yaml: 1"));
  }

  [Fact]
  public void InvalidYamlFallsBackToBuiltIns() {
    UiTranslations translations = UiTranslations.Parse("Russian: [unfinished");

    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.NotEmpty(translations.Warnings);
  }
}
