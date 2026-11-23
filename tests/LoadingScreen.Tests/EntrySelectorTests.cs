namespace LoadingScreen.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using LoadingScreen.Model;

using Xunit;

public class EntrySelectorTests {
  sealed class FakeBiomes : IBiomeResolver {
    public bool TryResolve(string name, out int biome) {
      biome = name.ToLowerInvariant() switch {
        "mistlands" => 8,
        "meadows" => 1,
        "moon" => 1024,
        "luna" => 1024,
        _ => 0,
      };
      return biome != 0;
    }

    public int GetBaseBiome(int biome) => biome == 1024 ? 1 : biome;
  }

  [Fact]
  public void TeleportMatchesResolvedBiomeValue() {
    ScreenConfig config = Config();
    config.Biome["Moon"] = List("biome.Moon", "moon.jpg");
    EntrySelector selector = new(config, biomes: new FakeBiomes());

    Assert.Equal("moon.jpg", selector.Select(Situation.Teleport(1024)).Entry.Image);
  }

  [Fact]
  public void DebugExplainsSkippedListAndGeneralFallback() {
    ScreenConfig config = Config();
    List<string> messages = [];
    EntrySelector selector = new(config, entry => entry.Image != "s1.jpg", debug: messages.Add);

    Selection selected = selector.Select(Situation.Sleep);

    Assert.Equal("general", selected.List.Name);
    Assert.Contains(messages, message => message.Contains("Skipped loading screen list")
        && message.Contains("screen=Sleep") && message.Contains("list=sleep"));
    Assert.Contains(messages, message => message.Contains("list=general")
        && message.Contains("reason=\"general fallback for Sleep\""));
  }

  [Fact]
  public void DebugExplainsUnmatchedSituationKey() {
    List<string> messages = [];
    EntrySelector selector = new(Config(), debug: messages.Add);

    selector.Select(Situation.Teleport("Plains"));

    Assert.Contains(messages, message => message.Contains("No biome loading screen list matched.")
        && message.Contains("biome=Plains"));
    Assert.Contains(messages, message => message.Contains("reason=\"general fallback for Teleport(Plains)\""));
  }

  [Fact]
  public void CustomBiomeFallsBackToBaseThenGeneral() {
    ScreenConfig config = Config();
    config.Biome["Luna"] = List("biome.Luna", "moon.jpg");
    config.Biome["Meadows"] = List("biome.Meadows", "meadows.jpg");
    EntrySelector selector = new(config, entry => entry.Image != "moon.jpg", biomes: new FakeBiomes());

    Assert.Equal("meadows.jpg", selector.Select(Situation.Teleport(1024)).Entry.Image);

    EntrySelector general = new(config, entry => entry.Image is not "moon.jpg" and not "meadows.jpg",
        biomes: new FakeBiomes());
    Assert.Equal("general", general.Select(Situation.Teleport(1024)).List.Name);
  }

  static EntryList List(string name, params string[] images) {
    EntryList list = new() { Name = name };
    list.Entries.AddRange(images.Select(image => new Entry { Image = image }));
    return list;
  }

  static ScreenConfig Config() {
    ScreenConfig config = new() { General = List("general", "g1.jpg", "g2.jpg") };
    config.Sleep = List("sleep", "s1.jpg");
    config.Biome["Mistlands"] = List("biome.Mistlands", "m1.jpg");
    config.Death["Fall"] = List("death.Fall", "f1.jpg", "f2.jpg", "f3.jpg");
    return config;
  }

  [Fact]
  public void TeleportPrefersBiomeList() {
    Selection selection = new EntrySelector(Config()).Select(Situation.Teleport("mistlands"));

    Assert.Equal("m1.jpg", selection.Entry.Image);
    Assert.Equal("biome.Mistlands", selection.List.Name);
  }

  [Fact]
  public void DeathPrefersCauseList() {
    Selection selection = new EntrySelector(Config()).Select(Situation.Death("Fall"));

    Assert.StartsWith("f", selection.Entry.Image);
  }

