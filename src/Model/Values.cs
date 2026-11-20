namespace LoadingScreen.Model;

using System;
using System.Globalization;

public enum LengthUnit {
  Pixels,
  Percent
}

public readonly struct Length {
  public float Value { get; }
  public LengthUnit Unit { get; }

  public Length(float value, LengthUnit unit) {
    Value = value;
    Unit = unit;
  }

  public float Resolve(float reference) {
    return Unit == LengthUnit.Percent ? reference * Value / 100f : Value;
  }

  public static bool TryParse(string text, out Length length) {
    length = default;

    if (string.IsNullOrWhiteSpace(text)) {
      return false;
    }

    string number = text.Trim();
    LengthUnit unit = LengthUnit.Pixels;

    if (number.EndsWith("%", StringComparison.Ordinal)) {
      unit = LengthUnit.Percent;
      number = number.Substring(0, number.Length - 1);
    } else if (number.EndsWith("px", StringComparison.OrdinalIgnoreCase)) {
      number = number.Substring(0, number.Length - 2);
    }

    if (!float.TryParse(number.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
        || float.IsNaN(value)
        || float.IsInfinity(value)) {
      return false;
    }

    length = new(value, unit);
    return true;
  }

  public override string ToString() {
    return Value.ToString(CultureInfo.InvariantCulture) + (Unit == LengthUnit.Percent ? "%" : "px");
  }
}

public readonly struct ColorValue {
  public byte R { get; }
  public byte G { get; }
  public byte B { get; }
  public byte A { get; }

  public ColorValue(byte r, byte g, byte b, byte a = 255) {
    R = r;
    G = g;
    B = b;
    A = a;
  }

  public static bool TryParse(string text, out ColorValue color) {
    color = default;

    if (string.IsNullOrWhiteSpace(text)) {
      return false;
    }

    string hex = text.Trim().TrimStart('#');

    if ((hex.Length != 6 && hex.Length != 8)
        || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)) {
      return false;
    }

    if (hex.Length == 6) {
      value = (value << 8) | 0xFF;
    }

    color = new((byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value);
    return true;
  }

  public override string ToString() {
    return $"#{R:X2}{G:X2}{B:X2}{A:X2}";
  }
}
