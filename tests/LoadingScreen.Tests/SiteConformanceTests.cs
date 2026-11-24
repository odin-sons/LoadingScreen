namespace LoadingScreen.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

using LoadingScreen.Model;

using Xunit;

public class SiteConformanceTests {
  static readonly string _root = FindRoot();
  static readonly JsonElement _schema = JsonDocument.Parse(ConfigSchemaGenerator.Generate()).RootElement;
  static JsonElement Def(string name) => _schema.GetProperty("$defs").GetProperty(name);
  static string[] Names(string name) => [.. Def(name).GetProperty("enum").EnumerateArray().Select(item => item.GetString())];
  static string[] Keys(string name) => [.. Def(name).GetProperty("properties").EnumerateObject()
      .Select(property => property.Name.ToLowerInvariant())];
  static string[] ParserKeys(string field) => (string[]) typeof(ConfigParser)
      .GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
  static string Kebab(string name) => string.Concat(name.Select((character, index) =>
      index > 0 && char.IsUpper(character) ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
  static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

  public static string FindRoot() {
    for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
      if (File.Exists(Path.Combine(directory.FullName, "site", "data", "config.schema.json"))) return directory.FullName;
    throw new InvalidOperationException("site/data/config.schema.json not found above the test output folder");
  }

  [Fact]
  public void SchemaNameListsMatchTheParser() {
    Assert.Equal(KnownNames.Biomes, Names("Biome"));
    Assert.Equal(DeathCauses.Names, Names("DeathCause"));
    Assert.Equal(new AnimationCatalog().Presets.Keys.Order(StringComparer.Ordinal), Names("AnimationPreset"));
    Assert.Equal(Enum.GetNames<Anchor>().Select(Kebab), Names("AnchorName"));
    Assert.Equal(KnownNames.GlobalKeys, Names("GlobalKey"));
    Assert.Equal(KnownNames.ImageExtensions, Names("ImageExtension"));
    Assert.Equal(KnownNames.AudioExtensions, Names("AudioExtension"));
    string[] kinds = Enum.GetNames<ConditionKind>();
    Assert.Equal(kinds.Select(Camel).Concat(kinds.Select(kind => "not" + kind)), Names("ConditionName"));
    Assert.Equal(
        LanguageText.GameCodes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        Def("Language").GetProperty("x-nameToCode").EnumerateObject()
            .Select(pair => new KeyValuePair<string, string>(pair.Name, pair.Value.GetString()))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal));
  }

  [Theory]
  [InlineData("AnchorValues", typeof(Anchor))]
  [InlineData("FitModeValues", typeof(FitMode))]
  [InlineData("SidesValues", typeof(Sides))]
  [InlineData("FadeCurveValues", typeof(FadeCurve))]
  [InlineData("ProgressStyleValues", typeof(ProgressStyle))]
  [InlineData("TextAlignValues", typeof(TextAlign))]
  [InlineData("ConditionKindValues", typeof(ConditionKind))]
  public void SchemaNumericEnumTablesMatchTheParser(string name, Type type) {
    JsonElement table = Def(name).GetProperty("x-enumValues");
    Assert.Equal(Enum.GetNames(type), table.EnumerateObject().Select(property => property.Name));
    foreach (string member in Enum.GetNames(type))
      Assert.Equal(Convert.ToInt32(Enum.Parse(type, member)), table.GetProperty(member).GetInt32());
  }

  [Theory]
  [InlineData("tip", typeof(TipDefaults))]
  [InlineData("separator", typeof(SeparatorDefaults))]
  [InlineData("spinner", typeof(SpinnerDefaults))]
  public void SchemaDefaultSectionsMatchTheParser(string section, Type type) {
    string defName = char.ToUpperInvariant(section[0]) + section.Substring(1) + "Defaults";
    JsonElement fields = Def(defName).GetProperty("properties");
    Assert.Equal(type.GetProperties().Select(property => property.Name).Order(), fields.EnumerateObject()
        .Select(property => property.Value.GetProperty("x-modelProperty").GetString()).Order());
    foreach (PropertyInfo property in type.GetProperties()) {
      string key = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
      string parserType = property.PropertyType == typeof(bool) ? "bool"
          : property.PropertyType == typeof(ColorValue) ? "color"
          : property.PropertyType == typeof(int) ? "int" : "float";
      Assert.Equal(parserType, fields.GetProperty(key).GetProperty("x-parserType").GetString());
    }
  }

  [Theory]
  [InlineData("ScreenDefaults", "_defaultKeys")]
  [InlineData("Layer", "_layerKeys")]
  [InlineData("Layout", "_layoutKeys")]
  [InlineData("Entry", "_entryKeys")]
  [InlineData("Placement", "_placementKeys")]
  [InlineData("AnimatedPlacement", "_animatedPlacementKeys")]
  public void SchemaKeysMatchParserTables(string definition, string parserField) =>
      Assert.Equal(ParserKeys(parserField).Order(), Keys(definition).Order());

  public static IEnumerable<object[]> Fixtures() =>
      Directory.GetFiles(Path.Combine(FindRoot(), "tests", "fixtures", "config"), "*.yaml")
          .OrderBy(path => path, StringComparer.Ordinal)
          .Select(path => new object[] { Path.GetFileName(path) });

  [Theory]
  [MemberData(nameof(Fixtures))]
  public void FixtureProblemsMatchTheExpectedList(string file) {
    string folder = Path.Combine(_root, "tests", "fixtures", "config");
    string yaml = File.ReadAllText(Path.Combine(folder, file));
    ConfigLoadResult result = new ConfigParser(_ => true, DeathCauses.Names).Parse(yaml);
    string actual = string.Concat(result.Problems.Select(problem =>
        $"{problem.Line}\t{problem.Code}\t{problem.Detail}\n"));
    string expectedPath = Path.Combine(folder, Path.ChangeExtension(file, ".expected"));
    if (Environment.GetEnvironmentVariable("LOADINGSCREEN_UPDATE_FIXTURES") == "1")
      File.WriteAllText(expectedPath, actual, new UTF8Encoding(false));
    Assert.Equal(File.ReadAllText(expectedPath).Replace("\r\n", "\n"), actual);
    Assert.DoesNotContain(result.Problems, problem => string.IsNullOrEmpty(problem.Code));
  }

  [Fact]
  public void EveryProblemCodeHasASharedFixture() {
    HashSet<string> covered = new();
    string folder = Path.Combine(_root, "tests", "fixtures", "config");
    foreach (string path in Directory.GetFiles(folder, "*.yaml")) {
      ConfigLoadResult result = new ConfigParser(_ => true, DeathCauses.Names).Parse(File.ReadAllText(path));
      foreach (var problem in result.Problems) covered.Add(problem.Code);
    }
    string[] declared = [.. typeof(ProblemCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string) field.GetRawConstantValue()).Order()];
    Assert.Equal(declared, covered.Order());
  }
}
