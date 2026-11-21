namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

public sealed class ConfigLoadResult {
  public ScreenConfig Config { get; } = new();
  public List<string> Warnings { get; } = [];
  public bool IsValid { get; set; } = true;
}

public sealed class ConfigParser {
  const int MaxNestingDepth = 32;

  static readonly string[] _placementKeys = ["fit", "position", "opacity", "fade", "animate"];
  static readonly string[] _defaultKeys = [
    .. _placementKeys, "background", "tint", "zoom", "tip", "separator", "spinner"
  ];
  static readonly string[] _layerKeys = [
    "id", "color", "image", "images", "source", "separator", "text", "size", "align", "background", .. _placementKeys
  ];
  static readonly string[] _layoutKeys = ["layers", .. _placementKeys];
  static readonly string[] _entryKeys = ["image", "text", "layout", "place", "when"];

  readonly Func<string, bool> _imageExists;
  readonly IEnumerable<string> _knownCauses;

  ConfigLoadResult _result;

  public ConfigParser(Func<string, bool> imageExists, IEnumerable<string> knownCauses = null) {
    _imageExists = imageExists ?? (_ => true);
    _knownCauses = knownCauses;
  }

  public ConfigLoadResult Parse(string yaml) {
    _result = new();

    if (string.IsNullOrWhiteSpace(yaml)) {
      return _result;
    }

    YamlStream stream = new();

    try {
      string structureProblem = CheckStructure(yaml);

      if (structureProblem != null) {
        Warn(0, $"{structureProblem}, config ignored");
        _result.IsValid = false;
        return _result;
      }

      stream.Load(new StringReader(yaml));
    } catch (YamlException exception) {
      Warn(exception.Start.Line, $"invalid YAML, config ignored: {exception.Message}");
      _result.IsValid = false;
      return _result;
    }

    if (stream.Documents.Count == 0 || IsNull(stream.Documents[0].RootNode)) {
      return _result;
    }

    if (stream.Documents[0].RootNode is not YamlMappingNode root) {
      Warn(stream.Documents[0].RootNode, "the top level must be a mapping, config ignored");
      _result.IsValid = false;
      return _result;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in root.Children) {
      switch (Key(pair.Key)) {
        case "defaults":
          _result.Config.Defaults = ParseDefaults(pair.Value);
          break;

        case "layouts":
          ParseLayouts(pair.Value);
          break;

        case "order":
          if (TryParseOrder(pair.Value, "order", out PickOrder order)) {
            _result.Config.Order = order;
          }

          break;

        case "situations":
          ParseSituations(pair.Value);
          break;

        default:
          Warn(pair.Key, $"unknown top-level key '{Name(pair.Key)}'");
          break;
      }
    }

    ValidateLayoutReferences();
    return _result;
  }

  static string CheckStructure(string yaml) {
    Parser parser = new(new StringReader(yaml));
    int depth = 0;

    while (parser.MoveNext()) {
      switch (parser.Current) {
        case AnchorAlias:
          return "YAML aliases (*name) are not supported";

        case MappingStart or SequenceStart:
          if (++depth > MaxNestingDepth) {
            return $"YAML is nested deeper than {MaxNestingDepth} levels";
          }

          break;

        case MappingEnd or SequenceEnd:
          depth--;
          break;
      }
    }

    return null;
  }

  void ParseLayouts(YamlNode node) {
    if (IsNull(node)) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, "'layouts' must be a mapping of layout names");
      return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      string name = Name(pair.Key);

      if (_result.Config.Layouts.ContainsKey(name)) {
        Warn(pair.Key, $"duplicate layout '{name}', ignored");
        continue;
      }

      Layout layout = ParseLayout(name, pair.Value);

