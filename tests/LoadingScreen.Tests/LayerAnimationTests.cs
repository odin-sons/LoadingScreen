namespace LoadingScreen.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using LoadingScreen.Model;
using Xunit;

public class LayerAnimationTests {
  static readonly AnimationCatalog Catalog = new();

  public static IEnumerable<object[]> IdlePresets() =>
      Catalog.Presets.Keys.Where(name => !name.StartsWith("fly-", StringComparison.Ordinal))
          .Select(name => new object[] { name });

  public static IEnumerable<object[]> CoverGeometries() {
    yield return [new LayerGeometry(1920f, 1080f, 1920f, 1080f)];
    yield return [new LayerGeometry(1920f, 1080f, 1920f, 1440f)];
    yield return [new LayerGeometry(1920f, 1080f, 2520f, 1080f)];
    yield return [new LayerGeometry(1280f, 1024f, 1820f, 1024f)];
  }

  public static IEnumerable<object[]> IdleOnCover() =>
      from preset in IdlePresets()
      from geometry in CoverGeometries()
      select new[] { preset[0], geometry[0] };

  [Theory]
  [MemberData(nameof(IdleOnCover))]
  public void IdlePresetsNeverExposeScreenEdgeOnCoverLayer(string idle, LayerGeometry geometry) {
    LayerAnimation animation = new(Catalog.Resolve("fade-in"), Catalog.Resolve(idle), Catalog.Resolve("none"),
        keepCovered: true);
    float duration = Math.Max(1f, Catalog.Resolve(idle).EffectiveDuration());
    for (int i = 0; i <= 997; i++) {
      float time = 2.5f * duration * i / 997f;
      AnimationSample sample = animation.SampleIdle(time, geometry);
      Assert.True(LayerAnimation.Covers(sample, geometry), $"{idle} at {time}s: {sample}");
    }
    for (int i = 0; i <= 50; i++) {
      AnimationSample sample = animation.SampleEnter(0.6f * i / 50f, geometry);
      Assert.True(LayerAnimation.Covers(sample, geometry), $"fade-in before {idle}: {sample}");
    }
  }

  [Theory]
  [InlineData("ken-burns", 0f, 0f, 0f, 0f)]
  [InlineData("ken-burns", 120f, -60f, 0f, 0f)]
  [InlineData("sway", 80f, 40f, 0f, 200f)]
  [InlineData("drift", -100f, 50f, -200f, -120f)]
  [InlineData("pan", 0f, 0f, 0f, 541f)]
  public void ForwardTransformedCornersEncloseScreen(string idle, float centerX, float centerY, float pivotX, float pivotY) {
    LayerGeometry geometry = new(1920f, 1080f, 1922f + 2f * Math.Abs(centerX), 1082f + 2f * Math.Abs(centerY),
        centerX, centerY, pivotX, pivotY);
    LayerAnimation animation = new(null, Catalog.Resolve(idle), null, keepCovered: true);
    LayerAnimation uncorrected = new(null, Catalog.Resolve(idle), null);
    float duration = Catalog.Resolve(idle).EffectiveDuration();
    bool exposedWithoutCorrection = false;
    for (int i = 0; i <= 400; i++) {
      float time = duration * i / 400f;
      Assert.True(Encloses(animation.SampleIdle(time, geometry), geometry), $"{idle} at {i}");
      exposedWithoutCorrection |= !Encloses(uncorrected.SampleIdle(time, geometry), geometry);
    }
    Assert.True(exposedWithoutCorrection);
  }

  [Fact]
  public void CoverFactorOnlyAppliesToLayersThatFillTheScreen() {
    LayerGeometry logo = new(1920f, 1080f, 400f, 200f);
    LayerAnimation animation = new(null, Catalog.Resolve("pan"), null, keepCovered: true);
    Assert.Equal(1f, animation.CoverScale(logo));
    Assert.Equal(1f, new LayerAnimation(null, Catalog.Resolve("pan"), null).CoverScale(
        new LayerGeometry(1920f, 1080f, 1920f, 1080f)));
    Assert.Equal(1f, new LayerAnimation(null, Catalog.Resolve("zoom-in"), null, keepCovered: true).CoverScale(
        new LayerGeometry(1920f, 1080f, 1920f, 1080f)));
  }

