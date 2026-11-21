namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class LanguageTextTests {
  [Theory]
  [InlineData("Game", "Russian", "ru")]
  [InlineData("pt-BR", "Russian", "pt-BR")]
  [InlineData("Game", "Chinese_Trad", "zh-TW")]
  [InlineData("Game", "Portuguese_European", "pt")]
  [InlineData("unknown", "German", "de")]
  [InlineData("Game", null, "en")]
  public void ResolvesGameAndForcedLanguage(string setting, string game, string expected) {
    Assert.Equal(expected, LanguageText.Code(setting, game));
  }

  [Fact]
  public void PrefersLocalizedTipsAndFallsBackToPlainTips() {
    Assert.Equal("tips.ru.txt", LanguageText.TipFileName("ru", name => name == "tips.ru.txt"));
    Assert.Equal("tips.txt", LanguageText.TipFileName("ru", _ => false));
  }

  [Fact]
  public void EntryTextUsesSelectedTranslationThenEnglishThenPlainText() {
    Entry entry = new() { Text = "Plain" };
    entry.Translations["en"] = "English";
    entry.Translations["ru"] = "Russian";

    Assert.Equal("Russian", entry.GetText("ru"));
    Assert.Equal("English", entry.GetText("de"));
    entry.Translations.Clear();
    Assert.Equal("Plain", entry.GetText("ru"));
  }

  [Fact]
  public void ParsesLocalizedTextAndKeepsTextOnlyEntries() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse("""
        situations:
          general:
            - text: { en: Welcome, ru: Привет }
        """);

    Assert.Empty(result.Warnings);
    Entry entry = Assert.Single(result.Config.General.Entries);
    Assert.Null(entry.Image);
    Assert.Equal("Привет", entry.GetText("ru"));
    Assert.Equal("Welcome", entry.GetText("de"));
  }

  [Fact]
  public void InvalidTranslationDoesNotDiscardValidEntry() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse("""
        situations:
          general:
            - image: image.jpg
              text: { xx: Unknown, en: Welcome, ru: [wrong] }
        """);

    Entry entry = Assert.Single(result.Config.General.Entries);
    Assert.Equal("Welcome", entry.GetText("ru"));
    Assert.Equal(2, result.Warnings.Count);
  }
}
