namespace LoadingScreen.Tests;

using System.Collections.Generic;
using LoadingScreen.Model;
using Xunit;

public class AnimationTests {
  [Theory]
  [InlineData("linear")]
  [InlineData("ease-in")]
  [InlineData("ease-out")]
  [InlineData("ease-in-out")]
  [InlineData("back-out")]
  [InlineData("elastic-out")]
  [InlineData("bounce-out")]
  [InlineData("cubic-bezier(0.25, 0.1, 0.25, 1)")]
  public void EasingCurvesHaveExactEndpoints(string name) {
    Assert.Equal(0f, AnimationEasing.Evaluate(name, 0f));
    Assert.Equal(1f, AnimationEasing.Evaluate(name, 1f));
  }

  [Fact]
  public void CubicBezierSolvesTimeAndRejectsNonMonotoneTimeControlPoints() {
    Assert.InRange(AnimationEasing.Evaluate("cubic-bezier(0, 0, 1, 1)", 0.5f), 0.499f, 0.501f);
    Assert.True(AnimationEasing.Evaluate("cubic-bezier(0.42, 0, 1, 1)", 0.5f) < 0.5f);
    Assert.False(AnimationEasing.IsValid("cubic-bezier(-1, 0, 1, 1)"));
  }

  [Fact]
  public void PingpongPlaysForwardAndBackwardRepeatedly() {
    AnimationPreset preset = new() { Duration = 2f, Repeat = AnimationRepeat.Pingpong };
    preset.Keyframes.Add(new() { At = 0f, Scale = 1f });
    preset.Keyframes.Add(new() { At = 1f, Scale = 2f });

    Assert.Equal(1.5f, preset.Sample(1f, 100f, 100f).Scale);
    Assert.Equal(2f, preset.Sample(2f, 100f, 100f).Scale);
    Assert.Equal(1.5f, preset.Sample(3f, 100f, 100f).Scale);
    Assert.Equal(1f, preset.Sample(4f, 100f, 100f).Scale);
    Assert.Equal(1.5f, preset.Sample(5f, 100f, 100f).Scale);
  }

  [Fact]
  public void BackOutOvershootsAndReturnsToEndpoint() {
    Assert.True(AnimationEasing.Evaluate("back-out", 0.8f, 1.7f) > 1f);
    Assert.Equal(1f, AnimationEasing.Evaluate("back-out", 1f, 1.7f));
    Assert.True(AnimationEasing.Evaluate("back-out", 0.8f, 2.5f)
        > AnimationEasing.Evaluate("back-out", 0.8f, 1.7f));
  }

  [Fact]
  public void KeyframesInterpolateEachPropertyAndResolveOffsetsAgainstViewport() {
    AnimationPreset preset = new() { Duration = 2f };
    preset.Keyframes.Add(new() { At = 0f, Scale = 1f, Rotation = 0f, X = new(0, LengthUnit.Pixels),
        Y = new(0, LengthUnit.Pixels), Opacity = 0f });
    preset.Keyframes.Add(new() { At = 1f, Scale = 2f, Rotation = 90f, X = new(50, LengthUnit.Percent),
        Y = new(20, LengthUnit.Pixels), Opacity = 1f });

    AnimationSample sample = preset.Sample(1f, 800f, 600f);
    Assert.Equal(1.5f, sample.Scale);
    Assert.Equal(45f, sample.Rotation);
    Assert.Equal(200f, sample.X);
    Assert.Equal(10f, sample.Y);
    Assert.Equal(0.5f, sample.Opacity);
  }

  [Fact]
  public void FlyPresetsStartOrEndPastMatchingViewportEdge() {
    AnimationCatalog catalog = new();
    Assert.True(catalog.Resolve("fly-in-left").Sample(0f, 800f, 600f, 200f, 100f).X < -500f);
    Assert.True(catalog.Resolve("fly-out-right").Sample(1f, 800f, 600f, 200f, 100f).X > 500f);
    Assert.Equal("back-out", catalog.Resolve("fly-in-top").Easing);
  }

  [Fact]
  public void BuiltInZoomRetainsFifteenSecondSmoothstep() {
    AnimationPreset zoom = new AnimationCatalog().Resolve("zoom-in");
    Assert.Equal(15f, zoom.Duration);
    Assert.Equal(1f, zoom.Sample(0f, 800f, 600f).Scale);
    Assert.Equal(1.025f, zoom.Sample(7.5f, 800f, 600f).Scale);
    Assert.Equal(1.05f, zoom.Sample(15f, 800f, 600f).Scale);
  }

  [Fact]
  public void ShortAndFullAnimationFormsMergeWithDefaults() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse("""
        defaults:
          animation: { enter: fade-in, exit: fly-out-left, speed: 2 }
        layouts:
          sample:
            layers:
              - image: entry
                animation: zoom-in
              - color: "#FFFFFF"
                animation: { enter: fly-in-left, idle: breathe, exit: fly-out-right, speed: 1.5 }
        """);

    Assert.Empty(result.Warnings);
    AnimationSpec shortForm = AnimationSpec.Merge(result.Config.Layouts["sample"].Layers[0].Animation,
        result.Config.Defaults.Animation);
    Assert.Equal("fade-in", shortForm.Enter);
    Assert.Equal("zoom-in", shortForm.Idle);
    Assert.Equal("fly-out-left", shortForm.Exit);
    Assert.Equal(2f, shortForm.Speed);
    AnimationSpec full = result.Config.Layouts["sample"].Layers[1].Animation;
    Assert.Equal("fly-in-left", full.Enter);
    Assert.Equal("breathe", full.Idle);
    Assert.Equal("fly-out-right", full.Exit);
    Assert.Equal(1.5f, full.Speed);
  }

  [Fact]
  public void CustomPresetsOverrideBuiltInsAndUnknownNamesWarnOnce() {
    AnimationCatalogLoadResult result = AnimationPresetParser.Parse("""
        presets:
          zoom-in:
            duration: 3
            easing: cubic-bezier(0.42, 0, 0.58, 1)
            repeat: pingpong
            speed: 2
            keyframes:
              - { at: 0, scale: 1, x: 0px, opacity: 1 }
              - { at: 1, scale: 1.2, x: 10%, opacity: 0.5 }
        """);

    Assert.True(result.IsValid);
    Assert.Empty(result.Warnings);
    AnimationPreset zoom = result.Catalog.Resolve("zoom-in");
    Assert.Equal(3f, zoom.Duration);
    Assert.Equal(1.2f, zoom.Sample(1.5f, 800f, 600f).Scale);
    Assert.Equal(80f, zoom.Sample(1.5f, 800f, 600f).X);
    List<string> warnings = [];
    AnimationPreset fallback = result.Catalog.Resolve("missing", warnings.Add);
    Assert.Same(result.Catalog.Resolve("none"), fallback);
    result.Catalog.Resolve("missing", warnings.Add);
    Assert.Single(warnings);
  }

  [Fact]
  public void CustomPresetsRejectAliasesWithoutReplacingBuiltIns() {
    AnimationCatalogLoadResult result = AnimationPresetParser.Parse("""
        presets:
          first: &shared { duration: 1, keyframes: [ { at: 0 }, { at: 1 } ] }
          second: *shared
        """);

    Assert.False(result.IsValid);
    Assert.Contains(result.Warnings, warning => warning.Contains("aliases"));
    Assert.Equal(15f, result.Catalog.Resolve("zoom-in").Duration);
  }
}
