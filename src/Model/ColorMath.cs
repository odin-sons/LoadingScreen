namespace LoadingScreen.Model;

using System;

public static class ColorMath {
  public const double MinimumContrast = 4.5;

  public static double RelativeLuminance(ColorValue color) {
    static double Linear(byte value) {
      double channel = value / 255d;
      return channel <= 0.04045d ? channel / 12.92d : Math.Pow((channel + 0.055d) / 1.055d, 2.4d);
    }

    return 0.2126d * Linear(color.R) + 0.7152d * Linear(color.G) + 0.0722d * Linear(color.B);
  }

  public static double ContrastRatio(double first, double second) {
    return (Math.Max(first, second) + 0.05d) / (Math.Min(first, second) + 0.05d);
  }

  public static ColorValue Blend(ColorValue foreground, ColorValue background) {
    double alpha = foreground.A / 255d;
    return new(
        (byte) Math.Round(foreground.R * alpha + background.R * (1d - alpha)),
        (byte) Math.Round(foreground.G * alpha + background.G * (1d - alpha)),
        (byte) Math.Round(foreground.B * alpha + background.B * (1d - alpha)));
  }

  public static ColorValue AutoText(double backgroundLuminance) {
    return ContrastRatio(1d, backgroundLuminance) >= ContrastRatio(0d, backgroundLuminance)
        ? new ColorValue(255, 255, 255) : new ColorValue(0, 0, 0);
  }

  public static ColorValue AutoBackground(double imageLuminance) {
    return imageLuminance >= 0.179d
        ? new ColorValue(0, 0, 0, 153) : new ColorValue(255, 255, 255, 153);
  }

  public static ColorValue AverageArea(ColorValue[] pixels, int width, int height,
      double left, double bottom, double right, double top) {
    int x0 = Math.Max(0, Math.Min(width - 1, (int) Math.Floor(left * width)));
    int x1 = Math.Max(x0 + 1, Math.Min(width, (int) Math.Ceiling(right * width)));
    int y0 = Math.Max(0, Math.Min(height - 1, (int) Math.Floor(bottom * height)));
    int y1 = Math.Max(y0 + 1, Math.Min(height, (int) Math.Ceiling(top * height)));
    long red = 0, green = 0, blue = 0, alpha = 0;
    for (int y = y0; y < y1; y++) {
      for (int x = x0; x < x1; x++) {
        ColorValue pixel = pixels[y * width + x];
        red += pixel.R;
        green += pixel.G;
        blue += pixel.B;
        alpha += pixel.A;
      }
    }
    int count = (x1 - x0) * (y1 - y0);
    return new((byte) (red / count), (byte) (green / count), (byte) (blue / count), (byte) (alpha / count));
  }

  public static double AverageLuminance(ColorValue[] pixels, int width, int height,
      double left, double bottom, double right, double top, ColorValue? box = null,
      ColorValue? backing = null) {
    int x0 = Math.Max(0, Math.Min(width - 1, (int) Math.Floor(left * width)));
    int x1 = Math.Max(x0 + 1, Math.Min(width, (int) Math.Ceiling(right * width)));
    int y0 = Math.Max(0, Math.Min(height - 1, (int) Math.Floor(bottom * height)));
    int y1 = Math.Max(y0 + 1, Math.Min(height, (int) Math.Ceiling(top * height)));
    double total = 0d;
    for (int y = y0; y < y1; y++) {
      for (int x = x0; x < x1; x++) {
        ColorValue pixel = pixels[y * width + x];
        if (backing.HasValue) pixel = Blend(pixel, backing.Value);
        total += RelativeLuminance(box.HasValue ? Blend(box.Value, pixel) : pixel);
      }
    }
    return total / ((x1 - x0) * (y1 - y0));
  }

  public static ColorValue AdjustLightness(ColorValue color, double backgroundLuminance, double minimumContrast = MinimumContrast) {
    if (ContrastRatio(RelativeLuminance(color), backgroundLuminance) >= minimumContrast) return color;
    ToHsl(color, out double hue, out double saturation, out double lightness);
    ColorValue best = color;
    double nearest = double.MaxValue;
    foreach (double limit in new[] { 0.1d, 0.9d }) {
      ColorValue end = FromHsl(hue, saturation, limit);
      if (ContrastRatio(RelativeLuminance(end), backgroundLuminance) < minimumContrast) continue;
      double low = Math.Min(lightness, limit);
      double high = Math.Max(lightness, limit);
      for (int i = 0; i < 20; i++) {
        double middle = (low + high) / 2d;
        bool enough = ContrastRatio(RelativeLuminance(FromHsl(hue, saturation, middle)), backgroundLuminance) >= minimumContrast;
        if (limit < lightness) {
          if (enough) low = middle; else high = middle;
        } else {
          if (enough) high = middle; else low = middle;
        }
      }
      double target = limit < lightness ? low : high;
      if (Math.Abs(target - lightness) >= nearest) continue;
      nearest = Math.Abs(target - lightness);
      best = FromHsl(hue, saturation, target);
    }
    return best;
  }

