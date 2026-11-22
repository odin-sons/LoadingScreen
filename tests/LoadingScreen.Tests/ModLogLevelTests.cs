namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class ModLogLevelTests {
  [Theory]
  [InlineData(ModLogLevel.Error, true, false, false, false)]
  [InlineData(ModLogLevel.Warning, true, true, false, false)]
  [InlineData(ModLogLevel.Info, true, true, true, false)]
  [InlineData(ModLogLevel.Debug, true, true, true, true)]
  public void ConfiguredLevelControlsSeverity(
      ModLogLevel configured, bool error, bool warning, bool info, bool debug) {
    Assert.Equal(error, ModLogFilter.Allows(configured, ModLogLevel.Error));
    Assert.Equal(warning, ModLogFilter.Allows(configured, ModLogLevel.Warning));
    Assert.Equal(info, ModLogFilter.Allows(configured, ModLogLevel.Info));
    Assert.Equal(debug, ModLogFilter.Allows(configured, ModLogLevel.Debug));
  }
}
