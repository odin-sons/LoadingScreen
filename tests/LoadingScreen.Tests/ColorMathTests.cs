namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class ColorMathTests {
  [Fact]
  public void RelativeLuminanceAndContrastUseWcagValues() {
    Assert.Equal(0d, ColorMath.RelativeLuminance(new(0, 0, 0)));
    Assert.Equal(1d, ColorMath.RelativeLuminance(new(255, 255, 255)), 6);
    Assert.Equal(21d, ColorMath.ContrastRatio(0d, 1d), 6);
    Assert.InRange(ColorMath.RelativeLuminance(new(128, 128, 128)), 0.215d, 0.217d);
  }

  [Fact]
  public void AutoTextChoosesBestContrast() {
    Assert.Equal(new ColorValue(255, 255, 255).ToString(), ColorMath.AutoText(0.01d).ToString());
    Assert.Equal(new ColorValue(0, 0, 0).ToString(), ColorMath.AutoText(0.9d).ToString());
  }

  [Fact]
  public void TranslucentBoxChangesEffectiveBackground() {
    ColorValue image = new(255, 255, 255);
    ColorValue effective = ColorMath.Blend(new(0, 0, 0, 153), image);
    Assert.Equal(new ColorValue(102, 102, 102).ToString(), effective.ToString());
    Assert.Equal(new ColorValue(255, 255, 255).ToString(),
        ColorMath.AutoText(ColorMath.RelativeLuminance(effective)).ToString());
  }

  [Fact]
  public void AverageLuminanceUsesTheSelectedImageArea() {
    ColorValue[] pixels = [
      new(0, 0, 0), new(255, 255, 255),
      new(0, 0, 0), new(255, 255, 255),
    ];
    Assert.Equal(0d, ColorMath.AverageLuminance(pixels, 2, 2, 0d, 0d, 0.5d, 1d));
    Assert.Equal(0.5d, ColorMath.AverageLuminance(pixels, 2, 2, 0d, 0d, 1d, 1d), 6);
    Assert.True(ColorMath.AverageLuminance(pixels, 2, 2, 0d, 0d, 1d, 1d, new(0, 0, 0, 153)) < 0.2d);
  }

  [Fact]
  public void TransparentPixelsUseTheUnderlyingBackground() {
    ColorValue[] pixels = [new(0, 0, 0, 0), new(0, 0, 0, 255)];
    Assert.Equal(0.5d, ColorMath.AverageLuminance(
        pixels, 2, 1, 0d, 0d, 1d, 1d, backing: new ColorValue(255, 255, 255)), 6);
  }

  [Fact]
  public void ReadableTextUsesTheSampledRangeAndRendersOpaque() {
    ColorValue[] pixels = [new(0, 0, 0), new(255, 255, 255)];
    (double Low, double High) Range(ColorValue? box) => ColorMath.LuminanceRange(
        pixels, 2, 1, 0d, 0d, 1d, 1d, box);
    var result = ColorMath.Readable(new(90, 90, 90, 20), null, new(128, 128, 128), Range);
    Assert.Equal(255, result.Text.A);
    Assert.True(result.Background.HasValue);
    Assert.True(ColorMath.WorstContrast(ColorMath.RelativeLuminance(result.Text),
        Range(result.Background)) >= 4.5d);
  }

  [Fact]
  public void LuminanceRangeIgnoresRareOutliers() {
    ColorValue[] pixels = new ColorValue[100];
    for (int i = 0; i < pixels.Length; i++) pixels[i] = i == 0 ? new(255, 255, 255) : new(0, 0, 0);
    var range = ColorMath.LuminanceRange(pixels, 10, 10, 0d, 0d, 1d, 1d);
    Assert.Equal(0d, range.Low);
    Assert.Equal(0d, range.High);
  }

  [Fact]
  public void ReadableTextAddsBoxWhenTheMeanPassesButBusySamplesFail() {
    ColorValue[] pixels = new ColorValue[64];
    for (int y = 0; y < 8; y++) {
      for (int x = 0; x < 8; x++) {
        pixels[y * 8 + x] = (x + y) % 2 == 0 ? new(255, 255, 255) : new(90, 90, 90);
      }
    }
    ColorValue text = new(20, 20, 20);
    double mean = ColorMath.AverageLuminance(pixels, 8, 8, 0d, 0d, 1d, 1d);
    Assert.True(ColorMath.ContrastRatio(ColorMath.RelativeLuminance(text), mean) >= 4.5d);
    (double Low, double High) Range(ColorValue? box) => ColorMath.LuminanceRange(
        pixels, 8, 8, 0d, 0d, 1d, 1d, box);
    Assert.True(ColorMath.WorstContrast(ColorMath.RelativeLuminance(text), Range(null)) < 4.5d);

    var result = ColorMath.Readable(text, null, new(172, 172, 172), Range);
    Assert.True(result.Background.HasValue);
    Assert.True(ColorMath.WorstContrast(ColorMath.RelativeLuminance(result.Text),
        Range(result.Background)) >= 4.5d);
  }

  [Theory]
  [InlineData(200, 90)]
  [InlineData(128, 128)]
  [InlineData(30, 60)]
  public void ReadableTextOnUniformSamplesMatchesTheSolidColorResult(byte background, byte text) {
    ColorValue color = new(background, background, background);
    ColorValue[] pixels = new ColorValue[16];
    for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
    var sampled = ColorMath.Readable(new(text, text, text), null, color,
        box => ColorMath.LuminanceRange(pixels, 4, 4, 0d, 0d, 1d, 1d, box));
    var solid = ColorMath.Readable(new(text, text, text), null, color);
    Assert.Equal(solid.Text.ToString(), sampled.Text.ToString());
    Assert.Equal(solid.Background?.ToString(), sampled.Background?.ToString());
  }

  [Fact]
  public void ReadableTextOnUniformBackgroundOnlyShiftsLightness() {
    var result = ColorMath.Readable(new(90, 90, 90), null, new(200, 200, 200));
    Assert.False(result.Background.HasValue);
    Assert.True(ColorMath.ContrastRatio(ColorMath.RelativeLuminance(result.Text),
        ColorMath.RelativeLuminance(new(200, 200, 200))) >= 4.5d);
  }

  [Fact]
  public void LightnessAdjustmentRetainsHueAndReachesAa() {
    ColorValue original = new(100, 140, 220);
    ColorValue adjusted = ColorMath.AdjustLightness(original, ColorMath.RelativeLuminance(new(255, 255, 255)));
    Assert.True(adjusted.B > adjusted.G && adjusted.G > adjusted.R);
    Assert.True(ColorMath.ContrastRatio(ColorMath.RelativeLuminance(adjusted), 1d) >= 4.5d);
  }

  [Fact]
  public void ReadableTextStrengthensBoxWhenHueCannotReachAa() {
    ColorValue image = new(128, 128, 128);
    ColorValue original = new(128, 128, 128);
    var result = ColorMath.Readable(original, new ColorValue(128, 128, 128, 16), image);
    Assert.True(result.Background.HasValue);
    Assert.True(result.Background.Value.A > 16);
    ColorValue effective = ColorMath.Blend(result.Background.Value, image);
    Assert.True(ColorMath.ContrastRatio(
        ColorMath.RelativeLuminance(result.Text), ColorMath.RelativeLuminance(effective)) >= 4.5d);
  }
}