  [Fact]
  public void SleepPrefersItsOwnListAndFallsBackToGeneral() {
    ScreenConfig config = Config();
    Assert.Equal("s1.jpg", new EntrySelector(config).Select(Situation.Sleep).Entry.Image);

    config.Sleep.Entries.Clear();
    Assert.Equal("general", new EntrySelector(config).Select(Situation.Sleep).List.Name);
  }

  [Fact]
  public void SleepFallsBackWhenItsEntriesAreUnusable() {
    EntrySelector selector = new(Config(), entry => entry.Image != "s1.jpg");

    Assert.Equal("general", selector.Select(Situation.Sleep).List.Name);
  }

  [Fact]
  public void UnknownKeyFallsBackToGeneral() {
    EntrySelector selector = new(Config());

    Assert.Equal("general", selector.Select(Situation.Teleport("Plains")).List.Name);
    Assert.Equal("general", selector.Select(Situation.Death("Drowning")).List.Name);
    Assert.Equal("general", selector.Select(Situation.General).List.Name);
  }

  [Fact]
  public void UnusableEntriesFallBackToNextLevel() {
    EntrySelector selector = new(Config(), entry => entry.Image != "m1.jpg");

    Assert.Equal("general", selector.Select(Situation.Teleport("Mistlands")).List.Name);
  }

  [Fact]
  public void NoUsableEntryReturnsNull() {
    Assert.Null(new EntrySelector(new ScreenConfig()).Select(Situation.General));
    Assert.Null(new EntrySelector(Config(), _ => false).Select(Situation.Death("Fall")));
  }

