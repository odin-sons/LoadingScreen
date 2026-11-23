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
  static readonly JsonElement _options = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "site", "data", "options.json"))).RootElement;

  static string FindRoot() {
    for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent) {
      if (File.Exists(Path.Combine(directory.FullName, "site", "data", "options.json"))) return directory.FullName;
    }
    throw new InvalidOperationException("site/data/options.json not found above the test output folder");
  }

  static string[] Strings(string name) => [.. _options.GetProperty(name).EnumerateArray().Select(item => item.GetString())];

  static Dictionary<string, JsonElement> Map(JsonElement element) =>
      element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);

  static string Kebab(string name) =>
      string.Concat(name.Select((character, index) => index > 0 && char.IsUpper(character)
          ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));

  static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

  [Fact]
  public void OptionListsMatchTheParser() {
    Assert.Equal(KnownNames.Biomes, Strings("biomes"));
    Assert.Equal(DeathCauses.Names, Strings("deathCauses"));
    Assert.Equal(new AnimationCatalog().Presets.Keys.Order(), Strings("animations").Order());
    Assert.Equal(Enum.GetNames(typeof(Anchor)).Select(Kebab), Strings("anchors"));
    string[] kinds = Enum.GetNames(typeof(ConditionKind));
    Assert.Equal(kinds.Select(Camel).Concat(kinds.Select(kind => "not" + kind)), Strings("conditions"));
    Assert.Equal(
        LanguageText.GameCodes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        Map(_options.GetProperty("languages")).Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value.GetString()))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal));
  }

  [Theory]
  [InlineData("anchor", typeof(Anchor))]
  [InlineData("fitMode", typeof(FitMode))]
  [InlineData("sides", typeof(Sides))]
  [InlineData("fadeCurve", typeof(FadeCurve))]
  [InlineData("progressStyle", typeof(ProgressStyle))]
  [InlineData("textAlign", typeof(TextAlign))]
  [InlineData("conditionKind", typeof(ConditionKind))]
  public void EnumTablesMatchTheParser(string name, Type type) {
    Dictionary<string, JsonElement> table = Map(_options.GetProperty("enums").GetProperty(name));
    Assert.Equal(Enum.GetNames(type), table.Keys);
    foreach (string member in Enum.GetNames(type)) {
      Assert.Equal(Convert.ToInt32(Enum.Parse(type, member)), table[member].GetInt32());
    }
  }

  [Theory]
  [InlineData("tip", typeof(TipDefaults))]
  [InlineData("continuePrompt", typeof(ContinuePromptDefaults))]
  [InlineData("separator", typeof(SeparatorDefaults))]
  [InlineData("spinner", typeof(SpinnerDefaults))]
  public void DefaultSectionTablesMatchTheParser(string section, Type type) {
    Dictionary<string, string> expected = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(
        property => property.Name,
        property => property.PropertyType == typeof(bool) ? "bool"
            : property.PropertyType == typeof(ColorValue) ? "color"
            : property.PropertyType == typeof(int) ? "int" : "float");
    Dictionary<string, string> actual = Map(_options.GetProperty("defaultSections").GetProperty(section))
        .ToDictionary(pair => pair.Key, pair => pair.Value.GetString());
    Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
  }

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
    string actual = string.Concat(result.Problems.Select(problem => $"{problem.Line}\t{problem.Detail}\n"));
    string expectedPath = Path.Combine(folder, Path.ChangeExtension(file, ".expected"));

    if (Environment.GetEnvironmentVariable("LOADINGSCREEN_UPDATE_FIXTURES") == "1") {
      File.WriteAllText(expectedPath, actual, new UTF8Encoding(false));
    }

    Assert.Equal(File.ReadAllText(expectedPath).Replace("\r\n", "\n"), actual);
  }
}
