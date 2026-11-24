namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;

public enum AnimationRepeat {
  Once,
  Loop,
  Pingpong
}

public enum OffscreenEdge {
  None,
  Left,
  Right,
  Top,
  Bottom
}

public sealed class AnimationSpec {
  [SchemaField("enter", "Enter animation preset.")]
  public string Enter { get; set; }
  [SchemaField("idle", "Idle animation preset.")]
  public string Idle { get; set; }
  [SchemaField("exit", "Exit animation preset.")]
  public string Exit { get; set; }
  [SchemaField("speed", "Playback speed.", Minimum = 0, ExclusiveMinimum = true)]
  public float? Speed { get; set; }
  [SchemaField("origin", "Animation origin.", EnumType = typeof(Anchor))]
  public Anchor? Origin { get; set; }

  public static AnimationSpec Merge(AnimationSpec specific, AnimationSpec fallback) => new() {
    Enter = specific?.Enter ?? fallback?.Enter ?? "none",
    Idle = specific?.Idle ?? fallback?.Idle ?? "none",
    Exit = specific?.Exit ?? fallback?.Exit ?? "none",
    Speed = specific?.Speed ?? fallback?.Speed ?? 1f,
    Origin = specific?.Origin ?? fallback?.Origin ?? Anchor.Center,
  };
}

public sealed class AnimationKeyframe {
  public float At { get; set; }
  public float Scale { get; set; } = 1f;
  public float Rotation { get; set; }
  public Length X { get; set; }
  public Length Y { get; set; }
  public float Opacity { get; set; } = 1f;
  public OffscreenEdge Offscreen { get; set; }
}

public readonly record struct AnimationSample(float Scale, float Rotation, float X, float Y, float Opacity) {
  public static AnimationSample Identity => new(1f, 0f, 0f, 0f, 1f);
}

public sealed class AnimationPreset {
  public float Duration { get; set; } = 1f;
  public string Easing { get; set; } = "linear";
  public float Overshoot { get; set; } = 1.70158f;
  public AnimationRepeat Repeat { get; set; } = AnimationRepeat.Once;
  public float Speed { get; set; } = 1f;
  public List<AnimationKeyframe> Keyframes { get; } = [];

  public float EffectiveDuration(float layerSpeed = 1f) =>
      Duration / (Speed * layerSpeed);

  public AnimationSample Sample(
      float elapsed, float viewportWidth, float viewportHeight, float layerWidth = 0f,
      float layerHeight = 0f, float centerX = 0f, float centerY = 0f,
      float layerSpeed = 1f, bool playOnce = false) {
    if (Keyframes.Count == 0) return AnimationSample.Identity;
    float duration = EffectiveDuration(layerSpeed);
    float progress = duration <= 0f ? 1f : Math.Max(0f, elapsed) / duration;
    if (!playOnce && Repeat == AnimationRepeat.Loop && progress > 1f) progress %= 1f;
    else if (!playOnce && Repeat == AnimationRepeat.Pingpong) {
      progress %= 2f;
      if (progress > 1f) progress = 2f - progress;
    } else progress = Math.Min(1f, progress);

    AnimationKeyframe first = Keyframes[0];
    if (progress <= first.At) return Resolve(first);
    for (int i = 1; i < Keyframes.Count; i++) {
      AnimationKeyframe next = Keyframes[i];
      if (progress > next.At) continue;
      AnimationKeyframe previous = Keyframes[i - 1];
      float fraction = next.At == previous.At ? 1f : (progress - previous.At) / (next.At - previous.At);
      float eased = AnimationEasing.Evaluate(Easing, fraction, Overshoot);
      AnimationSample a = Resolve(previous);
      AnimationSample b = Resolve(next);
      return new(
          Lerp(a.Scale, b.Scale, eased), Lerp(a.Rotation, b.Rotation, eased),
          Lerp(a.X, b.X, eased), Lerp(a.Y, b.Y, eased), Lerp(a.Opacity, b.Opacity, eased));
    }
    return Resolve(Keyframes[Keyframes.Count - 1]);

    AnimationSample Resolve(AnimationKeyframe frame) {
      float x = frame.X.Resolve(viewportWidth);
      float y = frame.Y.Resolve(viewportHeight);
      const float margin = 1f;
      switch (frame.Offscreen) {
        case OffscreenEdge.Left: x += -viewportWidth / 2f - layerWidth / 2f - centerX - margin; break;
        case OffscreenEdge.Right: x += viewportWidth / 2f + layerWidth / 2f - centerX + margin; break;
        case OffscreenEdge.Top: y += -viewportHeight / 2f - layerHeight / 2f + centerY - margin; break;
        case OffscreenEdge.Bottom: y += viewportHeight / 2f + layerHeight / 2f + centerY + margin; break;
      }
      return new(frame.Scale, frame.Rotation, x, y, frame.Opacity);
    }
  }

  static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

public static class AnimationEasing {
  public static float Evaluate(string easing, float value, float overshoot = 1.70158f) {
    float t = Math.Max(0f, Math.Min(1f, value));
    if (t == 0f || t == 1f) return t;
    return easing?.Trim().ToLowerInvariant() switch {
      "linear" => t,
      "ease-in" => t * t,
      "ease-out" => 1f - (1f - t) * (1f - t),
      "ease-in-out" => t * t * (3f - 2f * t),
      "back-out" => 1f + (overshoot + 1f) * (float) Math.Pow(t - 1f, 3)
          + overshoot * (t - 1f) * (t - 1f),
      "elastic-out" => (float) (Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * 2 * Math.PI / 3) + 1),
      "bounce-out" => Bounce(t),
      _ => TryBezier(easing, t, out float result) ? result : t,
    };
  }

