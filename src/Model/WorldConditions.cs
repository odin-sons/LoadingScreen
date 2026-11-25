namespace LoadingScreen.Model;

using System;
using System.Globalization;

public readonly record struct WorldState(
    float? DayFraction = null, string Environment = null, bool? Sailing = null, int? PlayerCount = null,
    bool? Multiplayer = null);

public static class WorldConditions {
  public static bool? Check(ConditionKind kind, string name, WorldState state) => kind switch {
    ConditionKind.Time => state.DayFraction is float fraction && Enum.TryParse(name, true, out TimeOfDay time)
        ? IsTime(time, fraction) : null,
    ConditionKind.Weather => state.Environment == null ? null
        : string.Equals(state.Environment, name, StringComparison.OrdinalIgnoreCase),
    ConditionKind.Sailing => state.Sailing,
    ConditionKind.Multiplayer => state.Multiplayer,
    ConditionKind.MinPlayers => Count(name, state) is (int count, int limit) ? count >= limit : null,
    ConditionKind.MaxPlayers => Count(name, state) is (int count, int limit) ? count <= limit : null,
    _ => null,
  };

  public static bool IsWorldCondition(ConditionKind kind) =>
      Conditions.IsAnyOf(kind) || Conditions.IsFlag(kind) || Conditions.IsCount(kind);

  static bool IsTime(TimeOfDay time, float fraction) {
    bool day = fraction >= 0.25f && fraction <= 0.75f;
    return time switch {
      TimeOfDay.Day => day,
      TimeOfDay.Night => !day,
      TimeOfDay.Morning => day && fraction < 0.5f,
      TimeOfDay.Evening => day && fraction >= 0.5f,
      _ => false,
    };
  }

  static (int, int)? Count(string name, WorldState state) =>
      state.PlayerCount is int count && count > 0
          && int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit)
          ? (count, limit) : null;
}
