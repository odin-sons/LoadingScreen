namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public sealed class ProgressTests {
  [Theory]
  [InlineData(-1f, 0f)]
  [InlineData(0.42f, 0.42f)]
  [InlineData(2f, 1f)]
  public void SceneUsesRealOperationFraction(float operation, float expected) {
    Assert.Equal(expected, ProgressMath.Scene(operation).Fraction);
  }

  [Fact]
  public void AreaWaitsForZoneThenCountsRequiredInstances() {
    Assert.Equal(0f, ProgressMath.Area(false, 0, 0).Fraction);
    Assert.Equal(0.75f, ProgressMath.Area(true, 2, 4).Fraction);
    ProgressValue ready = ProgressMath.Area(true, 4, 4);
    Assert.Equal(1f, ready.Fraction);
    Assert.True(ready.Pulse);
    Assert.Equal(1f, ProgressMath.Area(true, 0, 0).Fraction);
  }

  [Fact]
  public void SleepUsesElapsedGameTimeTowardMorning() {
    Assert.Equal(0.5f, ProgressMath.Sleep(900, 1050, 1200).Fraction);
    Assert.Equal(1f, ProgressMath.Sleep(900, 1300, 1200).Fraction);
    Assert.Equal(0f, ProgressMath.Sleep(900, 900, 1200).Fraction);
  }

  [Theory]
  [InlineData(ProgressStage.Connecting, "connectingStage")]
  [InlineData(ProgressStage.Password, "passwordStage")]
  [InlineData(ProgressStage.ReceivingWorld, "receivingWorldStage")]
  public void ConnectionStagesHaveNoInventedPercentage(ProgressStage stage, string key) {
    ProgressValue value = ProgressMath.Connection(stage);
    Assert.Null(value.Fraction);
    Assert.Equal(key, value.StageKey);
  }

  [Theory]
  [InlineData("bar", ProgressStyle.Bar)]
  [InlineData("ring", ProgressStyle.Ring)]
  [InlineData("percent", ProgressStyle.Percent)]
  public void ParsesProgressLayerWithColorSizeAndPlacement(string style, ProgressStyle expected) {
    string yaml = "layouts: { test: { layers: [ { progress: " + style
        + ", color: '#80A0C0', size: 24, place: { fit: { width: 240px }, position: { anchor: bottom } } } ] } }";
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    Layer layer = Assert.Single(result.Config.Layouts["test"].Layers);
    Assert.Equal(LayerKind.Progress, layer.Kind);
    Assert.Equal(expected, layer.ProgressStyle);
    Assert.Equal(new ColorValue(128, 160, 192), layer.Color);
    Assert.Equal(24f, layer.ProgressSize);
    Assert.Equal(Anchor.Bottom, layer.Placement.Anchor);
    Assert.DoesNotContain(result.Warnings, warning => warning.Contains("progress"));
  }

  [Fact]
  public void RejectsUnknownProgressStyleAndMixedContent() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(
        "layouts: { test: { layers: [ { progress: wheel }, { progress: bar, spinner: ring } ] } }");
    Assert.Empty(result.Config.Layouts);
    Assert.NotEmpty(result.Warnings);
  }

  [Fact]
  public void ConnectionStageNamesUseTranslationFallbackAndOverrides() {
    UiTranslations translations = UiTranslations.Parse("German: { connectingStage: Verbinden }");
    Assert.Equal("Verbinden", translations.Get("German", "connectingStage"));
    Assert.Equal("Password", translations.Get("German", "passwordStage"));
    Assert.Equal("Receiving the world", translations.Get("Unknown", "receivingWorldStage"));
  }
}
