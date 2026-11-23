namespace LoadingScreen.Tests;

using System.Linq;

using LoadingScreen.Model;

using Xunit;

public class BuiltinConfigTests {
  [Fact]
  public void BuiltinTipsParseWithTranslationsAndProgressGates() {
    ConfigLoadResult result = new ConfigParser(_ => true, DeathCauses.Names).Parse(BuiltinConfigText.Text);

    Assert.True(result.IsValid);
    Assert.Empty(result.Warnings);
    Assert.True(result.Config.General.Entries.Count >= 40);
    Assert.Equal(KnownNames.Biomes.Count, result.Config.Biome.Count);
    Assert.All(result.Config.Biome.Values, list => Assert.InRange(list.Entries.Count, 2, 4));
    Assert.Equal(DeathCauses.Names.Count, result.Config.Death.Count);
    Assert.True(result.Config.Sleep.Entries.Count >= 3);
    Assert.All(result.Config.AllLists.SelectMany(list => list.Entries), entry => {
      Assert.Null(entry.Image);
      Assert.False(string.IsNullOrWhiteSpace(entry.Translations["en"]));
      Assert.False(string.IsNullOrWhiteSpace(entry.Translations["ru"]));
    });

    string[] bossKeys = ["defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon",
        "defeated_goblinking", "defeated_queen", "defeated_fader"];
    foreach (string boss in bossKeys) {
      Assert.Contains(result.Config.General.Entries,
          entry => entry.Conditions.Any(condition => condition.Kind == ConditionKind.GlobalKey
              && condition.Name == boss));
    }
    Assert.All(result.Config.General.Entries.Skip(8), entry =>
        Assert.Contains(entry.Conditions, condition => condition.Kind == ConditionKind.GlobalKey));
  }

  [Fact]
  public void BuiltinTipsOnlyReplaceAnEmptyUserConfigWhenEnabled() {
    ConfigLoadResult empty = new ConfigParser(_ => true).Parse(ExampleConfigText.Text);
    ConfigLoadResult custom = new ConfigParser(_ => true).Parse("when: { sleep: [ { text: Custom } ] }");
    ConfigLoadResult invalidEntry = new ConfigParser(_ => true).Parse("when: { general: [ { text: '' } ] }");

    Assert.True(BuiltinTipsSelection.ShouldUse(empty, true));
    Assert.False(BuiltinTipsSelection.ShouldUse(empty, false));
    Assert.False(BuiltinTipsSelection.ShouldUse(custom, true));
    Assert.False(BuiltinTipsSelection.ShouldUse(invalidEntry, true));
  }
}
