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

  [Theory]
  [InlineData(LogSeverity.Fatal, ModLogLevel.Error)]
  [InlineData(LogSeverity.Error, ModLogLevel.Error)]
  [InlineData(LogSeverity.Warning, ModLogLevel.Warning)]
  [InlineData(LogSeverity.Info, ModLogLevel.Info)]
  [InlineData(LogSeverity.Debug, ModLogLevel.Debug)]
  public void SeverityMapsOntoModLevel(LogSeverity severity, ModLogLevel expected) {
    Assert.Equal(expected, ModLogFilter.Threshold(severity));
  }

  [Theory]
  [InlineData(0, ModLogLevel.Info)]
  [InlineData(1, ModLogLevel.Warning)]
  [InlineData(12, ModLogLevel.Warning)]
  public void MessageThresholdDependsOnReportedProblems(int problems, ModLogLevel expected) {
    Assert.Equal(expected, ModLogFilter.Threshold(LogSeverity.Message, problems));
  }

  [Fact]
  public void CleanSummaryIsHiddenByTheDefaultLevel() {
    Assert.False(ModLogFilter.Allows(ModLogLevel.Warning, LogSeverity.Message));
    Assert.True(ModLogFilter.Allows(ModLogLevel.Warning, LogSeverity.Message, problems: 2));
    Assert.False(ModLogFilter.Allows(ModLogLevel.Error, LogSeverity.Message, problems: 2));
  }

  [Fact]
  public void FatalPassesTheStrictestFilter() {
    Assert.True(ModLogFilter.Allows(ModLogLevel.Error, LogSeverity.Fatal));
  }
}