  [Fact]
  public void SingleEntryRepeats() {
    EntrySelector selector = new(Config());

    for (int i = 0; i < 5; i++) {
      Assert.Equal("m1.jpg", selector.Select(Situation.Teleport("Mistlands")).Entry.Image);
    }
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  public void NeverRepeatsImmediately(int count) {
    ScreenConfig config = new() { General = List("general", Enumerable.Range(0, count).Select(i => $"{i}.jpg").ToArray()) };
    EntrySelector selector = new(config, random: new Random(7));
    HashSet<string> seen = [];
    string previous = null;

    for (int i = 0; i < 200; i++) {
      string image = selector.Select(Situation.General).Entry.Image;
      Assert.NotEqual(previous, image);
      seen.Add(image);
      previous = image;
    }

    Assert.Equal(count, seen.Count);
  }

  [Fact]
  public void NoRepeatIsTrackedPerList() {
    EntrySelector selector = new(Config(), random: new Random(1));
    string general = selector.Select(Situation.General).Entry.Image;

    selector.Select(Situation.Death("Fall"));

    Assert.NotEqual(general, selector.Select(Situation.General).Entry.Image);
  }

  [Fact]
  public void LayoutComesFromEntryThenList() {
    ScreenConfig config = Config();
    config.General.LayoutName = "list-layout";
    config.General.Entries[0].LayoutName = "entry-layout";
    config.General.Entries.RemoveAt(1);

    Assert.Equal("entry-layout", new EntrySelector(config).Select(Situation.General).LayoutName);

    config.General.Entries[0].LayoutName = null;
    Assert.Equal("list-layout", new EntrySelector(config).Select(Situation.General).LayoutName);
  }
  [Fact]
  public void SequentialOrderFollowsConfigAndSkipsUnusable() {
    ScreenConfig config = new() { General = List("general", "a.jpg", "b.jpg", "c.jpg"), Order = PickOrder.Sequential };
    EntrySelector selector = new(config, entry => entry.Image != "b.jpg");

    string[] picks = [.. Enumerable.Range(0, 4).Select(_ => selector.Select(Situation.General).Entry.Image)];

    Assert.Equal(["a.jpg", "c.jpg", "a.jpg", "c.jpg"], picks);
  }

  [Fact]
  public void ListOrderOverridesGlobalOrder() {
    ScreenConfig config = Config();
    config.Death["Fall"].Order = PickOrder.Sequential;

    EntrySelector selector = new(config);
    string[] picks = [.. Enumerable.Range(0, 3).Select(_ => selector.Select(Situation.Death("Fall")).Entry.Image)];

    Assert.Equal(["f1.jpg", "f2.jpg", "f3.jpg"], picks);
  }

  [Fact]
  public void NextSelectionStaysInTheOriginalSituationalList() {
    ScreenConfig config = Config();
    config.Biome["Mistlands"] = List("biome.Mistlands", "m1.jpg", "m2.jpg");
    config.Biome["Mistlands"].Order = PickOrder.Sequential;
    EntrySelector selector = new(config);
    Selection first = selector.Select(Situation.Teleport("Mistlands"));

    Selection second = selector.SelectNext(first, Situation.Teleport("Mistlands"));
    Selection third = selector.SelectNext(second, Situation.Teleport("Mistlands"));

    Assert.Same(first.List, second.List);
    Assert.Equal(["m1.jpg", "m2.jpg", "m1.jpg"], new[] { first.Entry.Image, second.Entry.Image, third.Entry.Image });
  }

  [Fact]
  public void NextSelectionRespectsRandomOrderWithoutRepeats() {
    ScreenConfig config = Config();
    config.Death["Fall"].Order = PickOrder.Random;
    EntrySelector selector = new(config, random: new Random(7));
    Selection current = selector.Select(Situation.Death("Fall"));

    for (int i = 0; i < 100; i++) {
      Selection next = selector.SelectNext(current, Situation.Death("Fall"));
      Assert.Same(current.List, next.List);
      Assert.NotEqual(current.Entry, next.Entry);
      current = next;
    }
  }

  [Fact]
  public void NextSelectionDoesNotFallBackWhenItsListBecomesUnusable() {
    ScreenConfig config = Config();
    bool usable = true;
    EntrySelector selector = new(config, entry => entry.Image != "m1.jpg" || usable);
    Selection current = selector.Select(Situation.Teleport("Mistlands"));
    usable = false;

    Assert.Null(selector.SelectFromList(current.List));
  }

  [Fact]
  public void NextSelectionUsesTheNewConfigAfterReload() {
    ScreenConfig previousConfig = Config();
    Selection previous = new EntrySelector(previousConfig).Select(Situation.Teleport("Mistlands"));
    ScreenConfig currentConfig = Config();
    currentConfig.Biome["Mistlands"] = List("biome.Mistlands", "new.jpg");
    EntrySelector selector = new(currentConfig);

    Selection next = selector.SelectNext(previous, Situation.Teleport("Mistlands"));

    Assert.Same(currentConfig.Biome["Mistlands"], next.List);
    Assert.Equal("new.jpg", next.Entry.Image);
  }

  [Fact]
  public void DeathTriesEnemyDamageHitBiomeBaseAndGeneralInOrder() {
    ScreenConfig config = new();
    config.General.Entries.Add(new Entry { Image = "general.jpg" });
    config.Enemy["Dragon"] = List("enemy.Dragon", "dragon.jpg");
    config.Death["Frost"] = List("death.Frost", "frost.jpg");
    config.Death["EnemyHit"] = List("death.EnemyHit", "hit.jpg");
    config.Biome["Moon"] = List("biome.Moon", "moon.jpg");
    config.Biome["Meadows"] = List("biome.Meadows", "meadows.jpg");
    Situation death = Situation.Death(["Frost", "EnemyHit"], "Dragon", 1024);
    string[] priority = ["dragon.jpg", "frost.jpg", "hit.jpg", "moon.jpg", "meadows.jpg", "general.jpg"];

    for (int i = 0; i < priority.Length; i++) {
      HashSet<string> skipped = [.. priority.Take(i)];
      EntrySelector selector = new(config, entry => !skipped.Contains(entry.Image), biomes: new FakeBiomes());
      Assert.Equal(priority[i], selector.Select(death).Entry.Image);
    }
  }

  [Fact]
  public void DeathWithoutSpecificListsUsesBiomeThenBaseThenGeneral() {
    ScreenConfig config = Config();
    config.Biome["Luna"] = List("biome.Luna", "moon.jpg");
    config.Biome["Meadows"] = List("biome.Meadows", "meadows.jpg");
    Situation death = Situation.Death(["Slash", "EnemyHit"], "Dragon", 1024);

    Assert.Equal("moon.jpg", new EntrySelector(config, biomes: new FakeBiomes()).Select(death).Entry.Image);
    Assert.Equal("meadows.jpg",
        new EntrySelector(config, entry => entry.Image != "moon.jpg", biomes: new FakeBiomes()).Select(death).Entry.Image);
    Assert.Equal("general",
        new EntrySelector(config, entry => entry.Image is not "moon.jpg" and not "meadows.jpg",
            biomes: new FakeBiomes()).Select(death).List.Name);
  }
}

public class DeathCausesTests {
  static EntryList List(string name, string image) {
    EntryList list = new() { Name = name };
    list.Entries.Add(new Entry { Image = image });
    return list;
  }

