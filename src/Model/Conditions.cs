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
}

public sealed class Condition {
  public ConditionKind Kind { get; set; }
  public string Name { get; set; }
  public bool Negated { get; set; }

  public override string ToString() {
    return $"{(Negated ? "not " : string.Empty)}{Kind} {Name}";
  }
}

public interface IConditionContext {
  bool? Check(ConditionKind kind, string name);
}

public static class Conditions {
  public static readonly IReadOnlyDictionary<string, ConditionKind> Keys =
      new Dictionary<string, ConditionKind>(StringComparer.OrdinalIgnoreCase) {
        ["knownItem"] = ConditionKind.KnownItem,
        ["hasItem"] = ConditionKind.HasItem,
        ["knownRecipe"] = ConditionKind.KnownRecipe,
        ["knownBiome"] = ConditionKind.KnownBiome,
        ["globalKey"] = ConditionKind.GlobalKey,
        ["playerKey"] = ConditionKind.PlayerKey,
      };

  const string NegationPrefix = "not";

  public static bool TryParseKey(string key, out ConditionKind kind, out bool negated) {
    negated = false;

    if (string.IsNullOrEmpty(key)) {
      kind = default;
      return false;
    }

    if (Keys.TryGetValue(key, out kind)) {
      return true;
    }

    negated = key.StartsWith(NegationPrefix, StringComparison.OrdinalIgnoreCase);
    return negated && Keys.TryGetValue(key.Substring(NegationPrefix.Length), out kind);
  }

  public static bool AreMet(IReadOnlyList<Condition> conditions, IConditionContext context) {
    if (conditions == null || conditions.Count == 0) {
      return true;
    }

    if (context == null) {
      return false;
    }

    foreach (Condition condition in conditions) {
      bool? result = context.Check(condition.Kind, condition.Name);

      if (result == null || result.Value == condition.Negated) {
        return false;
      }
    }

    return true;
  }
}
