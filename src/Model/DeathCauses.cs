namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public static class DeathCauses {
  public static readonly IReadOnlyList<string> HitTypes = [
    "Undefined", "EnemyHit", "PlayerHit", "Fall", "Drowning", "Burning", "Freezing", "Poisoned", "Water", "Smoke",
    "EdgeOfWorld", "Impact", "Cart", "Tree", "Self", "Structural", "Turret", "Boat", "Stalagtite", "Catapult",
    "CinderFire", "AshlandsOcean", "AshlandsLava", "Incinerator", "DrawBridge"
  ];

  public static readonly IReadOnlyList<string> DamageTypes = [
    "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit"
  ];

  public static readonly IReadOnlyList<string> Names = [.. HitTypes, .. DamageTypes];

  static readonly HashSet<string> _hitTypesWithDamageType =
      new(["Undefined", "EnemyHit", "PlayerHit", "Turret", "Catapult"], StringComparer.OrdinalIgnoreCase);

  public const string EnemySection = "enemy";

  public static string EnemyKey(string prefabName) => $"{EnemySection}:{prefabName}";

  public static string[] Keys(
      string hitType, IEnumerable<KeyValuePair<string, float>> damages, string attackerPrefab = null) {
    List<string> keys = [];

    if (!string.IsNullOrEmpty(attackerPrefab)) {
      keys.Add(EnemyKey(attackerPrefab));
    }

    if (hitType != null && _hitTypesWithDamageType.Contains(hitType)) {
      KeyValuePair<string, float> dominant =
          (damages ?? [])
              .Where(pair => pair.Value > 0f && DamageTypes.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
              .OrderByDescending(pair => pair.Value)
              .FirstOrDefault();

      if (dominant.Key != null) {
        keys.Add(DamageTypes.First(name => string.Equals(name, dominant.Key, StringComparison.OrdinalIgnoreCase)));
      }
    }

    if (hitType != null) {
      keys.Add(hitType);
    }

    return [.. keys];
  }
}
