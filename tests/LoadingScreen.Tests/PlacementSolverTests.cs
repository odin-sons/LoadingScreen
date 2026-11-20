namespace LoadingScreen.Tests;

using LoadingScreen.Screens;

using Xunit;

public class PlacementSolverTests {
  const float ParentWidth = 1920f;
  const float ParentHeight = 1080f;

  static LayerRect Solve(Placement placement, float imageWidth = 1000f, float imageHeight = 1000f) {
    return PlacementSolver.Solve(placement, ParentWidth, ParentHeight, imageWidth, imageHeight);
  }

  static Placement WithFit(FitMode mode) => new() { Fit = new Fit { Mode = mode } };

  [Fact]
  public void DefaultIsContainCentered() {
    LayerRect rect = Solve(new Placement());

    Assert.Equal(1080f, rect.Width);
    Assert.Equal(1080f, rect.Height);
    Assert.Equal(0.5f, rect.AnchorX);
    Assert.Equal(0.5f, rect.AnchorY);
    Assert.Equal(0f, rect.OffsetX);
  }

  [Fact]
  public void CoverFillsAndCrops() {
    LayerRect rect = Solve(WithFit(FitMode.Cover));

    Assert.Equal(1920f, rect.Width);
    Assert.Equal(1920f, rect.Height);
  }

  [Fact]
  public void StretchAndNone() {
    Assert.Equal(1920f, Solve(WithFit(FitMode.Stretch)).Width);
    Assert.Equal(1080f, Solve(WithFit(FitMode.Stretch)).Height);
    Assert.Equal(500f, Solve(WithFit(FitMode.None), 500f, 200f).Width);
  }

  [Fact]
  public void BoxWithOneAxisKeepsAspect() {
    Placement placement = new() { Fit = new Fit { Mode = FitMode.Box, Width = new Length(50, LengthUnit.Percent) } };
    LayerRect rect = Solve(placement, 400f, 200f);

    Assert.Equal(960f, rect.Width, 0.01f);
    Assert.Equal(480f, rect.Height, 0.01f);
  }

  [Fact]
  public void BoxWithBothAxesContainsImage() {
    Placement placement = new() {
      Fit = new Fit { Mode = FitMode.Box, Width = new Length(800, LengthUnit.Pixels), Height = new Length(200, LengthUnit.Pixels) }
    };
    LayerRect rect = Solve(placement, 400f, 200f);

    Assert.Equal(400f, rect.Width);
    Assert.Equal(200f, rect.Height);
  }

  [Fact]
  public void AnchorAndOffsetsUseScreenDirections() {
    Placement placement = new() {
      Anchor = Anchor.TopRight,
      X = new Length(-5, LengthUnit.Percent),
      Y = new Length(10, LengthUnit.Pixels),
    };
    LayerRect rect = Solve(placement);

    Assert.Equal(1f, rect.AnchorX);
    Assert.Equal(1f, rect.AnchorY);
    Assert.Equal(-96f, rect.OffsetX);
    Assert.Equal(-10f, rect.OffsetY);
  }

  [Fact]
  public void FadeSizeDefaultsToFifteenPercent() {
    Assert.Equal(150f, PlacementSolver.FadeSize(null, 1000f));
    Assert.Equal(40f, PlacementSolver.FadeSize(new Length(40, LengthUnit.Pixels), 1000f));
  }
}
