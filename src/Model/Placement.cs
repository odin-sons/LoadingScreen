namespace LoadingScreen.Model;

using System;

public enum FitMode {
  Cover,
  Contain,
  Stretch,
  None,
  Box
}

public enum Anchor {
  Center,
  Left,
  Right,
  Top,
  Bottom,
  TopLeft,
  TopRight,
  BottomLeft,
  BottomRight
}

[Flags]
public enum Sides {
  None = 0,
  Left = 1,
  Right = 2,
  Top = 4,
  Bottom = 8,
  All = Left | Right | Top | Bottom
}

public enum FadeCurve {
  Linear,
  Smooth
}

public sealed class Fit {
  public FitMode Mode { get; set; }
  public Length? Width { get; set; }
  public Length? Height { get; set; }
}

public sealed class Fade {
  public Sides Sides { get; set; }
  public ColorValue? Color { get; set; }
  public Length? Size { get; set; }
  public FadeCurve? Curve { get; set; }
}

public class Placement {
  public Fit Fit { get; set; }
  public Anchor? Anchor { get; set; }
  public Length? X { get; set; }
  public Length? Y { get; set; }
  public float? Opacity { get; set; }
  public Fade Fade { get; set; }

  public bool IsEmpty =>
      Fit == null && Anchor == null && X == null && Y == null && Opacity == null && Fade == null;

  public static Placement Merge(params Placement[] mostSpecificFirst) {
    Placement result = new();

    foreach (Placement placement in mostSpecificFirst) {
      if (placement == null) {
        continue;
      }

      result.Fit ??= placement.Fit;
      result.Anchor ??= placement.Anchor;
      result.X ??= placement.X;
      result.Y ??= placement.Y;
      result.Opacity ??= placement.Opacity;
      result.Fade ??= placement.Fade;
    }

    return result;
  }
}