      if (layout != null) {
        _result.Config.Layouts[name] = layout;
      }
    }
  }

  Layout ParseLayout(string name, YamlNode node) {
    YamlNode layersNode = node;
    Layout layout = new() { Name = name };

    if (node is YamlMappingNode mapping) {
      layout.Placement = ParsePlacement(mapping, _layoutKeys, $"layout '{name}'");
      layersNode = Child(mapping, "layers");
    }

    if (layersNode is not YamlSequenceNode layers) {
      Warn(node, $"layout '{name}' needs a 'layers' list, ignored");
      return null;
    }

    foreach (YamlNode layerNode in layers.Children) {
      Layer layer = ParseLayer(layerNode, name);

      if (layer != null) {
        layout.Layers.Add(layer);
      }
    }

    if (layout.Layers.Count == 0) {
      Warn(node, $"layout '{name}' has no valid layers, ignored");
      return null;
    }

    return layout;
  }

  Layer ParseLayer(YamlNode node, string layoutName) {
    string where = $"layout '{layoutName}'";

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: a layer must be a mapping, ignored");
      return null;
    }

    Layer layer = new() {
      Id = Scalar(Child(mapping, "id")),
      Placement = ParsePlacement(mapping, _layerKeys, where),
    };

    YamlNode color = Child(mapping, "color");
    YamlNode image = Child(mapping, "image");
    YamlNode images = Child(mapping, "images");
    YamlNode source = Child(mapping, "source");
    YamlNode separator = Child(mapping, "separator");
    YamlNode text = Child(mapping, "text");

    int imageSources = new[] { image, images, source, separator, text }.Count(child => child != null);

    if (imageSources > 1) {
      Warn(node, $"{where}: use only one of 'image', 'images', 'source', 'separator' and 'text' in a layer, ignored");
      return null;
    }

    if (text != null) {
      return ParseTextLayer(layer, mapping, text, where);
    }

    foreach (string key in new[] { "size", "align", "background" }) {
      if (Child(mapping, key) != null) {
        Warn(Child(mapping, key), $"{where}: '{key}' applies only to text layers");
      }
    }

    if (imageSources == 0) {
      if (color == null) {
        Warn(node, $"{where}: a layer needs 'image', 'images', 'source', 'separator' or 'color', ignored");
        return null;
      }

      if (!ColorValue.TryParse(Scalar(color), out ColorValue value)) {
        Warn(color, $"{where}: invalid color '{Scalar(color)}', use #RRGGBB or #RRGGBBAA; layer ignored");
        return null;
      }

      layer.Kind = LayerKind.Color;
      layer.Color = value;
      return layer;
    }

    layer.Kind = LayerKind.Image;

    if (separator != null) {
      if (!string.Equals(Scalar(separator), "game", StringComparison.OrdinalIgnoreCase)) {
        Warn(separator, $"{where}: 'separator' supports only 'game' (the game's ornament line), layer ignored");
        return null;
      }

      if (color != null) {
        if (!ColorValue.TryParse(Scalar(color), out ColorValue tint)) {
          Warn(color, $"{where}: invalid color '{Scalar(color)}', use #RRGGBB or #RRGGBBAA; tint ignored");
        } else {
          layer.Color = tint;
          layer.HasTint = true;
        }
      }

      layer.GameSeparator = true;
      return layer;
    }

    if (color != null) {
      Warn(color, $"{where}: 'color' is ignored on an image layer");
    }

    if (source != null) {
      if (!string.Equals(Scalar(source), "entry", StringComparison.OrdinalIgnoreCase)) {
        Warn(source, $"{where}: 'source' supports only 'entry', layer ignored");
        return null;
      }

      layer.FromEntry = true;
      return layer;
    }

    IEnumerable<YamlNode> imageNodes = image != null ? [image] : Sequence(images, $"{where}: 'images'");

    foreach (YamlNode imageNode in imageNodes) {
      string path = Scalar(imageNode);

      if (CheckImage(imageNode, path, where)) {
        layer.Images.Add(NormalizePath(path));
      }
    }

    if (layer.Images.Count == 0) {
      Warn(node, $"{where}: a layer has no usable images, ignored");
      return null;
    }

    return layer;
  }

  Layer ParseTextLayer(Layer layer, YamlMappingNode mapping, YamlNode text, string where) {
    if (!string.Equals(Scalar(text), "caption", StringComparison.OrdinalIgnoreCase)) {
      Warn(text, $"{where}: 'text' supports only 'caption' (the entry text or a tip), layer ignored");
      return null;
    }

    layer.Kind = LayerKind.Text;
    layer.Text = new();

    YamlNode size = Child(mapping, "size");

    if (!IsNull(size)) {
      if (float.TryParse(Scalar(size), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
          && value > 0f) {
        layer.Text.FontSize = value;
      } else {
        Warn(size, $"{where}: 'size' must be a positive font size");
      }
    }

    layer.Text.Color = ParseColor(Child(mapping, "color"), where, "color");
    layer.Text.Background = ParseColor(Child(mapping, "background"), where, "background");

    YamlNode align = Child(mapping, "align");

    if (!IsNull(align)) {
      if (Enum.TryParse(Scalar(align), ignoreCase: true, out TextAlign value)
          && Enum.IsDefined(typeof(TextAlign), value)) {
        layer.Text.Align = value;
      } else {
        Warn(align, $"{where}: invalid align '{Scalar(align)}', use left, center or right");
      }
    }

    if (layer.Placement.Fit != null && layer.Placement.Fit.Mode != FitMode.Box) {
      Warn(Child(mapping, "fit"), $"{where}: a text layer supports only 'fit: {{ width }}' (the text width)");
      layer.Placement.Fit = null;
    }

    return layer;
  }

  ColorValue? ParseColor(YamlNode node, string where, string key) {
    if (IsNull(node)) {
      return null;
    }

    if (ColorValue.TryParse(Scalar(node), out ColorValue value)) {
      return value;
    }

    Warn(node, $"{where}: invalid {key} '{Scalar(node)}', use #RRGGBB or #RRGGBBAA");
    return null;
  }

  void ParseSituations(YamlNode node) {
    if (IsNull(node)) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, "'situations' must be a mapping");
      return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      switch (Key(pair.Key)) {
        case "general":
          _result.Config.General = ParseEntryList("general", pair.Value);
          break;

        case "teleport":
          ParseNamedLists(pair.Value, "teleport", _result.Config.Teleport, KnownNames.Biomes);
          break;

        case "death":
          ParseNamedLists(pair.Value, "death", _result.Config.Death, _knownCauses);
          break;

        default:
          Warn(pair.Key, $"unknown situation '{Name(pair.Key)}', use 'general', 'teleport' or 'death'");
          break;
      }
    }
  }

  void ParseNamedLists(
      YamlNode node, string situation, Dictionary<string, EntryList> target, IEnumerable<string> knownNames) {
    if (IsNull(node)) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"'{situation}' must be a mapping of names to entries");
      return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      string name = Name(pair.Key);

      if (situation == "death" && string.Equals(name, DeathCauses.EnemySection, StringComparison.OrdinalIgnoreCase)) {
        ParseEnemyLists(pair.Value, target);
        continue;
      }

      if (knownNames != null) {
        string known = knownNames.FirstOrDefault(
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));

        if (known == null) {
          Warn(pair.Key, $"unknown {situation} name '{name}', known: {string.Join(", ", knownNames)}");
          continue;
        }

        name = known;
      }

      if (target.ContainsKey(name)) {
        Warn(pair.Key, $"duplicate {situation} name '{name}', ignored");
        continue;
      }

      target[name] = ParseEntryList($"{situation}.{name}", pair.Value);
    }
  }

  bool TryParseOrder(YamlNode node, string where, out PickOrder order) {
    string value = Scalar(node);

    if (string.Equals(value, "random", StringComparison.OrdinalIgnoreCase)) {
      order = PickOrder.Random;
      return true;
    }

    if (string.Equals(value, "sequential", StringComparison.OrdinalIgnoreCase)) {
      order = PickOrder.Sequential;
      return true;
    }

    Warn(node, $"{where}: use 'random' or 'sequential', got '{value}'");
    order = default;
    return false;
  }

  void ParseEnemyLists(YamlNode node, Dictionary<string, EntryList> target) {
    if (IsNull(node)) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, "'death.enemy' must be a mapping of creature prefab names to entries");
      return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      string name = Name(pair.Key);
      string key = DeathCauses.EnemyKey(name);

      if (target.ContainsKey(key)) {
        Warn(pair.Key, $"duplicate death.enemy name '{name}', ignored");
        continue;
      }

      target[key] = ParseEntryList($"death.enemy.{name}", pair.Value);
    }
  }

  EntryList ParseEntryList(string name, YamlNode node) {
    EntryList list = new() { Name = name };
    YamlNode entriesNode = node;

    if (node is YamlMappingNode mapping) {
      WarnUnknownKeys(mapping, ["layout", "order", "entries"], name);
      list.LayoutName = Scalar(Child(mapping, "layout"));

      YamlNode orderNode = Child(mapping, "order");

      if (orderNode != null && TryParseOrder(orderNode, $"'{name}' order", out PickOrder order)) {
        list.Order = order;
      }

      entriesNode = Child(mapping, "entries");
    }

    foreach (YamlNode entryNode in Sequence(entriesNode, $"'{name}' entries")) {
      Entry entry = ParseEntry(entryNode, name);

      if (entry != null) {
        list.Entries.Add(entry);
      }
    }

    return list;
  }

  Entry ParseEntry(YamlNode node, string listName) {
    if (node is YamlScalarNode) {
      string path = Scalar(node);
      return CheckImage(node, path, listName) ? new() { Image = NormalizePath(path) } : null;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{listName}: an entry must be an image path or a mapping, ignored");
      return null;
    }

    WarnUnknownKeys(mapping, _entryKeys, listName);

    YamlNode imageNode = Child(mapping, "image");
    string image = Scalar(imageNode);
    string text = Scalar(Child(mapping, "text"));

    if (IsNull(imageNode) && string.IsNullOrEmpty(text)) {
      Warn(node, $"{listName}: an entry needs an 'image' or a 'text', ignored");
      return null;
    }

    if (!IsNull(imageNode) && !CheckImage(imageNode, image, listName)) {
      return null;
    }

    YamlNode place = Child(mapping, "place");

    Entry entry = new() {
      Image = IsNull(imageNode) ? null : NormalizePath(image),
      Text = text,
      LayoutName = Scalar(Child(mapping, "layout")),
      Place = IsNull(place) ? new() : ParsePlacement(place, _placementKeys, $"{listName}: 'place'"),
    };

    YamlNode when = Child(mapping, "when");

    if (!IsNull(when) && !ParseConditions(when, entry.Conditions, $"{listName}: 'when'")) {
      return null;
    }

    return entry;
  }

  bool ParseConditions(YamlNode node, List<Condition> conditions, string where) {
    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: expected a mapping, entry ignored");
      return false;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      if (!Conditions.TryParseKey(Name(pair.Key), out ConditionKind kind, out bool negated)) {
        Warn(pair.Key, $"{where}: unknown condition '{Name(pair.Key)}', entry ignored");
        return false;
      }

      IEnumerable<YamlNode> items = pair.Value is YamlSequenceNode sequence ? sequence.Children : [pair.Value];

      foreach (YamlNode item in items) {
        string name = Scalar(item)?.Trim();

        if (string.IsNullOrEmpty(name)) {
          Warn(item, $"{where}: '{Name(pair.Key)}' needs a name, entry ignored");
          return false;
        }

        if (kind == ConditionKind.KnownBiome) {
          string biome = KnownNames.Biomes.FirstOrDefault(
              known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase));

          if (biome == null) {
            Warn(item, $"{where}: unknown biome '{name}', entry ignored");
            return false;
          }

          name = biome;
        }

        conditions.Add(new() { Kind = kind, Name = name, Negated = negated });
      }
    }

    return true;
  }

  ScreenDefaults ParseDefaults(YamlNode node) {
    ScreenDefaults defaults = new();
    Placement placement = ParsePlacement(node, _defaultKeys, "defaults");
    defaults.Fit = placement.Fit;
    defaults.Anchor = placement.Anchor;
    defaults.X = placement.X;
    defaults.Y = placement.Y;
    defaults.Opacity = placement.Opacity;
    defaults.Fade = placement.Fade;
    defaults.Animate = placement.Animate;

    if (node is not YamlMappingNode mapping) {
      return defaults;
    }

    defaults.Background = ParseDefaultColor(Child(mapping, "background"), "defaults.background", defaults.Background);
    defaults.Tint = ParseDefaultColor(Child(mapping, "tint"), "defaults.tint", defaults.Tint);

    if (DefaultMap(Child(mapping, "zoom"), "zoom", ["enabled", "amount", "seconds"]) is { } zoom) {
      defaults.Zoom.Enabled = ParseDefaultBool(Child(zoom, "enabled"), "zoom.enabled", defaults.Zoom.Enabled);
      defaults.Zoom.Amount = ParseDefaultFloat(Child(zoom, "amount"), "zoom.amount", defaults.Zoom.Amount, 0.5f, 1.5f);
      defaults.Zoom.Seconds = ParseDefaultFloat(Child(zoom, "seconds"), "zoom.seconds", defaults.Zoom.Seconds, float.Epsilon, float.MaxValue);
    }

    if (DefaultMap(Child(mapping, "tip"), "tip", ["x", "bottom", "size", "color"]) is { } tip) {
      defaults.Tip.X = ParseDefaultFloat(Child(tip, "x"), "tip.x", defaults.Tip.X);
      defaults.Tip.Bottom = ParseDefaultFloat(Child(tip, "bottom"), "tip.bottom", defaults.Tip.Bottom);
      defaults.Tip.Size = ParseDefaultInt(Child(tip, "size"), "tip.size", defaults.Tip.Size, 0, 64);
      defaults.Tip.Color = ParseDefaultColor(Child(tip, "color"), "tip.color", defaults.Tip.Color);
    }

    if (DefaultMap(Child(mapping, "separator"), "separator", ["show", "x", "bottom", "onlayouts"]) is { } separator) {
      defaults.Separator.Show = ParseDefaultBool(Child(separator, "show"), "separator.show", defaults.Separator.Show);
      defaults.Separator.X = ParseDefaultFloat(Child(separator, "x"), "separator.x", defaults.Separator.X);
      defaults.Separator.Bottom = ParseDefaultFloat(Child(separator, "bottom"), "separator.bottom", defaults.Separator.Bottom);
      defaults.Separator.OnLayouts = ParseDefaultBool(Child(separator, "onlayouts"), "separator.onLayouts", defaults.Separator.OnLayouts);
    }

    if (DefaultMap(Child(mapping, "spinner"), "spinner", ["center", "x", "bottom"]) is { } spinner) {
      defaults.Spinner.Center = ParseDefaultBool(Child(spinner, "center"), "spinner.center", defaults.Spinner.Center);
      defaults.Spinner.X = ParseDefaultFloat(Child(spinner, "x"), "spinner.x", defaults.Spinner.X);
      defaults.Spinner.Bottom = ParseDefaultFloat(Child(spinner, "bottom"), "spinner.bottom", defaults.Spinner.Bottom);
    }

    return defaults;
  }

  YamlMappingNode DefaultMap(YamlNode node, string key, IReadOnlyCollection<string> allowedKeys) {
    if (node == null) {
      return null;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"defaults.{key}: expected a mapping; built-in values used");
      return null;
    }

    WarnUnknownKeys(mapping, allowedKeys, $"defaults.{key}");
    return mapping;
  }

  ColorValue ParseDefaultColor(YamlNode node, string key, ColorValue fallback) {
    if (node == null) {
      return fallback;
    }

    if (ColorValue.TryParse(Scalar(node), out ColorValue value)) {
      return value;
    }

    Warn(node, $"{key}: invalid color, use #RRGGBB or #RRGGBBAA; built-in value used");
    return fallback;
  }

  bool ParseDefaultBool(YamlNode node, string key, bool fallback) {
    if (node == null) {
      return fallback;
    }

    if (bool.TryParse(Scalar(node), out bool value)) {
      return value;
    }

    Warn(node, $"{key}: must be true or false; built-in value used");
    return fallback;
  }

  float ParseDefaultFloat(YamlNode node, string key, float fallback, float min = float.MinValue, float max = float.MaxValue) {
    if (node == null) {
      return fallback;
    }

    if (float.TryParse(Scalar(node), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
        && !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max) {
      return value;
    }

    Warn(node, $"{key}: invalid number; built-in value used");
    return fallback;
  }

  int ParseDefaultInt(YamlNode node, string key, int fallback, int min, int max) {
    if (node == null) {
      return fallback;
    }

    if (int.TryParse(Scalar(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        && value >= min && value <= max) {
      return value;
    }

    Warn(node, $"{key}: must be an integer from {min} to {max}; built-in value used");
    return fallback;
  }

  Placement ParsePlacement(YamlNode node, IReadOnlyCollection<string> allowedKeys, string where) {
    Placement placement = new();

    if (IsNull(node)) {
      return placement;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: expected a mapping");
      return placement;
    }

    WarnUnknownKeys(mapping, allowedKeys, where);

    placement.Fit = ParseFit(Child(mapping, "fit"), where);
    ParsePosition(Child(mapping, "position"), placement, where);
    placement.Opacity = ParseOpacity(Child(mapping, "opacity"), where);
    placement.Fade = ParseFade(Child(mapping, "fade"), where);
    placement.Animate = ParseBool(Child(mapping, "animate"), where, "animate");

    return placement;
  }

  Fit ParseFit(YamlNode node, string where) {
    if (IsNull(node)) {
      return null;
    }

    if (node is YamlMappingNode mapping) {
      WarnUnknownKeys(mapping, ["width", "height"], $"{where}: 'fit'");

      Fit box = new() {
        Mode = FitMode.Box,
        Width = ParseLength(Child(mapping, "width"), where, "width", allowNegative: false),
        Height = ParseLength(Child(mapping, "height"), where, "height", allowNegative: false),
      };

      if (box.Width == null && box.Height == null) {
        Warn(node, $"{where}: 'fit' needs 'width' and/or 'height'");
        return null;
      }

      return box;
    }

    string text = Scalar(node);

    if (text != null
        && Enum.TryParse(text, ignoreCase: true, out FitMode mode)
        && mode != FitMode.Box
        && Enum.IsDefined(typeof(FitMode), mode)) {
      return new() { Mode = mode };
    }

    Warn(node, $"{where}: invalid fit '{text}', use cover, contain, stretch, none or {{ width, height }}");
    return null;
  }

  void ParsePosition(YamlNode node, Placement placement, string where) {
    if (IsNull(node)) {
      return;
    }

    if (node is YamlScalarNode) {
      placement.Anchor = ParseAnchor(node, where);
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: 'position' must be an anchor name or a mapping");
      return;
    }

    WarnUnknownKeys(mapping, ["anchor", "x", "y"], $"{where}: 'position'");

    placement.Anchor = ParseAnchor(Child(mapping, "anchor"), where);
    placement.X = ParseLength(Child(mapping, "x"), where, "x", allowNegative: true);
    placement.Y = ParseLength(Child(mapping, "y"), where, "y", allowNegative: true);
  }

  Anchor? ParseAnchor(YamlNode node, string where) {
    if (IsNull(node)) {
      return null;
    }

    string text = Scalar(node);
    string compact = text?.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

    if (compact != null && Enum.TryParse(compact, ignoreCase: true, out Anchor anchor)
        && Enum.IsDefined(typeof(Anchor), anchor)) {
      return anchor;
    }

    Warn(node, $"{where}: invalid anchor '{text}', use center, left, right, top, bottom or e.g. top-left");
    return null;
  }

  float? ParseOpacity(YamlNode node, string where) {
    if (IsNull(node)) {
      return null;
    }

    if (!float.TryParse(Scalar(node), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
        || value < 0f
        || value > 1f) {
      Warn(node, $"{where}: 'opacity' must be a number from 0 to 1");
      return null;
    }

    return value;
  }

  Fade ParseFade(YamlNode node, string where) {
    if (IsNull(node)) {
      return null;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: 'fade' must be a mapping");
      return null;
    }

    string fadeWhere = $"{where}: 'fade'";
    WarnUnknownKeys(mapping, ["sides", "color", "size", "curve"], fadeWhere);

    Fade fade = new() { Sides = ParseSides(Child(mapping, "sides"), fadeWhere) };

    if (fade.Sides == Sides.None) {
      Warn(node, $"{fadeWhere} needs 'sides' (left, right, top, bottom or all), ignored");
      return null;
    }

    YamlNode color = Child(mapping, "color");

    if (!IsNull(color)) {
      if (ColorValue.TryParse(Scalar(color), out ColorValue value)) {
        fade.Color = value;
      } else {
        Warn(color, $"{fadeWhere}: invalid color '{Scalar(color)}', use #RRGGBB or #RRGGBBAA");
      }
    }

    fade.Size = ParseLength(Child(mapping, "size"), fadeWhere, "size", allowNegative: false);

    YamlNode curve = Child(mapping, "curve");

    if (!IsNull(curve)) {
      if (Enum.TryParse(Scalar(curve), ignoreCase: true, out FadeCurve value)
          && Enum.IsDefined(typeof(FadeCurve), value)) {
        fade.Curve = value;
      } else {
        Warn(curve, $"{fadeWhere}: invalid curve '{Scalar(curve)}', use linear or smooth");
      }
    }

    return fade;
  }

  Sides ParseSides(YamlNode node, string where) {
    Sides sides = Sides.None;
    IEnumerable<YamlNode> items = node is YamlSequenceNode sequence ? sequence.Children : IsNull(node) ? [] : [node];

    foreach (YamlNode item in items) {
      string text = Scalar(item);

      if (text != null
          && Enum.TryParse(text, ignoreCase: true, out Sides side)
          && side != Sides.None
          && Enum.IsDefined(typeof(Sides), side)) {
        sides |= side;
      } else {
        Warn(item, $"{where}: invalid side '{text}'");
      }
    }

    return sides;
  }

  Length? ParseLength(YamlNode node, string where, string key, bool allowNegative) {
    if (IsNull(node)) {
      return null;
    }

    string text = Scalar(node);

    if (!Length.TryParse(text, out Length length)) {
      Warn(node, $"{where}: invalid {key} '{text}', use a number with % or px");
      return null;
    }

    if (!allowNegative && length.Value < 0f) {
      Warn(node, $"{where}: {key} must not be negative");
      return null;
    }

    return length;
  }

  bool? ParseBool(YamlNode node, string where, string key) {
    if (IsNull(node)) {
      return null;
    }

    if (bool.TryParse(Scalar(node), out bool value)) {
      return value;
    }

    Warn(node, $"{where}: '{key}' must be true or false");
    return null;
  }

  bool CheckImage(YamlNode node, string path, string where) {
    if (string.IsNullOrWhiteSpace(path)) {
      Warn(node, $"{where}: empty image path, ignored");
      return false;
    }

    string normalized = NormalizePath(path);
    string extension;

    try {
      if (Path.IsPathRooted(path) || normalized.StartsWith("/", StringComparison.Ordinal)
          || normalized.Split('/').Any(segment => segment == "..")) {
        Warn(node, $"{where}: image path '{path}' must be relative to the config folder, ignored");
        return false;
      }

      extension = Path.GetExtension(normalized);
    } catch (ArgumentException) {
      Warn(node, $"{where}: image path '{path}' contains invalid characters, ignored");
      return false;
    }

    if (!KnownNames.ImageExtensions.Any(
        known => string.Equals(known, extension, StringComparison.OrdinalIgnoreCase))) {
      Warn(node, $"{where}: unsupported image type '{path}', use {string.Join(", ", KnownNames.ImageExtensions)}");
      return false;
    }

    if (!_imageExists(normalized)) {
      Warn(node, $"{where}: image '{path}' not found, ignored");
      return false;
    }

    return true;
  }

  void ValidateLayoutReferences() {
    ScreenConfig config = _result.Config;

    foreach (EntryList list in config.AllLists) {
      list.LayoutName = CheckLayoutName(list.LayoutName, list.Name);

      foreach (Entry entry in list.Entries) {
        entry.LayoutName = CheckLayoutName(entry.LayoutName, list.Name);
      }
    }

    foreach (Layout layout in config.Layouts.Values) {
      if (!layout.ShowsEntryImage) {
        Warn(0, $"layout '{layout.Name}' has no 'source: entry' layer, so entry images are not shown with it");
      }
    }
  }

  string CheckLayoutName(string name, string where) {
    if (name == null || _result.Config.Layouts.ContainsKey(name)) {
      return name;
    }

    Warn(0, $"{where}: unknown layout '{name}', the default layout is used");
    return null;
  }

  IEnumerable<YamlNode> Sequence(YamlNode node, string where) {
    if (IsNull(node)) {
      return [];
    }

    if (node is YamlSequenceNode sequence) {
      return sequence.Children;
    }

    Warn(node, $"{where} must be a list");
    return [];
  }

  void WarnUnknownKeys(YamlMappingNode mapping, IReadOnlyCollection<string> allowed, string where) {
    foreach (YamlNode key in mapping.Children.Keys) {
      if (!allowed.Contains(Key(key))) {
        Warn(key, $"{where}: unknown key '{Name(key)}'");
      }
    }
  }

  static YamlNode Child(YamlMappingNode mapping, string key) {
    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      if (Key(pair.Key) == key) {
        return pair.Value;
      }
    }

    return null;
  }

  static string Key(YamlNode node) {
    return (node as YamlScalarNode)?.Value?.Trim().ToLowerInvariant() ?? string.Empty;
  }

  static string Name(YamlNode node) {
    return (node as YamlScalarNode)?.Value?.Trim() ?? string.Empty;
  }

  static string Scalar(YamlNode node) {
    return IsNull(node) ? null : (node as YamlScalarNode)?.Value;
  }

  static bool IsNull(YamlNode node) {
    if (node == null) {
      return true;
    }

    if (node is not YamlScalarNode scalar || scalar.Style != ScalarStyle.Plain) {
      return false;
    }

    return string.IsNullOrEmpty(scalar.Value) || scalar.Value == "~" || scalar.Value == "null";
  }

  static string NormalizePath(string path) {
    return path.Trim().Replace('\\', '/');
  }

  void Warn(YamlNode node, string message) {
    Warn(node == null ? 0 : (int) node.Start.Line, message);
  }

  void Warn(long line, string message) {
    _result.Warnings.Add(line > 0 ? $"line {line}: {message}" : message);
  }
}
