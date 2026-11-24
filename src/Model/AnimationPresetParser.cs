namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public sealed class AnimationCatalogLoadResult {
  public AnimationCatalog Catalog { get; } = new();
  public List<string> Warnings { get; } = [];
  public bool IsValid { get; internal set; } = true;
}

public static class AnimationPresetParser {
  public static AnimationCatalogLoadResult Parse(string yaml) {
    AnimationCatalogLoadResult result = new();
    if (string.IsNullOrWhiteSpace(yaml)) return result;
    if (yaml.Length > 256 * 1024) {
      result.Warnings.Add(LogContext.Message("Animation presets rejected.", ("file", "animations.yaml"),
          ("bytes", yaml.Length), ("maxBytes", 256 * 1024)));
      result.IsValid = false;
      return result;
    }
    YamlStream stream = new();
    try {
      string structureProblem = YamlGuard.Check(yaml);
      if (structureProblem != null) {
        result.Warnings.Add(LogContext.Message("Animation presets rejected.", ("file", "animations.yaml"),
            ("reason", structureProblem)));
        result.IsValid = false;
        return result;
      }
      stream.Load(new StringReader(yaml));
    } catch (YamlException exception) {
      result.Warnings.Add(LogContext.Message("Animation presets could not be parsed.",
          ("file", "animations.yaml"), ("line", exception.Start.Line), ("error", exception.Message)));
      result.IsValid = false;
      return result;
    }

    if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) {
      result.Warnings.Add(LogContext.Message("Animation presets rejected.", ("file", "animations.yaml"),
          ("key", "presets"), ("expected", "mapping")));
      result.IsValid = false;
      return result;
    }
    YamlNode presetsNode = Child(root, "presets");
    if (presetsNode is not YamlMappingNode presets) {
      result.Warnings.Add(LogContext.Message("Animation presets rejected.", ("file", "animations.yaml"),
          ("key", "presets"), ("expected", "mapping")));
      result.IsValid = false;
      return result;
    }
    foreach (KeyValuePair<YamlNode, YamlNode> pair in presets.Children) {
      string name = Scalar(pair.Key)?.Trim();
      if (string.IsNullOrEmpty(name) || pair.Value is not YamlMappingNode map) {
        Warn(result, pair.Key, "preset needs a name and mapping");
        continue;
      }
      AnimationPreset preset = ParsePreset(result, name, map);
      if (preset != null) result.Catalog.Override(name, preset);
    }
    return result;
  }

  static AnimationPreset ParsePreset(AnimationCatalogLoadResult result, string name, YamlMappingNode map) {
    foreach (YamlNode key in map.Children.Keys) {
      if (!new[] { "duration", "easing", "overshoot", "repeat", "speed", "keyframes" }
          .Contains(Scalar(key)?.ToLowerInvariant())) {
        Warn(result, key, $"preset '{name}': unknown key '{Scalar(key)}'");
      }
    }
    AnimationPreset preset = new();
    if (!TryPositive(Child(map, "duration"), out float duration)) {
      Warn(result, map, $"preset '{name}': duration must be positive seconds");
      return null;
    }
    preset.Duration = duration;
    YamlNode easingNode = Child(map, "easing");
    if (easingNode != null) {
      string easing = Scalar(easingNode)?.Trim().ToLowerInvariant();
      if (!AnimationEasing.IsValid(easing)) {
        Warn(result, easingNode, $"preset '{name}': unknown easing '{easing}'");
        return null;
      }
      preset.Easing = easing;
    }
    YamlNode overshootNode = Child(map, "overshoot");
    if (overshootNode != null) {
      if (!TryFinite(overshootNode, out float overshoot) || overshoot < 0f) {
        Warn(result, overshootNode, $"preset '{name}': overshoot must be nonnegative");
        return null;
      }
      preset.Overshoot = overshoot;
    }
    YamlNode repeatNode = Child(map, "repeat");
    if (repeatNode != null) {
      if (!Enum.TryParse(Scalar(repeatNode), true, out AnimationRepeat repeat)
          || !Enum.IsDefined(typeof(AnimationRepeat), repeat)) {
        Warn(result, repeatNode, $"preset '{name}': repeat must be once, loop or pingpong");
        return null;
      }
      preset.Repeat = repeat;
    }
    YamlNode speedNode = Child(map, "speed");
    if (speedNode != null) {
      if (!TryPositive(speedNode, out float speed)) {
        Warn(result, speedNode, $"preset '{name}': speed must be positive");
        return null;
      }
      preset.Speed = speed;
    }
    if (Child(map, "keyframes") is not YamlSequenceNode frames || frames.Children.Count < 2) {
      Warn(result, map, $"preset '{name}': keyframes need at least two items");
      return null;
    }
    foreach (YamlNode node in frames.Children) {
      if (node is not YamlMappingNode frameMap || !TryFinite(Child(frameMap, "at"), out float at)
          || at < 0f || at > 1f || (preset.Keyframes.Count > 0 && at <= preset.Keyframes[^1].At)) {
        Warn(result, node, $"preset '{name}': keyframe at must increase from 0 to 1");
        return null;
      }
      AnimationKeyframe frame = new() { At = at };
      if (!ParseFrame(result, name, frameMap, frame)) return null;
      preset.Keyframes.Add(frame);
    }
    if (preset.Keyframes[0].At != 0f || preset.Keyframes[^1].At != 1f) {
      Warn(result, map, $"preset '{name}': keyframes must start at 0 and end at 1");
      return null;
    }
    return preset;
  }

  static bool ParseFrame(
      AnimationCatalogLoadResult result, string name, YamlMappingNode map, AnimationKeyframe frame) {
    foreach (YamlNode key in map.Children.Keys) {
      if (!new[] { "at", "scale", "rotation", "x", "y", "opacity" }
          .Contains(Scalar(key)?.ToLowerInvariant())) {
        Warn(result, key, $"preset '{name}': unknown keyframe key '{Scalar(key)}'");
      }
    }
    if (!Number("scale", 0f, float.MaxValue, value => frame.Scale = value)
        || !Number("rotation", float.MinValue, float.MaxValue, value => frame.Rotation = value)
        || !Number("opacity", 0f, 1f, value => frame.Opacity = value)
        || !Offset("x", value => frame.X = value)
        || !Offset("y", value => frame.Y = value)) return false;
    return true;

    bool Number(string key, float min, float max, Action<float> assign) {
      YamlNode node = Child(map, key);
      if (node == null) return true;
      if (TryFinite(node, out float value) && value >= min && value <= max) {
        assign(value);
        return true;
      }
      Warn(result, node, $"preset '{name}': invalid {key}");
      return false;
    }

    bool Offset(string key, Action<Length> assign) {
      YamlNode node = Child(map, key);
      if (node == null) return true;
      if (Length.TryParse(Scalar(node), out Length value)) {
        assign(value);
        return true;
      }
      Warn(result, node, $"preset '{name}': invalid {key} offset");
      return false;
    }
  }

  static bool TryPositive(YamlNode node, out float value) =>
      TryFinite(node, out value) && value > 0f;

  static bool TryFinite(YamlNode node, out float value) =>
      float.TryParse(Scalar(node), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
      && !float.IsNaN(value) && !float.IsInfinity(value);

  static string Scalar(YamlNode node) => (node as YamlScalarNode)?.Value;

  static YamlNode Child(YamlMappingNode map, string name) =>
      map.Children.FirstOrDefault(pair => string.Equals(Scalar(pair.Key), name, StringComparison.OrdinalIgnoreCase)).Value;

  static void Warn(AnimationCatalogLoadResult result, YamlNode node, string message) =>
      result.Warnings.Add(LogContext.Message("Animation preset warning.", ("file", "animations.yaml"),
          ("line", node?.Start.Line ?? 0), ("detail", message), ("value", Scalar(node))));
}