  public static bool IsValid(string easing) {
    if (easing is "linear" or "ease-in" or "ease-out" or "ease-in-out"
        or "back-out" or "elastic-out" or "bounce-out") return true;
    return ParseBezier(easing, out _, out _, out _, out _);
  }

  static float Bounce(float t) {
    if (t < 1f / 2.75f) return 7.5625f * t * t;
    if (t < 2f / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
    if (t < 2.5f / 2.75f) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
    t -= 2.625f / 2.75f;
    return 7.5625f * t * t + 0.984375f;
  }

  static bool TryBezier(string easing, float x, out float result) {
    result = 0f;
    if (!ParseBezier(easing, out float x1, out float y1, out float x2, out float y2)) return false;
    float low = 0f, high = 1f;
    for (int i = 0; i < 24; i++) {
      float middle = (low + high) / 2f;
      if (Curve(middle, x1, x2) < x) low = middle;
      else high = middle;
    }
    result = Curve((low + high) / 2f, y1, y2);
    return true;
  }

  static float Curve(float t, float a, float b) {
    float reverse = 1f - t;
    return 3f * reverse * reverse * t * a + 3f * reverse * t * t * b + t * t * t;
  }

  static bool ParseBezier(string easing, out float x1, out float y1, out float x2, out float y2) {
    x1 = y1 = x2 = y2 = 0f;
    if (easing == null || !easing.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase)
        || !easing.EndsWith(")", StringComparison.Ordinal)) return false;
    string[] values = easing.Substring(13, easing.Length - 14).Split(',');
    return values.Length == 4 && Try(values[0], out x1) && Try(values[1], out y1)
        && Try(values[2], out x2) && Try(values[3], out y2)
        && x1 >= 0f && x1 <= 1f && x2 >= 0f && x2 <= 1f;
  }

  static bool Try(string text, out float value) =>
      float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
      && !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class AnimationCatalog {
  readonly Dictionary<string, AnimationPreset> _presets = new(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);

  public AnimationCatalog() {
    AddBuiltIns();
  }

  public IReadOnlyDictionary<string, AnimationPreset> Presets => _presets;

  public void Override(string name, AnimationPreset preset) => _presets[name] = preset;

  public AnimationPreset Resolve(string name, Action<string> warn = null) {
    name ??= "none";
    if (_presets.TryGetValue(name, out AnimationPreset preset)) return preset;
    if (_warned.Add(name)) warn?.Invoke(LogContext.Message("Unknown animation preset; using none.",
        ("preset", name), ("available", string.Join(",", _presets.Keys))));
    return _presets["none"];
  }

  void AddBuiltIns() {
    Add("none", 0f, "linear", AnimationRepeat.Once, Frame(0f), Frame(1f));
    Add("zoom-in", 15f, "ease-in-out", AnimationRepeat.Once, Frame(0f), Frame(1f, scale: 1.05f));
    Add("zoom-out", 15f, "ease-in-out", AnimationRepeat.Once, Frame(0f, scale: 1.05f), Frame(1f));
    Add("pan", 15f, "ease-in-out", AnimationRepeat.Pingpong, Frame(0f, x: "-3%"), Frame(1f, x: "3%"));
    Add("ken-burns", 15f, "ease-in-out", AnimationRepeat.Pingpong,
        Frame(0f, scale: 1.02f, x: "-3%", y: "2%"), Frame(1f, scale: 1.08f, x: "3%", y: "-2%"));
    Add("fade-in", 0.6f, "ease-out", AnimationRepeat.Once, Frame(0f, opacity: 0f), Frame(1f));
    Add("pulse", 2f, "ease-in-out", AnimationRepeat.Pingpong, Frame(0f), Frame(1f, scale: 1.06f));
    Add("breathe", 4f, "ease-in-out", AnimationRepeat.Pingpong, Frame(0f), Frame(1f, scale: 1.025f));
    Add("spin", 4f, "linear", AnimationRepeat.Loop, Frame(0f), Frame(1f, rotation: 360f));
    Add("sway", 3f, "ease-in-out", AnimationRepeat.Pingpong, Frame(0f, rotation: -4f), Frame(1f, rotation: 4f));
    Add("drift", 6f, "ease-in-out", AnimationRepeat.Pingpong,
        Frame(0f, x: "-2%", y: "1%"), Frame(1f, x: "2%", y: "-1%"));
    foreach (OffscreenEdge edge in new[] { OffscreenEdge.Left, OffscreenEdge.Right, OffscreenEdge.Top, OffscreenEdge.Bottom }) {
      string suffix = edge.ToString().ToLowerInvariant();
      Add("fly-in-" + suffix, 0.8f, "back-out", AnimationRepeat.Once,
          Frame(0f, edge: edge), Frame(1f));
      Add("fly-out-" + suffix, 0.6f, "ease-in", AnimationRepeat.Once,
          Frame(0f), Frame(1f, edge: edge));
    }
  }

  void Add(string name, float duration, string easing, AnimationRepeat repeat, params AnimationKeyframe[] frames) {
    AnimationPreset preset = new() { Duration = duration, Easing = easing, Repeat = repeat };
    preset.Keyframes.AddRange(frames);
    _presets[name] = preset;
  }

  static AnimationKeyframe Frame(
      float at, float scale = 1f, float rotation = 0f, string x = "0px", string y = "0px",
      float opacity = 1f, OffscreenEdge edge = OffscreenEdge.None) {
    Length.TryParse(x, out Length horizontal);
    Length.TryParse(y, out Length vertical);
    return new() { At = at, Scale = scale, Rotation = rotation, X = horizontal,
        Y = vertical, Opacity = opacity, Offscreen = edge };
  }
}