  [Fact]
  public void EnemyHitPrefersDominantDamageType() {
    string[] keys = DeathCauses.Keys("EnemyHit", new Dictionary<string, float> { ["Slash"] = 10f, ["Frost"] = 30f });

    Assert.Equal(["Frost", "EnemyHit"], keys);
  }

  [Fact]
  public void EnemyIsCarriedSeparatelyFromDamageAndHitType() {
    string[] keys = DeathCauses.Keys("EnemyHit", new Dictionary<string, float> { ["Frost"] = 30f });
    Situation situation = Situation.Death(keys, "Dragon");

    Assert.Equal(["Frost", "EnemyHit"], situation.Keys);
    Assert.Equal("Dragon", situation.AttackerPrefab);
  }

  [Fact]
  public void EnvironmentalDeathIgnoresDamageType() {
    Assert.Equal(["Fall"], DeathCauses.Keys("Fall", new Dictionary<string, float> { ["Blunt"] = 50f }));
  }

  [Fact]
  public void DeathCauseKeysSelectMostSpecificList() {
    ScreenConfig config = new();
    config.General.Entries.Add(new Entry { Image = "g.jpg" });
    config.Death["EnemyHit"] = new EntryList { Name = "death.EnemyHit" };
    config.Death["EnemyHit"].Entries.Add(new Entry { Image = "enemy.jpg" });
    config.Death["Frost"] = new EntryList { Name = "death.Frost" };
    config.Death["Frost"].Entries.Add(new Entry { Image = "frost.jpg" });
    EntrySelector selector = new(config);

    Assert.Equal("frost.jpg", selector.Select(Situation.Death("Frost", "EnemyHit")).Entry.Image);
    Assert.Equal("enemy.jpg", selector.Select(Situation.Death("Slash", "EnemyHit")).Entry.Image);
    Assert.Equal("g.jpg", selector.Select(Situation.Death("Fall")).Entry.Image);
  }

  [Fact]
  public void EnemyListPrecedesDamageAndHitTypeAndFallsBackWhenUnusable() {
    ScreenConfig config = new();
    config.General.Entries.Add(new Entry { Image = "general.jpg" });
    config.Enemy["Dragon"] = List("enemy.Dragon", "dragon.jpg");
    config.Death["Frost"] = List("death.Frost", "frost.jpg");
    config.Death["EnemyHit"] = List("death.EnemyHit", "hit.jpg");
    Situation death = Situation.Death(["Frost", "EnemyHit"], "dragon");

    Assert.Equal("dragon.jpg", new EntrySelector(config).Select(death).Entry.Image);
    Assert.Equal("frost.jpg", new EntrySelector(config, entry => entry.Image != "dragon.jpg").Select(death).Entry.Image);
    Assert.Equal("hit.jpg", new EntrySelector(config, entry => entry.Image != "dragon.jpg" && entry.Image != "frost.jpg").Select(death).Entry.Image);
    Assert.Equal("general.jpg", new EntrySelector(config, entry => entry.Image == "general.jpg").Select(death).Entry.Image);
  }
}
