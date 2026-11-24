namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class UiTranslationsTests {
  [Theory]
  [InlineData(100000)]
  [InlineData(33)]
  public void DeepNestingFallsBackToBuiltInText(int depth) {
    UiTranslations translations = UiTranslations.Parse("English: " + new string('[', depth) + new string(']', depth));
    Assert.Equal("s", translations.Get("English", "secondsUnit"));
    Assert.Contains(translations.Warnings, warning => warning.Contains("ignored"));
  }

  [Fact]
  public void AliasesAndOversizedFilesAreIgnored() {
    Assert.Contains(UiTranslations.Parse("a: &x 1\nb: *x\n").Warnings, warning => warning.Contains("aliases"));
    Assert.Contains(UiTranslations.Parse("# " + new string('x', UiTranslations.MaxBytes)).Warnings,
        warning => warning.Contains("maxBytes"));
  }

  [Theory]
  [InlineData("Russian", "с")]
  [InlineData("zh-TW", "秒")]
  [InlineData("Portuguese_Brazilian", "s")]
  [InlineData("Abenaki", "s")]
  public void UsesBuiltInGameLanguages(string language, string unit) {
    UiTranslations translations = new();
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
          connectingStage: "Подключение"
        en:
          secondsUnit: sec
        """);

    Assert.Equal("Подключение", translations.Get("Russian", "connectingStage"));
    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.Equal("Connecting", translations.Get("Unknown", "connectingStage"));
    Assert.Equal("sec", translations.Get("English", "secondsUnit"));
  }

  [Fact]
  public void UnknownLanguageAndInvalidOverridesFallBackPerKey() {
    UiTranslations translations = UiTranslations.Parse("""
        Russian:
          connectingStage: ""
          secondsUnit: [invalid]
          unknownKey: unused
        Unknown:
          connectingStage: unused
        """);

    Assert.Equal("Connecting", translations.Get("Russian", "connectingStage"));
    Assert.Equal("с", translations.Get("Russian", "secondsUnit"));
    Assert.Equal("Connecting", translations.Get("Unknown", "connectingStage"));
    Assert.Contains(translations.Warnings, warning => warning.StartsWith(
        "Unknown translation key ignored. file=translations.yaml line=") && warning.Contains("key=unknownKey"));
    Assert.Contains(translations.Warnings, warning =>
        warning.StartsWith("Unknown translation language ignored.") && warning.Contains("language=Unknown"));
  }

  [Fact]
  public void RemovedContinuePromptKeyIsReportedAndIgnored() {
    UiTranslations translations = UiTranslations.Parse("""
        Russian:
          continuePrompt: "Продолжить"
        """);

    Assert.Null(translations.Get("Russian", "continuePrompt"));
    Assert.DoesNotContain("continuePrompt", UiTranslations.Keys);
    Assert.Contains(translations.Warnings, warning =>
        warning.StartsWith("Removed translation key ignored.") && warning.Contains("key=continuePrompt"));
    Assert.DoesNotContain(translations.Warnings, warning => warning.StartsWith("Unknown translation key"));
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
