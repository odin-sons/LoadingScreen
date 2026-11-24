namespace LoadingScreen.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using LoadingScreen.Model;

using Xunit;

public static class ConfigSchemaGenerator {
  static JsonObject Ref(string name) => new() { ["$ref"] = "#/$defs/" + name };
  static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode) JsonValue.Create(value)).ToArray());
  static JsonObject Object(JsonObject properties) => new() {
    ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false
  };
  static JsonObject Choice(params JsonNode[] choices) => new() {
    ["oneOf"] = new JsonArray(choices.Select(choice => choice.DeepClone()).ToArray())
  };

  static JsonObject EnumSchema(string name, IEnumerable<string> values) => new() {
    ["type"] = "string", ["enum"] = Strings(values), ["title"] = name
  };

  static string Kebab(string name) => string.Concat(name.Select((character, index) =>
      index > 0 && char.IsUpper(character) ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
  static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

  static JsonObject Field(SchemaFieldAttribute field, Type type) {
    JsonObject schema = field.Form switch {
      "auto-color" => Choice(new JsonObject { ["const"] = "auto" }, Color()),
      "animation" => Choice(new JsonObject { ["type"] = "string", ["minLength"] = 1 }, Ref("AnimationSpec")),
      "conditions" => Ref("Conditions"),
      "fit" => Choice(Ref("FitMode"), Ref("Fit")),
      "layer-image" => Choice(new JsonObject { ["type"] = "string" },
          new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }),
      "layout-map" => new JsonObject { ["type"] = "object", ["additionalProperties"] = Ref("LayoutValue") },
      "legacy-zoom" => Ref("LegacyZoom"),
      "length" => new JsonObject { ["type"] = "string", ["pattern"] = @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:%|px)?$" },
      "percent" => new JsonObject { ["type"] = "string", ["pattern"] = @"^(?:100(?:\.0+)?|(?:\d|[1-9]\d)(?:\.\d+)?)%$" },
      "position" => Choice(Ref("AnchorName"), Ref("Position")),
      "sides" => Choice(Ref("SideName"), new JsonObject { ["type"] = "array", ["items"] = Ref("SideName") }),
      "spinner" => Choice(Ref("SpinnerStyle"), new JsonObject { ["type"] = "string", ["pattern"] = @"\.(png|jpe?g)$" }),
      "text-source" => Ref("TextSource"),
      "translated-text" => Choice(new JsonObject { ["type"] = "string" },
          new JsonObject { ["type"] = "object", ["additionalProperties"] = new JsonObject { ["type"] = "string" } }),
      "when" => Ref("When"),
      _ => FieldType(type),
    };
    schema["description"] = field.Description;
    bool stringValue = schema["type"]?.GetValue<string>() == "string";
    if (!double.IsNaN(field.Minimum)) schema[stringValue ? "x-minimum" :
        field.ExclusiveMinimum ? "exclusiveMinimum" : "minimum"] = field.Minimum;
    if (!double.IsNaN(field.Maximum)) schema[stringValue ? "x-maximum" : "maximum"] = field.Maximum;
    if (field.Form == null) schema["x-parserType"] = ParserType(type);
    return schema;
  }

  static JsonObject Color() => new() { ["type"] = "string", ["pattern"] = "^#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$" };

  static string ParserType(Type type) {
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type == typeof(bool)) return "bool";
    if (type == typeof(int)) return "int";
    if (type == typeof(float) || type == typeof(double)) return "float";
    if (type == typeof(ColorValue)) return "color";
    return "value";
  }

  static JsonObject FieldType(Type type) {
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type == typeof(bool)) return new() { ["type"] = "boolean" };
    if (type == typeof(int)) return new() { ["type"] = "integer" };
    if (type == typeof(float) || type == typeof(double)) return new() { ["type"] = "number" };
    if (type == typeof(ColorValue)) return Color();
    if (type == typeof(string)) return new() { ["type"] = "string" };
    if (type == typeof(Anchor)) return Ref("AnchorName");
    if (type == typeof(PickOrder)) return Ref("PickOrder");
    if (type == typeof(ProgressStyle)) return Ref("ProgressStyle");
    if (type == typeof(TextAlign)) return Ref("TextAlign");
    if (type == typeof(FadeCurve)) return Ref("FadeCurve");
    if (type.IsEnum) return Ref(type.Name);
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
      return new() { ["type"] = "array", ["items"] = FieldType(type.GenericTypeArguments[0]) };
    return Ref(type.Name);
  }

  static JsonObject Properties(Type type, bool declaredOnly = false) {
    JsonObject properties = new();
    foreach (SchemaFieldAttribute field in type.GetCustomAttributes(typeof(SchemaFieldAttribute), false)
        .Cast<SchemaFieldAttribute>()
        .OrderBy(field => field.Name, StringComparer.Ordinal))
      properties[field.Name] = Field(field, field.ValueType ?? typeof(string));
    BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
    if (declaredOnly) flags |= BindingFlags.DeclaredOnly;
    foreach (PropertyInfo property in type.GetProperties(flags).OrderBy(property => property.Name, StringComparer.Ordinal)) {
      foreach (SchemaFieldAttribute field in property.GetCustomAttributes<SchemaFieldAttribute>()) {
        JsonObject schema = Field(field, field.EnumType ?? field.ValueType ?? property.PropertyType);
        schema["x-modelProperty"] = property.Name;
        properties[field.Name] = schema;
      }
    }
    return properties;
  }

  static JsonObject TypeDefinition(Type type, bool declaredOnly = false) => Object(Properties(type, declaredOnly));

  static JsonObject NumericEnum(Type type) {
    JsonObject numbers = new();
    foreach (string name in Enum.GetNames(type)) numbers[name] = Convert.ToInt32(Enum.Parse(type, name));
    return new() { ["type"] = "string", ["enum"] = Strings(Enum.GetNames(type)), ["x-enumValues"] = numbers };
  }

  static JsonObject Build() {
    JsonObject defs = new();
    foreach (Type type in new[] {
        typeof(ScreenDefaults), typeof(Layout), typeof(Layer), typeof(TextStyle), typeof(Entry),
        typeof(EntryList), typeof(TipDefaults), typeof(SeparatorDefaults),
        typeof(SpinnerDefaults), typeof(Fit), typeof(Fade), typeof(AnimationSpec),
        typeof(VignetteSettings), typeof(MaskSettings) })
      defs[type.Name] = TypeDefinition(type, type == typeof(ScreenDefaults));

    JsonObject layer = (JsonObject) defs["Layer"];
    JsonObject layerProperties = (JsonObject) layer["properties"];
    layer["x-layerKinds"] = Strings(typeof(Layer).GetProperties()
        .SelectMany(property => property.GetCustomAttributes<SchemaFieldAttribute>())
        .Where(field => field.LayerKind).OrderBy(field => field.LayerKindOrder).Select(field => field.Name));
    foreach (KeyValuePair<string, JsonNode> property in (JsonObject) defs["TextStyle"]["properties"])
      if (!layerProperties.ContainsKey(property.Key)) layerProperties[property.Key] = property.Value.DeepClone();

    JsonObject placement = Properties(typeof(Placement));
    JsonNode animate = placement["animate"].DeepClone();
    placement.Remove("animate");
    JsonNode x = placement["x"].DeepClone();
    JsonNode y = placement["y"].DeepClone();
    placement.Remove("x");
    placement.Remove("y");
    defs["Placement"] = Object(placement);
    JsonObject animatedPlacement = (JsonObject) defs["Placement"].DeepClone();
    ((JsonObject) animatedPlacement["properties"])["animate"] = animate;
    defs["AnimatedPlacement"] = animatedPlacement;
    defs["ScreenDefaults"]["properties"]["place"] = Ref("AnimatedPlacement");
    layerProperties["place"] = Ref("AnimatedPlacement");
    defs["Position"] = Object(new JsonObject {
      ["anchor"] = Ref("AnchorName"), ["x"] = x, ["y"] = y
    });

    defs["FitMode"] = EnumSchema("Fit mode", Enum.GetNames<FitMode>().Where(name => name != "Box").Select(Kebab));
    defs["Fit"]["x-formName"] = Kebab(nameof(FitMode.Box));
    defs["AnchorName"] = EnumSchema("Anchor", Enum.GetNames<Anchor>().Select(Kebab));
    defs["SideName"] = EnumSchema("Fade side", new[] { "left", "right", "top", "bottom", "all" });
    defs["SpinnerStyle"] = EnumSchema("Spinner style", Enum.GetNames<SpinnerStyle>().Where(name => name != "Image").Select(Kebab));
    defs["TextSource"] = EnumSchema("Text source", Enum.GetNames<TextSource>().Select(Kebab));
    defs["TextSource"]["x-removedValues"] = Strings(ConfigParser.RemovedTextSources);
    defs["ScreenDefaults"]["x-removedKeys"] = Strings(ConfigParser.RemovedDefaultKeys);
    defs["PickOrder"] = EnumSchema("Order", Enum.GetNames<PickOrder>().Select(Kebab));
    defs["ProgressStyle"] = EnumSchema("Progress style", Enum.GetNames<ProgressStyle>().Select(Kebab));
    defs["TextAlign"] = EnumSchema("Text alignment", Enum.GetNames<TextAlign>().Select(Kebab));
    defs["FadeCurve"] = EnumSchema("Fade curve", Enum.GetNames<FadeCurve>().Select(Kebab));
    defs["Biome"] = EnumSchema("Biome", KnownNames.Biomes);
    defs["DeathCause"] = EnumSchema("Death cause", DeathCauses.Names);
    defs["AnimationPreset"] = EnumSchema("Built-in animation", new AnimationCatalog().Presets.Keys.Order(StringComparer.Ordinal));
    defs["GlobalKey"] = EnumSchema("Global key", KnownNames.GlobalKeys);
    defs["ImageExtension"] = EnumSchema("Image extension", KnownNames.ImageExtensions);
    defs["AudioExtension"] = EnumSchema("Audio extension", KnownNames.AudioExtensions);
    string[] conditions = Enum.GetNames<ConditionKind>().Select(Camel).ToArray();
    defs["ConditionName"] = EnumSchema("Condition", conditions.Concat(conditions.Select(name => "not" + char.ToUpperInvariant(name[0]) + name.Substring(1))));
    defs["Language"] = new JsonObject {
      ["type"] = "string", ["enum"] = Strings(LanguageText.GameCodes.Values.Distinct().Order(StringComparer.Ordinal)),
      ["x-nameToCode"] = JsonSerializer.SerializeToNode(
          LanguageText.GameCodes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
              .ToDictionary(pair => pair.Key, pair => pair.Value))
    };
    defs["ProblemCode"] = new JsonObject {
      ["type"] = "string",
      ["enum"] = Strings(typeof(ProblemCodes).GetFields()
          .Where(field => field.IsLiteral && field.FieldType == typeof(string))
          .Select(field => (string) field.GetRawConstantValue()))
    };
    foreach (Type type in new[] { typeof(Anchor), typeof(FitMode), typeof(Sides), typeof(FadeCurve),
        typeof(ProgressStyle), typeof(TextAlign), typeof(ConditionKind) })
      defs[type.Name + "Values"] = NumericEnum(type);

    JsonObject conditionProperties = new();
    foreach (string name in conditions.Concat(conditions.Select(value => "not" + value[0].ToString().ToUpperInvariant() + value.Substring(1))))
      conditionProperties[name] = Choice(new JsonObject { ["type"] = "string" },
          new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } });
    defs["Conditions"] = Object(conditionProperties);
    defs["LegacyZoom"] = Object(new JsonObject {
      ["enabled"] = new JsonObject { ["type"] = "boolean" },
      ["amount"] = new JsonObject { ["type"] = "number" },
      ["seconds"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
    });
    defs["LayoutValue"] = Choice(new JsonObject { ["type"] = "array", ["items"] = Ref("Layer") }, Ref("Layout"));
    defs["Layout"]["properties"]["layers"]["maxItems"] = ConfigParser.MaxLayers;
    defs["EntryValue"] = Choice(new JsonObject { ["type"] = "string" }, Ref("Entry"));
    defs["EntryListValue"] = Choice(new JsonObject { ["type"] = "array", ["items"] = Ref("EntryValue") }, Ref("EntryList"));
    JsonObject namedLists = new() { ["type"] = "object", ["additionalProperties"] = Ref("EntryListValue") };
    defs["When"] = Object(new JsonObject {
      ["general"] = Ref("EntryListValue"), ["sleep"] = Ref("EntryListValue"),
      ["biome"] = namedLists.DeepClone(), ["death"] = namedLists.DeepClone(), ["enemy"] = namedLists.DeepClone()
    });

    JsonObject root = Object(Properties(typeof(ScreenConfig)));
    root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
    root["$id"] = "https://odin-sons.github.io/LoadingScreen/data/config.schema.json";
    root["title"] = "LoadingScreen configuration";
    root["description"] = "loadingscreen.yaml for the LoadingScreen Valheim mod.";
    root["$defs"] = defs;
    return root;
  }

  public static string Generate() => Build().ToJsonString(new JsonSerializerOptions { WriteIndented = true })
      .Replace("\r\n", "\n") + "\n";
}

public class ConfigSchemaTests {
  [Fact]
  public void GeneratedSchemaIsCurrent() {
    string root = SiteConformanceTests.FindRoot();
    string path = Path.Combine(root, "site", "data", "config.schema.json");
    string actual = ConfigSchemaGenerator.Generate();
    if (Environment.GetEnvironmentVariable("LOADINGSCREEN_UPDATE_FIXTURES") == "1")
      File.WriteAllText(path, actual, new UTF8Encoding(false));
    Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
  }
}
