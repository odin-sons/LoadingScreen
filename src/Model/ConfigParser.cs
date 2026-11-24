namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

public sealed class ConfigLoadResult {
  public ScreenConfig Config { get; } = new();
  public List<string> Warnings { get; } = [];
  public List<(long Line, string Detail)> Problems { get; } = [];
  public bool IsValid { get; set; } = true;
  public bool HasDeclaredEntries { get; set; }
}

public sealed class ConfigParser {
  const int MaxNestingDepth = 32;
  public const int MaxLayers = 32;

  static readonly string[] _placementKeys = ["fit", "position", "opacity", "fade"];
  static readonly string[] _animatedPlacementKeys = [.. _placementKeys, "animate"];
  static readonly string[] _defaultKeys = [
    "place", "background", "tint", "vignette", "mask", "animation", "tip", "continueprompt", "separator", "spinner",
    "zoom"
  ];
  static readonly string[] _layerKeys = [
    "id", "color", "image", "text", "spinner", "progress", "size", "align", "background", "place", "animation"
  ];
  static readonly string[] _layoutKeys = ["layers", "sound", "showsleepui", "vignette", "mask", "place"];
  static readonly string[] _entryKeys = ["image", "sound", "voice", "text", "layout", "place", "when"];

  readonly Func<string, bool> _assetExists;
  readonly IEnumerable<string> _knownCauses;

  ConfigLoadResult _result;

  public ConfigParser(Func<string, bool> assetExists, IEnumerable<string> knownCauses = null) {
    _assetExists = assetExists ?? (_ => true);
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

        case "when":
          ParseWhen(pair.Value);
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
      WarnUnknownKeys(mapping, _layoutKeys, $"layout '{name}'");
      layout.Placement = ParsePlacement(Child(mapping, "place"), $"layout '{name}': 'place'");
      layersNode = Child(mapping, "layers");
      layout.Vignette = ParseVignette(Child(mapping, "vignette"), $"layout '{name}'.vignette");
      layout.Mask = ParseMask(Child(mapping, "mask"), $"layout '{name}'.mask");
      layout.Sound = ParseAudio(Child(mapping, "sound"), $"layout '{name}'", "sound");
      YamlNode showSleepUi = Child(mapping, "showsleepui");
      if (!IsNull(showSleepUi)) {
        if (bool.TryParse(Scalar(showSleepUi), out bool show)) {
          layout.ShowSleepUi = show;
        } else {
          Warn(showSleepUi, $"layout '{name}': 'showSleepUi' must be true or false");
        }
      }
    }

    if (layersNode is not YamlSequenceNode layers) {
      Warn(node, $"layout '{name}' needs a 'layers' list, ignored");
      return null;
    }

    for (int index = 0; index < Math.Min(layers.Children.Count, MaxLayers); index++) {
      Layer layer = ParseLayer(layers.Children[index], name, index);

      if (layer != null) {
        layout.Layers.Add(layer);
      }
    }

    if (layers.Children.Count > MaxLayers) {
      Warn(layers.Children[MaxLayers], $"layout '{name}': only the first {MaxLayers} layers are used, the rest are ignored");
    }

    if (layout.Layers.Count == 0) {
      Warn(node, $"layout '{name}' has no valid layers, ignored");
      return null;
    }

