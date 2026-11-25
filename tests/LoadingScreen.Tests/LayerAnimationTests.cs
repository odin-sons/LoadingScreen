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

  static void AssertClose(AnimationSample expected, AnimationSample actual) {
    Assert.InRange(actual.Scale, expected.Scale - 1e-4f, expected.Scale + 1e-4f);
    Assert.InRange(actual.Rotation, expected.Rotation - 1e-3f, expected.Rotation + 1e-3f);
    Assert.InRange(actual.X, expected.X - 0.01f, expected.X + 0.01f);
    Assert.InRange(actual.Y, expected.Y - 0.01f, expected.Y + 0.01f);
    Assert.InRange(actual.Opacity, expected.Opacity - 1e-4f, expected.Opacity + 1e-4f);
  }
}
