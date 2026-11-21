namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class ScreenEffectsTests {
  [Fact]
  public void ParsesGlobalAndLayoutEffectsWithPropertyFallback() {
    const string yaml = """
        defaults:
          vignette: { color: "#102030", size: 30%, strength: 0.6 }
          mask: { color: "#405060", opacity: 0.3 }
        layouts:
          night:
            vignette: { strength: 0.9 }
            mask: { color: "#708090" }
            layers: [ { image: entry } ]
        """;
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    Layout layout = result.Config.Layouts["night"];
    VignetteSettings vignette = ScreenEffects.ResolveVignette(result.Config.Defaults.Vignette, layout.Vignette);
    MaskSettings mask = ScreenEffects.ResolveMask(result.Config.Defaults.Mask, layout.Mask);
    Assert.Empty(result.Warnings);
    Assert.Equal(new ColorValue(0x10, 0x20, 0x30), vignette.Color);
    Assert.Equal(30f, vignette.Size);
    Assert.Equal(0.9f, vignette.Strength);
    Assert.Equal(new ColorValue(0x70, 0x80, 0x90), mask.Color);
    Assert.Equal(0.3f, mask.Opacity);
  }

  [Fact]
  public void EffectsAreOffUnlessConfigured() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse("layouts: { plain: { layers: [ { image: entry } ] } }");
    Assert.Null(ScreenEffects.ResolveVignette(result.Config.Defaults.Vignette, result.Config.Layouts["plain"].Vignette));
    Assert.Null(ScreenEffects.ResolveMask(result.Config.Defaults.Mask, result.Config.Layouts["plain"].Mask));
  }

  [Fact]
  public void LayoutCanDisableInheritedEffects() {
    const string yaml = """
        defaults:
          vignette: { strength: 0.8 }
          mask: { opacity: 0.4 }
        layouts:
          plain:
            vignette: { strength: 0 }
            mask: { opacity: 0 }
            layers: [ { image: entry } ]
        """;
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    Layout layout = result.Config.Layouts["plain"];

    Assert.Empty(result.Warnings);
    Assert.Equal(0f, ScreenEffects.ResolveVignette(result.Config.Defaults.Vignette, layout.Vignette).Strength);
    Assert.Equal(0f, ScreenEffects.ResolveMask(result.Config.Defaults.Mask, layout.Mask).Opacity);
  }

  [Fact]
  public void InvalidValuesWarnAndUseBuiltInEffectValues() {
    const string yaml = """
        defaults:
          vignette: { color: wrong, size: 110%, strength: .nan, extra: true }
          mask: { color: nope, opacity: -1, extra: true }
        layouts:
          night:
            vignette: []
            mask: false
            layers: [ { image: entry } ]
        """;
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    VignetteSettings vignette = ScreenEffects.ResolveVignette(result.Config.Defaults.Vignette, result.Config.Layouts["night"].Vignette);
    MaskSettings mask = ScreenEffects.ResolveMask(result.Config.Defaults.Mask, result.Config.Layouts["night"].Mask);
    Assert.Equal(new ColorValue(0, 0, 0), vignette.Color);
    Assert.Equal(25f, vignette.Size);
    Assert.Equal(0.5f, vignette.Strength);
    Assert.Equal(new ColorValue(0, 0, 0), mask.Color);
    Assert.Equal(0.25f, mask.Opacity);
    Assert.Equal(9, result.Warnings.Count);
    Assert.All(result.Warnings, warning => Assert.Contains("line ", warning));
  }

  [Theory]
  [InlineData(0f, 0f, 25f, 0.8f, 0f)]
  [InlineData(0.75f, 0f, 25f, 0.8f, 0f)]
  [InlineData(1f, 0f, 25f, 0.8f, 0.8f)]
  [InlineData(1f, 1f, 25f, 0.8f, 0.8f)]
  public void VignetteAlphaRisesFromCenterToScreenEdges(float x, float y, float size, float strength, float expected) {
    Assert.Equal(expected, ScreenEffects.VignetteAlpha(x, y, size, strength), 3);
  }
}
