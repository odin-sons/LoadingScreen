namespace LoadingScreen.Model;

using System;

public sealed class VignetteSettings {
  [SchemaField("color", "Vignette color.")]
  public ColorValue? Color { get; set; }
  [SchemaField("size", "Outer size in percent.", Form = "percent", Minimum = 0, Maximum = 100)]
  public float? Size { get; set; }
  [SchemaField("strength", "Vignette strength.", Minimum = 0, Maximum = 1)]
  public float? Strength { get; set; }
}

public sealed class MaskSettings {
  [SchemaField("color", "Mask color.")]
  public ColorValue? Color { get; set; }
  [SchemaField("opacity", "Mask opacity.", Minimum = 0, Maximum = 1)]
  public float? Opacity { get; set; }
}

public static class ScreenEffects {
  public static VignetteSettings ResolveVignette(VignetteSettings defaults, VignetteSettings layout) {
    if (defaults == null && layout == null) return null;
    return new() {
      Color = layout?.Color ?? defaults?.Color ?? new ColorValue(0, 0, 0),
      Size = layout?.Size ?? defaults?.Size ?? 25f,
      Strength = layout?.Strength ?? defaults?.Strength ?? 0.5f,
    };
  }

  public static MaskSettings ResolveMask(MaskSettings defaults, MaskSettings layout) {
    if (defaults == null && layout == null) return null;
    return new() {
      Color = layout?.Color ?? defaults?.Color ?? new ColorValue(0, 0, 0),
      Opacity = layout?.Opacity ?? defaults?.Opacity ?? 0.25f,
    };
  }

  public static float VignetteAlpha(float x, float y, float size, float strength) {
    float radius = (float) Math.Sqrt((x * x) + (y * y));
    float width = size / 100f;
    if (width <= 0f) return radius >= 1f ? strength : 0f;
    float t = Math.Max(0f, Math.Min(1f, (radius - (1f - width)) / width));
    return strength * t * t * (3f - (2f * t));
  }
}
