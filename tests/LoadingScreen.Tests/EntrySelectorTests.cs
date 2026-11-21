namespace LoadingScreen.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using LoadingScreen.Model;

using Xunit;

public class EntrySelectorTests {
  static EntryList List(string name, params string[] images) {
    EntryList list = new() { Name = name };
    list.Entries.AddRange(images.Select(image => new Entry { Image = image }));
    return list;
  }

  static ScreenConfig Config() {
    ScreenConfig config = new() { General = List("general", "g1.jpg", "g2.jpg") };
    config.Teleport["Mistlands"] = List("teleport.Mistlands", "m1.jpg");
    config.Death["Fall"] = List("death.Fall", "f1.jpg", "f2.jpg", "f3.jpg");
    return config;
  }

  [Fact]
  public void TeleportPrefersBiomeList() {
    Selection selection = new EntrySelector(Config()).Select(Situation.Teleport("mistlands"));

    Assert.Equal("m1.jpg", selection.Entry.Image);
    Assert.Equal("teleport.Mistlands", selection.List.Name);
  }

  [Fact]
  public void DeathPrefersCauseList() {
    Selection selection = new EntrySelector(Config()).Select(Situation.Death("Fall"));

    Assert.StartsWith("f", selection.Entry.Image);
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
    config.Teleport["Mistlands"] = List("teleport.Mistlands", "m1.jpg", "m2.jpg");
    config.Teleport["Mistlands"].Order = PickOrder.Sequential;
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
    currentConfig.Teleport["Mistlands"] = List("teleport.Mistlands", "new.jpg");
    EntrySelector selector = new(currentConfig);

    Selection next = selector.SelectNext(previous, Situation.Teleport("Mistlands"));

    Assert.Same(currentConfig.Teleport["Mistlands"], next.List);
    Assert.Equal("new.jpg", next.Entry.Image);
  }
}

public class DeathCausesTests {
  [Fact]
  public void EnemyHitPrefersDominantDamageType() {
    string[] keys = DeathCauses.Keys("EnemyHit", new Dictionary<string, float> { ["Slash"] = 10f, ["Frost"] = 30f });

    Assert.Equal(["Frost", "EnemyHit"], keys);
  }

  [Fact]
  public void EnemyComesBeforeDamageTypeAndHitType() {
    string[] keys = DeathCauses.Keys("EnemyHit", new Dictionary<string, float> { ["Frost"] = 30f }, "Dragon");

    Assert.Equal(["enemy:Dragon", "Frost", "EnemyHit"], keys);
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
}