  [Fact]
  public void CoverFactorIsConstantSoPanDoesNotPulse() {
    LayerGeometry screen = new(1920f, 1080f, 1920f, 1080f);
    LayerAnimation animation = new(null, Catalog.Resolve("pan"), null, keepCovered: true);
    float first = animation.SampleIdle(0f, screen).Scale;
    Assert.InRange(first, 1.06f, 1.07f);
    Assert.Equal(first, animation.SampleIdle(7.5f, screen).Scale);
    Assert.Equal(first, animation.SampleIdle(15f, screen).Scale);
  }

  public static IEnumerable<object[]> EnterThenIdle() =>
      from enter in new[] { "none", "fade-in", "zoom-out", "fly-in-left", "fly-in-right", "fly-in-top", "fly-in-bottom" }
      from idle in IdlePresets()
      select new object[] { enter, idle[0] };

  [Theory]
  [MemberData(nameof(EnterThenIdle))]
  public void EnterEndsOnIdleFirstPose(string enter, string idle) {
    LayerGeometry geometry = new(1920f, 1080f, 1920f, 1440f, 0f, 0f);
    LayerAnimation animation = new(Catalog.Resolve(enter), Catalog.Resolve(idle), null, keepCovered: true);
    float end = Catalog.Resolve(enter).EffectiveDuration();
    AssertClose(animation.SampleIdle(0f, geometry), animation.SampleEnter(end, geometry));
  }

  [Theory]
  [InlineData("none")]
  [InlineData("fly-out-left")]
  [InlineData("fly-out-bottom")]
  public void ExitStartsFromCurrentIdlePose(string exit) {
    LayerGeometry geometry = new(1920f, 1080f, 1920f, 1080f);
    LayerAnimation animation = new(null, Catalog.Resolve("ken-burns"), Catalog.Resolve(exit), keepCovered: true);
    AssertClose(animation.SampleIdle(4.2f, geometry), animation.SampleExit(0f, 4.2f, geometry));
  }

  [Fact]
  public void FlyInStartsFullyOffscreenWithIdlePose() {
    LayerGeometry geometry = new(1920f, 1080f, 1920f, 1080f);
    LayerAnimation animation = new(Catalog.Resolve("fly-in-left"), Catalog.Resolve("ken-burns"), null,
        keepCovered: true);
    AnimationSample start = animation.SampleEnter(0f, geometry);
    Assert.True(start.X + geometry.Width * start.Scale / 2f <= -geometry.ViewportWidth / 2f);
  }

  static bool Encloses(AnimationSample sample, LayerGeometry geometry) {
    double angle = sample.Rotation * Math.PI / 180.0;
    double cos = Math.Cos(angle), sin = Math.Sin(angle);
    double pivotX = geometry.CenterX + geometry.PivotX + sample.X;
    double pivotY = -(geometry.CenterY + geometry.PivotY) + sample.Y;
    (double X, double Y)[] corners = new (double, double)[4];
    for (int i = 0; i < 4; i++) {
      double localX = (i == 0 || i == 3 ? -geometry.Width : geometry.Width) / 2.0 - geometry.PivotX;
      double localY = (i < 2 ? -geometry.Height : geometry.Height) / 2.0 + geometry.PivotY;
      localX *= sample.Scale;
      localY *= sample.Scale;
      corners[i] = (pivotX + localX * cos - localY * sin, pivotY + localX * sin + localY * cos);
    }
    for (int i = 0; i < 4; i++) {
      double screenX = (i % 2 == 0 ? -1 : 1) * geometry.ViewportWidth / 2.0;
      double screenY = (i < 2 ? -1 : 1) * geometry.ViewportHeight / 2.0;
      for (int edge = 0; edge < 4; edge++) {
        (double ax, double ay) = corners[edge];
        (double bx, double by) = corners[(edge + 1) % 4];
        if ((bx - ax) * (screenY - ay) - (by - ay) * (screenX - ax) < -0.5 * Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))) return false;
      }
    }
    return true;
  }

  static void AssertClose(AnimationSample expected, AnimationSample actual) {
    Assert.InRange(actual.Scale, expected.Scale - 1e-4f, expected.Scale + 1e-4f);
    Assert.InRange(actual.Rotation, expected.Rotation - 1e-3f, expected.Rotation + 1e-3f);
    Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
    Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    Assert.InRange(actual.Opacity, expected.Opacity - 1e-4f, expected.Opacity + 1e-4f);
  }
}
