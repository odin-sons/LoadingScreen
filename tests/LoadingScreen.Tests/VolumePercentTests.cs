namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class VolumePercentTests {
  [Theory]
  [InlineData(0, 0f)]
  [InlineData(35, 0.35f)]
  [InlineData(100, 1f)]
  [InlineData(-5, 0f)]
  [InlineData(150, 1f)]
  public void MapsPercentToSliderRange(int percent, float expected) {
    Assert.Equal(expected, VolumePercent.ToSlider(percent), 4);
  }

  [Theory]
  [InlineData(0f, 0)]
  [InlineData(0.345f, 35)]
  [InlineData(0.994f, 99)]
  [InlineData(1f, 100)]
  [InlineData(-0.2f, 0)]
  [InlineData(1.7f, 100)]
  [InlineData(float.NaN, 0)]
  public void RoundsSliderValueToWholePercent(float value, int expected) {
    Assert.Equal(expected, VolumePercent.FromSlider(value));
  }

  [Fact]
  public void RoundTripsEveryPercent() {
    for (int percent = VolumePercent.Min; percent <= VolumePercent.Max; percent++) {
      Assert.Equal(percent, VolumePercent.FromSlider(VolumePercent.ToSlider(percent)));
    }
  }

  [Fact]
  public void FormatsLikeTheGameSliders() {
    Assert.Equal("40%", VolumePercent.Format(0.4f));
    Assert.Equal("100%", VolumePercent.Format(2f));
  }
}
