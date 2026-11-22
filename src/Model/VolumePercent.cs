namespace LoadingScreen.Model;

using System;
using System.Globalization;

public static class VolumePercent {
  public const int Min = 0;
  public const int Max = 100;

  public static float ToSlider(int percent) => Clamp(percent) / (float) Max;

  public static int FromSlider(float value) {
    if (float.IsNaN(value)) return Min;
    return Clamp((int) Math.Round(value * Max, MidpointRounding.AwayFromZero));
  }

  public static string Format(float value) => FromSlider(value).ToString(CultureInfo.InvariantCulture) + "%";

  static int Clamp(int percent) => Math.Max(Min, Math.Min(Max, percent));
}
