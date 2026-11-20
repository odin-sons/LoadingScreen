namespace LoadingScreen.Model;

using System;

public readonly struct LayerRect {
  public float Width { get; }
  public float Height { get; }
  public float AnchorX { get; }
  public float AnchorY { get; }
  public float OffsetX { get; }
  public float OffsetY { get; }

  public LayerRect(float width, float height, float anchorX, float anchorY, float offsetX, float offsetY) {
    Width = width;
    Height = height;
    AnchorX = anchorX;
    AnchorY = anchorY;
    OffsetX = offsetX;
    OffsetY = offsetY;
  }
}

public static class PlacementSolver {
  public static readonly Length DefaultFadeSize = new(15f, LengthUnit.Percent);

  public static LayerRect Solve(
      Placement placement, float parentWidth, float parentHeight, float imageWidth, float imageHeight) {
    placement ??= new();
    imageWidth = Math.Max(imageWidth, 1f);
    imageHeight = Math.Max(imageHeight, 1f);

    (float width, float height) = Size(placement.Fit, parentWidth, parentHeight, imageWidth, imageHeight);
    (float anchorX, float anchorY) = AnchorPoint(placement.Anchor ?? Anchor.Center);

    float offsetX = placement.X?.Resolve(parentWidth) ?? 0f;
    float offsetY = -(placement.Y?.Resolve(parentHeight) ?? 0f);

    return new(width, height, anchorX, anchorY, offsetX, offsetY);
  }

  public static float FadeSize(Length? size, float dimension) {
    return Math.Max(0f, (size ?? DefaultFadeSize).Resolve(dimension));
  }

  static (float, float) Size(Fit fit, float parentWidth, float parentHeight, float imageWidth, float imageHeight) {
    switch (fit?.Mode ?? FitMode.Contain) {
      case FitMode.Cover:
        return Scale(Math.Max(parentWidth / imageWidth, parentHeight / imageHeight), imageWidth, imageHeight);

      case FitMode.Stretch:
        return (parentWidth, parentHeight);

      case FitMode.None:
        return (imageWidth, imageHeight);

      case FitMode.Box:
        float? boxWidth = fit.Width?.Resolve(parentWidth);
        float? boxHeight = fit.Height?.Resolve(parentHeight);

        if (boxWidth.HasValue && boxHeight.HasValue) {
          return Scale(Math.Min(boxWidth.Value / imageWidth, boxHeight.Value / imageHeight), imageWidth, imageHeight);
        }

        if (boxWidth.HasValue) {
          return Scale(boxWidth.Value / imageWidth, imageWidth, imageHeight);
        }

        if (boxHeight.HasValue) {
          return Scale(boxHeight.Value / imageHeight, imageWidth, imageHeight);
        }

        return (imageWidth, imageHeight);

      default:
        return Scale(Math.Min(parentWidth / imageWidth, parentHeight / imageHeight), imageWidth, imageHeight);
    }
  }

  static (float, float) Scale(float scale, float width, float height) {
    return (width * scale, height * scale);
  }

  static (float, float) AnchorPoint(Anchor anchor) {
    return anchor switch {
      Anchor.Left => (0f, 0.5f),
      Anchor.Right => (1f, 0.5f),
      Anchor.Top => (0.5f, 1f),
      Anchor.Bottom => (0.5f, 0f),
      Anchor.TopLeft => (0f, 1f),
      Anchor.TopRight => (1f, 1f),
      Anchor.BottomLeft => (0f, 0f),
      Anchor.BottomRight => (1f, 0f),
      _ => (0.5f, 0.5f),
    };
  }
}
