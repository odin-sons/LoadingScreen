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
  [SchemaField("width", "Maximum width.", Form = "length", Minimum = 0)]
  public Length? Width { get; set; }
  [SchemaField("height", "Maximum height.", Form = "length", Minimum = 0)]
  public Length? Height { get; set; }
}

public sealed class Fade {
  [SchemaField("sides", "Sides to fade.", Form = "sides")]
  public Sides Sides { get; set; }
  [SchemaField("color", "Fade color.")]
  public ColorValue? Color { get; set; }
  [SchemaField("size", "Fade distance.", Form = "length", Minimum = 0)]
  public Length? Size { get; set; }
  [SchemaField("curve", "Fade curve.", EnumType = typeof(FadeCurve))]
  public FadeCurve? Curve { get; set; }
}

[SchemaField("animate", "Legacy animation toggle.", ValueType = typeof(bool))]
public class Placement {
  [SchemaField("fit", "Image fit mode or dimensions.", Form = "fit")]
  public Fit Fit { get; set; }
  [SchemaField("position", "Anchor or anchored offsets.", Form = "position")]
  public Anchor? Anchor { get; set; }
  [SchemaField("x", "Horizontal position offset.", Form = "length")]
  public Length? X { get; set; }
  [SchemaField("y", "Vertical position offset.", Form = "length")]
  public Length? Y { get; set; }
  [SchemaField("opacity", "Opacity from 0 to 1.", Minimum = 0, Maximum = 1)]
  public float? Opacity { get; set; }
  [SchemaField("fade", "Edge fade.")]
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
