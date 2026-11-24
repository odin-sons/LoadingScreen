namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class LogContextTests {
  [Fact]
  public void MessageQuotesValuesThatWouldBreakFields() {
    Assert.Equal("Could not read config. file=loadingscreen.yaml error=\"access denied\" value=\"a=b\"",
        LogContext.Message("Could not read config.", ("file", "loadingscreen.yaml"),
            ("error", "access denied"), ("value", "a=b")));
  }

  [Fact]
  public void MessageEscapesLineBreaksAndControlCharactersInTheSentence() {
    string forged = LogContext.Message("unknown key 'x\r\n[Error  : LoadingScreen] y\u0007\u2028'", ("source", "server"));
    Assert.Equal("unknown key 'x\\r\\n[Error  : LoadingScreen] y\\u0007\\u2028' source=server", forged);
  }

  [Fact]
  public void MessageTruncatesLongSentencesAndValues() {
    string message = LogContext.Message(new string('a', 5000), ("value", new string('b', 5000)));
    Assert.StartsWith(new string('a', LogContext.MaxSentenceLength) + "... value=", message);
    Assert.EndsWith(new string('b', LogContext.MaxValueLength) + "...", message);
  }

  [Fact]
  public void FileKeepsPathsRelativeToTheSuppliedFolder() {
    string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LoadingScreen", "images");
    string path = System.IO.Path.Combine(folder, "biome", "forest.png");
    Assert.Equal("biome/forest.png", LogContext.File(path, folder));
    Assert.Equal("forest.png", LogContext.File(path));
  }

  [Fact]
  public void LogOnceUsesDistinctKeys() {
    LogOnce once = new();
    Assert.True(once.First("hud:LoadingImage"));
    Assert.False(once.First("hud:LoadingImage"));
    Assert.True(once.First("menu:LoadingImage"));
  }
}
