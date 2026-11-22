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
