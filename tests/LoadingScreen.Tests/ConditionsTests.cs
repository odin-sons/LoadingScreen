namespace LoadingScreen.Tests;

using System.Collections.Generic;
using System.Linq;

using LoadingScreen.Screens;

using Xunit;

public class ConditionsTests {
  sealed class FakeContext(Dictionary<(ConditionKind, string), bool> facts) : IConditionContext {
    public bool? Check(ConditionKind kind, string name) {
      return facts.TryGetValue((kind, name), out bool value) ? value : null;
    }
  }

  static ConfigLoadResult Parse(string yaml) {
    return new ConfigParser(path => path == "mistlands/fog.jpg").Parse(yaml);
  }

  const string MistTip = """
      situations:
        teleport:
          Mistlands:
            - text: Craft a Wisplight to push back the mist.
              when:
                notKnownItem: Demister
                knownBiome: mistlands
            - image: mistlands/fog.jpg
              when:
                globalKey: [defeated_queen, defeated_fader]
      """;

  [Fact]
  public void ParsesConditionsAndTextOnlyEntries() {
    ConfigLoadResult result = Parse(MistTip);
    List<Entry> entries = result.Config.Teleport["Mistlands"].Entries;

    Assert.Empty(result.Warnings);
    Assert.Equal(2, entries.Count);

    Entry tip = entries[0];
    Assert.Null(tip.Image);
    Assert.Equal("Craft a Wisplight to push back the mist.", tip.Text);
    Assert.Collection(
        tip.Conditions,
        condition => {
          Assert.Equal(ConditionKind.KnownItem, condition.Kind);
          Assert.Equal("Demister", condition.Name);
          Assert.True(condition.Negated);
        },
        condition => {
          Assert.Equal(ConditionKind.KnownBiome, condition.Kind);
          Assert.Equal("Mistlands", condition.Name);
          Assert.False(condition.Negated);
        });

    Assert.Equal(["defeated_queen", "defeated_fader"], entries[1].Conditions.Select(condition => condition.Name));
  }

  [Theory]
  [InlineData("when: { knowsEverything: Demister }", "unknown condition")]
  [InlineData("when: { knownBiome: Moon }", "unknown biome")]
  [InlineData("when: { knownItem: '' }", "needs a name")]
  [InlineData("when: [ knownItem ]", "expected a mapping")]
  public void RejectsEntriesWithInvalidConditions(string when, string warning) {
    ConfigLoadResult result = Parse($"situations: {{ general: [ {{ text: Tip, {when} }} ] }}");

    Assert.Empty(result.Config.General.Entries);
    Assert.Contains(result.Warnings, message => message.Contains(warning));
  }

  [Fact]
  public void EntryNeedsImageOrText() {
    ConfigLoadResult result = Parse("situations: { general: [ { layout: x } ] }");

    Assert.Empty(result.Config.General.Entries);
    Assert.Contains(result.Warnings, message => message.Contains("needs an 'image' or a 'text'"));
  }

  [Fact]
  public void AllConditionsMustHold() {
    List<Condition> conditions = [
      new() { Kind = ConditionKind.KnownItem, Name = "Demister", Negated = true },
      new() { Kind = ConditionKind.GlobalKey, Name = "defeated_queen" },
    ];

    FakeContext newcomer = new(new() {
      [(ConditionKind.KnownItem, "Demister")] = false,
      [(ConditionKind.GlobalKey, "defeated_queen")] = true,
    });
    FakeContext veteran = new(new() {
      [(ConditionKind.KnownItem, "Demister")] = true,
      [(ConditionKind.GlobalKey, "defeated_queen")] = true,
    });

    Assert.True(Conditions.AreMet(conditions, newcomer));
    Assert.False(Conditions.AreMet(conditions, veteran));
  }

  [Fact]
  public void UnknownFactsAndMissingContextFailConditions() {
    List<Condition> conditions = [new() { Kind = ConditionKind.PlayerKey, Name = "x", Negated = true }];

    Assert.False(Conditions.AreMet(conditions, new FakeContext([])));
    Assert.False(Conditions.AreMet(conditions, null));
    Assert.True(Conditions.AreMet([], null));
  }

  [Fact]
  public void SelectorSkipsEntriesWhoseConditionsFail() {
    ScreenConfig config = Parse(MistTip).Config;
    FakeContext veteran = new(new() {
      [(ConditionKind.KnownItem, "Demister")] = true,
      [(ConditionKind.KnownBiome, "Mistlands")] = true,
      [(ConditionKind.GlobalKey, "defeated_queen")] = true,
      [(ConditionKind.GlobalKey, "defeated_fader")] = true,
    });

    EntrySelector selector = new(config, entry => Conditions.AreMet(entry.Conditions, veteran));

    for (int i = 0; i < 5; i++) {
      Assert.Equal("mistlands/fog.jpg", selector.Select(Situation.Teleport("Mistlands")).Entry.Image);
    }
  }
}

public class ReadmeConditionExamplesTests {
  static string FindReadme() {
    for (System.IO.DirectoryInfo dir = new(System.AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
      string path = System.IO.Path.Combine(dir.FullName, "README.md");

      if (System.IO.File.Exists(path) && System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "manifest.json"))) {
        return path;
      }
    }

    return null;
  }

  [Fact]
  public void ConditionExamplesParseWithoutWarnings() {
    string readme = System.IO.File.ReadAllText(FindReadme());
    List<string> blocks = System.Text.RegularExpressions.Regex
        .Matches(readme, "```yaml\r?\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline)
        .Select(match => match.Groups[1].Value)
        .Where(block => block.Contains("when:"))
        .ToList();

    Assert.True(blocks.Count >= 3);

    foreach (string block in blocks) {
      ConfigLoadResult result = new ConfigParser(_ => true, DeathCauses.Names).Parse(block);

      Assert.Empty(result.Warnings);
      Assert.True(result.Config.EntryCount > 0);
    }
  }
}
