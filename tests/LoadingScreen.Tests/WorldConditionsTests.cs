namespace LoadingScreen.Tests;

using System.Collections.Generic;

using LoadingScreen.Model;

using Xunit;

public class WorldConditionsTests {
  sealed class FakeContext(Dictionary<(ConditionKind, string), bool> facts) : IConditionContext {
    public bool? Check(ConditionKind kind, string name) {
      return facts.TryGetValue((kind, name), out bool value) ? value : null;
    }
  }

  static ConfigLoadResult Parse(string yaml) {
    return new ConfigParser(path => path == "mistlands/fog.jpg").Parse(yaml);
  }

  sealed class WorldContext(WorldState state) : IConditionContext {
    public bool? Check(ConditionKind kind, string name) => WorldConditions.Check(kind, name, state);
  }

  static List<Condition> ParseWhen(string when) {
    ConfigLoadResult result = Parse($"""
        when:
          general:
            - image: mistlands/fog.jpg
              when: {when}
        """);
    Assert.Empty(result.Warnings);
    return result.Config.General.Entries[0].Conditions;
  }

  [Fact]
  public void ParsesWorldConditions() {
    List<Condition> conditions = ParseWhen(
        "{ time: [Night, evening], weather: [Rain, ThunderStorm], sailing: false, minPlayers: 2, notMultiplayer: true }");

    Assert.Collection(
        conditions,
        condition => {
          Assert.Equal(ConditionKind.Time, condition.Kind);
          Assert.Equal("night", condition.Name);
          Assert.Equal(["evening"], condition.Alternatives);
        },
        condition => Assert.Equal(["ThunderStorm"], condition.Alternatives),
        condition => {
          Assert.Equal(ConditionKind.Sailing, condition.Kind);
          Assert.True(condition.Negated);
        },
        condition => Assert.Equal("2", condition.Name),
        condition => {
          Assert.Equal(ConditionKind.Multiplayer, condition.Kind);
          Assert.True(condition.Negated);
        });
  }

  [Theory]
  [InlineData("day", 0.3f, true)]
  [InlineData("day", 0.8f, false)]
  [InlineData("night", 0.1f, true)]
  [InlineData("night", 0.9f, true)]
  [InlineData("night", 0.5f, false)]
  [InlineData("morning", 0.3f, true)]
  [InlineData("morning", 0.6f, false)]
  [InlineData("evening", 0.6f, true)]
  [InlineData("evening", 0.2f, false)]
  public void TimeOfDayFollowsDayFraction(string time, float fraction, bool expected) {
    Assert.Equal(expected, WorldConditions.Check(ConditionKind.Time, time, new(DayFraction: fraction)));
  }

  [Fact]
  public void WorldConditionsMatchState() {
    WorldState state = new(0.6f, "Rain", true, 3, true);
    Assert.True(Conditions.AreMet(ParseWhen("{ weather: [Clear, rain], sailing: true, minPlayers: 3, maxPlayers: 3 }"),
        new WorldContext(state)));
    Assert.False(Conditions.AreMet(ParseWhen("{ weather: Snow }"), new WorldContext(state)));
    Assert.False(Conditions.AreMet(ParseWhen("{ minPlayers: 4 }"), new WorldContext(state)));
    Assert.False(Conditions.AreMet(ParseWhen("{ notSailing: true }"), new WorldContext(state)));
    Assert.False(Conditions.AreMet(ParseWhen("{ multiplayer: false }"), new WorldContext(state)));
    Assert.True(Conditions.AreMet(ParseWhen("{ time: [night, evening] }"), new WorldContext(state)));
  }

  [Theory]
  [InlineData("{ time: night }")]
  [InlineData("{ notTime: night }")]
  [InlineData("{ weather: Rain }")]
  [InlineData("{ notWeather: Rain }")]
  [InlineData("{ sailing: false }")]
  [InlineData("{ minPlayers: 1 }")]
  [InlineData("{ notMaxPlayers: 5 }")]
  [InlineData("{ multiplayer: true }")]
  public void WorldConditionsAreUnknownBeforeTheWorldLoads(string when) {
    Assert.False(Conditions.AreMet(ParseWhen(when), new WorldContext(default)));
    Assert.False(Conditions.AreMet(ParseWhen(when), new WorldContext(new(PlayerCount: 0))));
  }

  [Fact]
  public void AnyOfIsUnknownOnlyWhenNoAlternativeMatches() {
    Condition condition = new() { Kind = ConditionKind.Weather, Name = "Rain" };
    condition.Alternatives.Add("Snow");
    FakeContext partial = new(new() { [(ConditionKind.Weather, "Snow")] = true });
    Assert.True(Conditions.AreMet([condition], partial));
    FakeContext unknown = new(new() { [(ConditionKind.Weather, "Snow")] = false });
    Assert.False(Conditions.AreMet([condition], unknown));
    condition.Negated = true;
    Assert.False(Conditions.AreMet([condition], unknown));
  }
}