    return layout;
  }

  Layer ParseLayer(YamlNode node, string layoutName, int index) {
    string id = node is YamlMappingNode layerMapping ? Scalar(Child(layerMapping, "id")) : null;
    string where = $"layout '{layoutName}' layer '{id ?? index.ToString(CultureInfo.InvariantCulture)}'";

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: a layer must be a mapping, ignored");
      return null;
    }

    WarnUnknownKeys(mapping, _layerKeys, where);
    Layer layer = new() {
      Id = Scalar(Child(mapping, "id")),
      Placement = ParsePlacement(Child(mapping, "place"), $"{where}: 'place'", keys: _animatedPlacementKeys),
      Animation = ParseAnimation(Child(mapping, "animation"), $"{where}.animation"),
    };
    layer.Animation ??= LegacyAnimate(Child(mapping, "place"), $"{where}: 'place'");

    YamlNode color = Child(mapping, "color");
    YamlNode image = Child(mapping, "image");
    YamlNode text = Child(mapping, "text");
    YamlNode spinner = Child(mapping, "spinner");
    YamlNode progress = Child(mapping, "progress");

    if ((image != null ? 1 : 0) + (text != null ? 1 : 0) + (spinner != null ? 1 : 0)
        + (progress != null ? 1 : 0) > 1) {
      Warn(node, $"{where}: use only one of 'image', 'text', 'spinner' and 'progress' in a layer, ignored");
      return null;
    }

    if (progress != null) {
      if (!Enum.TryParse(Scalar(progress), ignoreCase: true, out ProgressStyle style)
          || !Enum.IsDefined(typeof(ProgressStyle), style)) {
        Warn(progress, $"{where}: 'progress' needs bar, ring or percent; layer ignored");
        return null;
      }
      layer.Kind = LayerKind.Progress;
      layer.ProgressStyle = style;
      layer.Color = ParseColor(color, where, "color") ?? new(255, 255, 255);
      YamlNode progressSize = Child(mapping, "size");
      if (!IsNull(progressSize)) {
        if (float.TryParse(Scalar(progressSize), NumberStyles.Float, CultureInfo.InvariantCulture, out float sizeValue)
            && !float.IsInfinity(sizeValue) && !float.IsNaN(sizeValue) && sizeValue > 0f) {
          layer.ProgressSize = sizeValue;
        } else {
          Warn(progressSize, $"{where}: 'size' must be positive");
        }
      }
      return layer;
    }

    if (spinner != null) {
      if (color != null) {
        Warn(node, $"{where}: use only one of 'color' and 'spinner' in a layer, ignored");
        return null;
      }
      if (spinner is not YamlScalarNode || string.IsNullOrWhiteSpace(Scalar(spinner))) {
        Warn(spinner, $"{where}: 'spinner' needs hammer, ring, dots or an image path; layer ignored");
        return null;
      }
      layer.Kind = LayerKind.Spinner;
      string style = Scalar(spinner).Trim();
      if (style.Equals("hammer", StringComparison.OrdinalIgnoreCase)) layer.SpinnerStyle = SpinnerStyle.Hammer;
      else if (style.Equals("ring", StringComparison.OrdinalIgnoreCase)) layer.SpinnerStyle = SpinnerStyle.Ring;
      else if (style.Equals("dots", StringComparison.OrdinalIgnoreCase)) layer.SpinnerStyle = SpinnerStyle.Dots;
      else {
        if (!CheckImage(spinner, style, where)) return null;
        layer.SpinnerStyle = SpinnerStyle.Image;
        layer.Images.Add(NormalizePath(style));
      }
      layer.Animation ??= new();
      layer.Animation.Idle ??= "spin";
      return layer;
    }

    if (text != null) {
      return ParseTextLayer(layer, mapping, text, where);
    }

    foreach (string key in new[] { "size", "align", "background" }) {
      if (Child(mapping, key) != null) {
        Warn(Child(mapping, key), $"{where}: '{key}' applies only to text layers");
      }
    }

    if (image == null) {
      if (color == null) {
        Warn(node, $"{where}: a layer needs 'image', 'text' or 'color', ignored");
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

    if (image is YamlScalarNode && string.Equals(Scalar(image), "separator", StringComparison.OrdinalIgnoreCase)) {
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

    if (image is YamlScalarNode && string.Equals(Scalar(image), "entry", StringComparison.OrdinalIgnoreCase)) {
      layer.FromEntry = true;
      return layer;
    }

    IEnumerable<YamlNode> imageNodes = image is YamlSequenceNode sequence ? sequence.Children : [image];

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
    string sourceName = Scalar(text);
    if (!string.Equals(sourceName, "caption", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(sourceName, "prompt", StringComparison.OrdinalIgnoreCase)) {
      Warn(text, $"{where}: 'text' needs caption or prompt, layer ignored");
      return null;
    }

    layer.Kind = LayerKind.Text;
    layer.TextSource = string.Equals(sourceName, "prompt", StringComparison.OrdinalIgnoreCase)
        ? TextSource.Prompt : TextSource.Caption;
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

    YamlNode textColor = Child(mapping, "color");
    YamlNode background = Child(mapping, "background");
    layer.Text.AutoColor = string.Equals(Scalar(textColor), "auto", StringComparison.OrdinalIgnoreCase);
    layer.Text.AutoBackground = string.Equals(Scalar(background), "auto", StringComparison.OrdinalIgnoreCase);
    if (!layer.Text.AutoColor) layer.Text.Color = ParseColor(textColor, where, "color");
    if (!layer.Text.AutoBackground) layer.Text.Background = ParseColor(background, where, "background");

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
      YamlMappingNode place = Child(mapping, "place") as YamlMappingNode;
      Warn(place == null ? null : Child(place, "fit"),
          $"{where}: a text layer supports only 'fit: {{ width }}' (the text width)");
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

  void ParseWhen(YamlNode node) {
    if (IsNull(node)) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, "'when' must be a mapping");
      return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      switch (Key(pair.Key)) {
        case "general":
          _result.Config.General = ParseEntryList("general", pair.Value);
          break;

        case "sleep":
          _result.Config.Sleep = ParseEntryList("sleep", pair.Value);
          break;

        case "biome":
          ParseNamedLists(pair.Value, "biome", _result.Config.Biome, null);
          break;

        case "death":
          ParseNamedLists(pair.Value, "death", _result.Config.Death, _knownCauses);
          break;

        case "enemy":
          ParseNamedLists(pair.Value, "enemy", _result.Config.Enemy, null);
          break;

        default:
          Warn(pair.Key, $"unknown situation '{Name(pair.Key)}', use 'general', 'sleep', 'biome', 'death' or 'enemy'");
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

      if (situation == "biome") {
        name = KnownNames.Biomes.FirstOrDefault(
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) ?? name;
      } else if (knownNames != null) {
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

  EntryList ParseEntryList(string name, YamlNode node) {
    EntryList list = new() { Name = name };
    YamlNode entriesNode = node;

    if (node is YamlMappingNode mapping) {
      WarnUnknownKeys(mapping, ["layout", "sound", "order", "entries"], name);
      list.LayoutName = Scalar(Child(mapping, "layout"));
      list.Sound = ParseAudio(Child(mapping, "sound"), name, "sound");

      YamlNode orderNode = Child(mapping, "order");

      if (orderNode != null && TryParseOrder(orderNode, $"'{name}' order", out PickOrder order)) {
        list.Order = order;
      }

      entriesNode = Child(mapping, "entries");
    }

    int index = 0;
    foreach (YamlNode entryNode in Sequence(entriesNode, $"'{name}' entries")) {
      _result.HasDeclaredEntries = true;
      Entry entry = ParseEntry(entryNode, $"{name}.entries[{index++}]");

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
    YamlNode textNode = Child(mapping, "text");
    string text = Scalar(textNode);
    Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);

    if (textNode is YamlMappingNode textMapping) {
      foreach (KeyValuePair<YamlNode, YamlNode> pair in textMapping.Children) {
        if (!LanguageText.TryCode(Name(pair.Key), out string code)) {
          Warn(pair.Key, $"{listName}: unknown text language '{Name(pair.Key)}'");
        } else if (string.IsNullOrWhiteSpace(Scalar(pair.Value))) {
          Warn(pair.Value, $"{listName}: text for '{code}' must be a non-empty string");
        } else {
          translations[code] = Scalar(pair.Value);
        }
      }
    } else if (!IsNull(textNode) && textNode is not YamlScalarNode) {
      Warn(textNode, $"{listName}: 'text' must be a string or a language mapping");
    }

    if (IsNull(imageNode) && string.IsNullOrEmpty(text) && translations.Count == 0) {
      Warn(node, $"{listName}: an entry needs an 'image' or a 'text', ignored");
      return null;
    }

    if (!IsNull(imageNode) && !CheckImage(imageNode, image, listName)) {
      return null;
    }

    YamlNode place = Child(mapping, "place");

    Entry entry = new() {
      Image = IsNull(imageNode) ? null : NormalizePath(image),
      Sound = ParseAudio(Child(mapping, "sound"), listName, "sound"),
      Voice = ParseAudio(Child(mapping, "voice"), listName, "voice"),
      Text = text,
      LayoutName = Scalar(Child(mapping, "layout")),
      Place = ParsePlacement(place, $"{listName}: 'place'"),
    };
    foreach (KeyValuePair<string, string> translation in translations) {
      entry.Translations[translation.Key] = translation.Value;
    }

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

          name = biome ?? name;
        }

        conditions.Add(new() { Kind = kind, Name = name, Negated = negated });
      }
    }

    return true;
  }

  ScreenDefaults ParseDefaults(YamlNode node) {
    ScreenDefaults defaults = new();
    if (IsNull(node)) {
      return defaults;
    }
    if (node is not YamlMappingNode mapping) {
      Warn(node, "defaults: expected a mapping");
      return defaults;
    }

    WarnUnknownKeys(mapping, _defaultKeys, "defaults");
    ParsePlacement(Child(mapping, "place"), "defaults.place", defaults, _animatedPlacementKeys);
    defaults.Background = ParseDefaultColor(Child(mapping, "background"), "defaults.background", defaults.Background);
    defaults.Tint = ParseDefaultColor(Child(mapping, "tint"), "defaults.tint", defaults.Tint);
    defaults.Vignette = ParseVignette(Child(mapping, "vignette"), "defaults.vignette");
    defaults.Mask = ParseMask(Child(mapping, "mask"), "defaults.mask");
    defaults.Animation = ParseAnimation(Child(mapping, "animation"), "defaults.animation")
        ?? LegacyAnimate(Child(mapping, "place"), "defaults.place")
        ?? LegacyZoom(Child(mapping, "zoom"));
    ParseDefaultSection(Child(mapping, "tip"), "tip", defaults.Tip,
        new Dictionary<string, (float Min, float Max)> { ["Size"] = (0f, 64f) });
    ParseDefaultSection(Child(mapping, "continueprompt"), "continuePrompt", defaults.ContinuePrompt,
        new Dictionary<string, (float Min, float Max)> { ["Size"] = (0f, 64f) });
    ParseDefaultSection(Child(mapping, "separator"), "separator", defaults.Separator);
    ParseDefaultSection(Child(mapping, "spinner"), "spinner", defaults.Spinner);

    return defaults;
  }

  void ParseDefaultSection<T>(
      YamlNode node, string section, T target, IReadOnlyDictionary<string, (float Min, float Max)> ranges = null) {
    if (node == null) {
      return;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"defaults.{section}: expected a mapping; built-in values used");
      return;
    }

    PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

    foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children) {
      string key = Name(pair.Key);
      PropertyInfo property = properties.FirstOrDefault(
          candidate => string.Equals(candidate.Name, key, StringComparison.OrdinalIgnoreCase));

      if (property == null) {
        Warn(pair.Key, $"defaults.{section}: unknown key '{key}'");
        continue;
      }

      string where = $"{section}.{key}";
      object fallback = property.GetValue(target);
      object value = property.PropertyType == typeof(bool)
          ? ParseDefaultBool(pair.Value, where, (bool) fallback)
          : property.PropertyType == typeof(ColorValue)
              ? ParseDefaultColor(pair.Value, where, (ColorValue) fallback)
              : ParseDefaultNumber(pair.Value, where, property, fallback, ranges);
      property.SetValue(target, value);
    }
  }

  object ParseDefaultNumber(
      YamlNode node, string where, PropertyInfo property, object fallback,
      IReadOnlyDictionary<string, (float Min, float Max)> ranges) {
    float min = float.MinValue;
    float max = float.MaxValue;
    if (ranges != null && ranges.TryGetValue(property.Name, out var bounds)) {
      min = bounds.Min;
      max = bounds.Max;
    }

    if (property.PropertyType == typeof(int)) {
      return ParseDefaultInt(node, where, (int) fallback, (int) min, (int) max);
    }

    return ParseDefaultFloat(node, where, (float) fallback, min, max);
  }

  VignetteSettings ParseVignette(YamlNode node, string where) {
    if (node == null) return null;
    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: expected a mapping; effect ignored");
      return null;
    }

    WarnUnknownKeys(mapping, ["color", "size", "strength"], where);
    VignetteSettings result = new() { Color = ParseColor(Child(mapping, "color"), where, "color") };
    YamlNode sizeNode = Child(mapping, "size");
    if (!IsNull(sizeNode)) {
      string text = Scalar(sizeNode);
      if (text != null && text.EndsWith("%", StringComparison.Ordinal)
          && Length.TryParse(text, out Length length) && length.Value >= 0f && length.Value <= 100f) {
        result.Size = length.Value;
      } else {
        Warn(sizeNode, $"{where}: 'size' must be from 0% to 100%");
      }
    }

    result.Strength = ParseEffectOpacity(Child(mapping, "strength"), where, "strength");
    return result;
  }

  MaskSettings ParseMask(YamlNode node, string where) {
    if (node == null) return null;
    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: expected a mapping; effect ignored");
      return null;
    }

    WarnUnknownKeys(mapping, ["color", "opacity"], where);
    return new() {
      Color = ParseColor(Child(mapping, "color"), where, "color"),
      Opacity = ParseEffectOpacity(Child(mapping, "opacity"), where, "opacity"),
    };
  }

  float? ParseEffectOpacity(YamlNode node, string where, string key) {
    if (IsNull(node)) return null;
    if (float.TryParse(Scalar(node), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
        && !float.IsNaN(value) && value >= 0f && value <= 1f) {
      return value;
    }

    Warn(node, $"{where}: '{key}' must be a number from 0 to 1");
    return null;
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

  Placement ParsePlacement(
      YamlNode node, string where, Placement placement = null, IReadOnlyCollection<string> keys = null) {
    placement ??= new Placement();

    if (IsNull(node)) {
      return placement;
    }

    if (node is not YamlMappingNode mapping) {
      Warn(node, $"{where}: expected a mapping");
      return placement;
    }

    WarnUnknownKeys(mapping, keys ?? _placementKeys, where);

    placement.Fit = ParseFit(Child(mapping, "fit"), where);
    ParsePosition(Child(mapping, "position"), placement, where);
    placement.Opacity = ParseOpacity(Child(mapping, "opacity"), where);
    placement.Fade = ParseFade(Child(mapping, "fade"), where);

    return placement;
  }

  AnimationSpec LegacyAnimate(YamlNode place, string where) {
    if (place is not YamlMappingNode mapping) return null;
    bool? animate = ParseBool(Child(mapping, "animate"), where, "animate");
    return animate.HasValue ? new() { Idle = animate.Value ? "zoom-in" : "none" } : null;
  }

  AnimationSpec LegacyZoom(YamlNode node) {
    if (IsNull(node)) return null;
    if (node is not YamlMappingNode mapping) {
      Warn(node, "defaults.zoom: expected a mapping");
      return null;
    }
    WarnUnknownKeys(mapping, ["enabled", "amount", "seconds"], "defaults.zoom");
    bool? enabled = ParseBool(Child(mapping, "enabled"), "defaults.zoom", "enabled");
    if (enabled == false) return new() { Idle = "none" };
    YamlNode secondsNode = Child(mapping, "seconds");
    if (IsNull(secondsNode)) return null;
    if (float.TryParse(Scalar(secondsNode), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
        && !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0f) {
      return new() { Speed = 15f / seconds };
    }
    Warn(secondsNode, "defaults.zoom: 'seconds' must be positive");
    return null;
  }

  AnimationSpec ParseAnimation(YamlNode node, string where) {
    if (IsNull(node)) return null;
    if (node is YamlScalarNode) {
      string name = Scalar(node)?.Trim();
      if (!string.IsNullOrEmpty(name)) return new() { Idle = name };
      Warn(node, $"{where}: preset name cannot be empty");
      return null;
    }
    if (node is not YamlMappingNode map) {
      Warn(node, $"{where}: expected a preset name or phase mapping");
      return null;
    }
    WarnUnknownKeys(map, ["enter", "idle", "exit", "speed", "origin"], where);
    AnimationSpec spec = new() {
      Enter = Phase("enter"),
      Idle = Phase("idle"),
      Exit = Phase("exit"),
      Origin = ParseAnchor(Child(map, "origin"), $"{where}: 'origin'"),
    };
    YamlNode speedNode = Child(map, "speed");
    if (!IsNull(speedNode)) {
      if (float.TryParse(Scalar(speedNode), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)
          && !float.IsNaN(speed) && !float.IsInfinity(speed) && speed > 0f) spec.Speed = speed;
      else Warn(speedNode, $"{where}: speed must be positive");
    }
    return spec;

    string Phase(string key) {
      YamlNode phase = Child(map, key);
      if (IsNull(phase)) return null;
      string name = Scalar(phase)?.Trim();
      if (!string.IsNullOrEmpty(name)) return name;
      Warn(phase, $"{where}: '{key}' must be a preset name");
      return null;
    }
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

    if (!_assetExists(normalized)) {
      Warn(node, $"{where}: image '{path}' not found, ignored");
      return false;
    }

    return true;
  }

  string ParseAudio(YamlNode node, string where, string key) {
    if (IsNull(node)) return null;

    string path = Scalar(node);
    if (string.IsNullOrWhiteSpace(path)) {
      Warn(node, $"{where}: '{key}' needs an audio path, ignored");
      return null;
    }

    string normalized = NormalizePath(path);
    string extension;
    try {
      if (Path.IsPathRooted(path) || normalized.StartsWith("/", StringComparison.Ordinal)
          || normalized.Split('/').Any(segment => segment == ".." || segment.IndexOf(':') >= 0)) {
        Warn(node, $"{where}: audio path '{path}' must be relative to the config folder, ignored");
        return null;
      }
      extension = Path.GetExtension(normalized);
    } catch (ArgumentException) {
      Warn(node, $"{where}: invalid audio path '{path}', ignored");
      return null;
    }

    if (!KnownNames.AudioExtensions.Any(known => string.Equals(known, extension, StringComparison.OrdinalIgnoreCase))) {
      Warn(node, $"{where}: unsupported audio type '{path}', use .ogg or .wav");
      return null;
    }

    if (!_assetExists(normalized)) {
      Warn(node, $"{where}: audio '{path}' not found, ignored");
      return null;
    }

    return normalized;
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
        Warn(0, $"layout '{layout.Name}' has no 'image: entry' layer, so entry images are not shown with it");
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
    Warn(node == null ? 0 : (int) node.Start.Line, message, Scalar(node));
  }

  void Warn(long line, string message, string value = null) {
    _result.Problems.Add((line, message));
    _result.Warnings.Add(LogContext.Message("Configuration warning.",
        ("file", "loadingscreen.yaml"), ("line", line), ("detail", message), ("value", value)));
  }
}
