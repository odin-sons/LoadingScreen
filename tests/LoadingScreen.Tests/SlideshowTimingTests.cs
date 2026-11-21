namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class SlideshowTimingTests {
  [Theory]
  [InlineData(0, 10f, 0f, false)]
  [InlineData(-1, 10f, 0f, false)]
  [InlineData(5, 14.9f, 10f, false)]
  [InlineData(5, 15f, 10f, true)]
  [InlineData(5, 15.1f, 10f, true)]
  [InlineData(5, 9f, 10f, false)]
  public void AdvancesOnlyAfterPositiveIntervalElapses(int intervalSeconds, float now, float lastImageTime, bool expected) {
    Assert.Equal(expected, SlideshowTiming.IsDue(now, lastImageTime, intervalSeconds));
  }
}
