namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class ConfigProblemTallyTests {
  [Fact]
  public void EmptyTallyHasNoDetails() {
    ConfigProblemTally tally = new();

    Assert.Equal(0, tally.Total);
    Assert.Equal(string.Empty, tally.Details());
  }

  [Fact]
  public void CountsProblemsPerTrackedFileInFixedOrder() {
    ConfigProblemTally tally = new();

    Assert.True(tally.Add(LogContext.Message("Translation text ignored.", ("file", "translations.yaml"), ("line", 3))));
    Assert.True(tally.Add(LogContext.Message("Invalid value.", ("file", "loadingscreen.yaml"), ("line", 7))));
    Assert.True(tally.Add(LogContext.Message("Invalid value.", ("file", "LoadingScreen.yaml"), ("line", 9))));

    Assert.Equal(3, tally.Total);
    Assert.Equal("loadingscreen.yaml: 2, translations.yaml: 1", tally.Details());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("Server file was rejected for sharing. file=images/a.png")]
  [InlineData("Harmony patching failed. operation=patch")]
  [InlineData("Some text with profile=animations.yaml")]
  public void IgnoresMessagesWithoutATrackedFile(string message) {
    ConfigProblemTally tally = new();

    Assert.False(tally.Add(message));
    Assert.Equal(0, tally.Total);
  }

  [Fact]
  public void ReadsQuotedFileValues() {
    ConfigProblemTally tally = new();

    Assert.True(tally.Add("Animation presets rejected. file=\"animations.yaml\" line=2"));
    Assert.Equal("animations.yaml: 1", tally.Details());
  }
}
