namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;

public enum ConditionKind {
  KnownItem,
  HasItem,
  KnownRecipe,
  KnownBiome,
  GlobalKey,
  PlayerKey,
  Time,
  Weather,
  Sailing,
  MinPlayers,
  MaxPlayers,
  Multiplayer,
}

public enum TimeOfDay {
  Day,
  Night,
  Morning,
  Evening,
}

public sealed class Condition {
  public ConditionKind Kind { get; set; }
  public string Name { get; set; }
  public bool Negated { get; set; }
  public List<string> Alternatives { get; } = [];

  public override string ToString() {
    string names = Alternatives.Count == 0 ? Name : Name + "|" + string.Join("|", Alternatives);
    return $"{(Negated ? "not " : string.Empty)}{Kind} {names}";
  }
}

public interface IConditionContext {
  bool? Check(ConditionKind kind, string name);
}

public static class Conditions {
  const string NegationPrefix = "not";
  public const int MaxPlayerCount = 999;

  public static bool IsAnyOf(ConditionKind kind) => kind is ConditionKind.Time or ConditionKind.Weather;

  public static bool IsFlag(ConditionKind kind) => kind is ConditionKind.Sailing or ConditionKind.Multiplayer;

  public static bool IsCount(ConditionKind kind) => kind is ConditionKind.MinPlayers or ConditionKind.MaxPlayers;

  public static bool TryParseKey(string key, out ConditionKind kind, out bool negated) {
    negated = false;

    if (string.IsNullOrEmpty(key)) {
      kind = default;
      return false;
    }

    if (Enum.TryParse(key, ignoreCase: true, out kind) && Enum.IsDefined(typeof(ConditionKind), kind)) {
      return true;
    }

    negated = key.StartsWith(NegationPrefix, StringComparison.OrdinalIgnoreCase);
    return negated && Enum.TryParse(key.Substring(NegationPrefix.Length), ignoreCase: true, out kind)
        && Enum.IsDefined(typeof(ConditionKind), kind);
  }

  public static bool AreMet(IReadOnlyList<Condition> conditions, IConditionContext context) {
    if (conditions == null || conditions.Count == 0) {
      return true;
    }

    if (context == null) {
      return false;
    }

    foreach (Condition condition in conditions) {
      bool? result = Check(condition, context);

      if (result == null || result.Value == condition.Negated) {
        return false;
      }
    }

    return true;
  }

  static bool? Check(Condition condition, IConditionContext context) {
    bool? result = context.Check(condition.Kind, condition.Name);

    foreach (string alternative in condition.Alternatives) {
      if (result == true) {
        break;
      }

      bool? next = context.Check(condition.Kind, alternative);
      result = next == true ? true : result == null || next == null ? null : false;
    }

    return result;
  }
}