  public static (ColorValue Text, ColorValue? Background) Readable(
      ColorValue text, ColorValue? box, ColorValue imageBackground,
      Func<ColorValue?, double> effectiveLuminance = null) {
    text = new(text.R, text.G, text.B);
    ColorValue background = box.HasValue ? Blend(box.Value, imageBackground) : imageBackground;
    double luminance = effectiveLuminance?.Invoke(box) ?? RelativeLuminance(background);
    ColorValue adjusted = AdjustLightness(text, luminance);
    if (ContrastRatio(RelativeLuminance(adjusted), luminance) >= MinimumContrast) {
      return (adjusted, box);
    }

    foreach (ColorValue boxColor in new[] {
        box ?? AutoBackground(RelativeLuminance(text)),
        new ColorValue(0, 0, 0, box?.A ?? 0),
        new ColorValue(255, 255, 255, box?.A ?? 0) }) {
      ColorValue solid = new(boxColor.R, boxColor.G, boxColor.B);
      double solidLuminance = effectiveLuminance?.Invoke(solid)
          ?? RelativeLuminance(Blend(solid, imageBackground));
      if (ContrastRatio(RelativeLuminance(AdjustLightness(text, solidLuminance)), solidLuminance)
          < MinimumContrast) continue;
      int low = boxColor.A, high = 255;
      while (low < high) {
        int middle = (low + high) / 2;
        ColorValue candidateBox = new(boxColor.R, boxColor.G, boxColor.B, (byte) middle);
        double candidateLuminance = effectiveLuminance?.Invoke(candidateBox)
            ?? RelativeLuminance(Blend(candidateBox, imageBackground));
        ColorValue candidateText = AdjustLightness(text, candidateLuminance);
        if (ContrastRatio(RelativeLuminance(candidateText), candidateLuminance) >= MinimumContrast) high = middle;
        else low = middle + 1;
      }
      ColorValue chosenBox = new(boxColor.R, boxColor.G, boxColor.B, (byte) low);
      double chosenLuminance = effectiveLuminance?.Invoke(chosenBox)
          ?? RelativeLuminance(Blend(chosenBox, imageBackground));
      return (AdjustLightness(text, chosenLuminance), chosenBox);
    }

    ColorValue fallback = AutoText(RelativeLuminance(imageBackground));
    ColorValue opposite = fallback.R == 0 ? new(255, 255, 255, 255) : new(0, 0, 0, 255);
    return (fallback, opposite);
  }

  static void ToHsl(ColorValue color, out double hue, out double saturation, out double lightness) {
    double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
    double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
    double delta = max - min;
    lightness = (max + min) / 2d;
    saturation = delta == 0d ? 0d : delta / (1d - Math.Abs(2d * lightness - 1d));
    hue = delta == 0d ? 0d : max == r ? ((g - b) / delta) % 6d : max == g ? (b - r) / delta + 2d : (r - g) / delta + 4d;
    hue = (hue * 60d + 360d) % 360d;
  }

  static ColorValue FromHsl(double hue, double saturation, double lightness) {
    double chroma = (1d - Math.Abs(2d * lightness - 1d)) * saturation;
    double x = chroma * (1d - Math.Abs((hue / 60d) % 2d - 1d));
    double m = lightness - chroma / 2d;
    (double r, double g, double b) = hue < 60d ? (chroma, x, 0d)
        : hue < 120d ? (x, chroma, 0d) : hue < 180d ? (0d, chroma, x)
        : hue < 240d ? (0d, x, chroma) : hue < 300d ? (x, 0d, chroma) : (chroma, 0d, x);
    return new(
        (byte) Math.Round((r + m) * 255d),
        (byte) Math.Round((g + m) * 255d),
        (byte) Math.Round((b + m) * 255d));
  }
}
