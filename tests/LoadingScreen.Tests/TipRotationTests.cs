namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class TipRotationTests {
  [Theory]
  [InlineData(0, 10f, 0f, null, 2, false)]
  [InlineData(-1, 10f, 0f, null, 2, false)]
  [InlineData(5, 4.9f, 0f, null, 2, false)]
  [InlineData(5, 5f, 0f, null, 2, true)]
  [InlineData(5, 5f, 0f, "Entry caption", 2, false)]
  [InlineData(5, 5f, 0f, "", 2, false)]
  [InlineData(5, 5f, 0f, null, 0, false)]
  public void RotatesOnlyAvailableTipsWithoutAFixedCaption(
      int intervalSeconds, float now, float lastTipTime, string currentCaption, int tipCount, bool expected) {
    Assert.Equal(expected, TipRotation.IsDue(now, lastTipTime, intervalSeconds, currentCaption, tipCount));
  }
}
