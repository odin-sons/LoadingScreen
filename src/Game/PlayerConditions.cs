namespace LoadingScreen;

using System;
using System.Collections.Generic;
using System.Linq;

using global::LoadingScreen.Model;

using UnityEngine;

public sealed class PlayerConditions : IConditionContext {
  public static PlayerConditions Instance { get; } = new();

  readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> _reportedUnknown = new(StringComparer.OrdinalIgnoreCase);

  public bool? Check(ConditionKind kind, string name) {
    if (kind == ConditionKind.GlobalKey) {
      return ZoneSystem.instance ? ZoneSystem.instance.GetGlobalKey(name) : null;
    }

    Player player = Player.m_localPlayer;

    if (!player) {
      return null;
    }

    switch (kind) {
      case ConditionKind.PlayerKey:
        return player.HaveUniqueKey(name);

      case ConditionKind.KnownBiome:
        return BiomeResolver.Instance.TryResolve(name, out int biome)
            ? IsBiomeKnown(player, (Heightmap.Biome)biome) : null;
    }

    string itemName = ItemName(name);

    if (itemName == null) {
      return null;
    }

    return kind switch {
      ConditionKind.KnownItem => player.IsMaterialKnown(itemName),
      ConditionKind.KnownRecipe => player.IsRecipeKnown(itemName),
      ConditionKind.HasItem => player.GetInventory()?.HaveItem(itemName) ?? false,
      _ => null,
    };
  }

  static bool IsBiomeKnown(Player player, Heightmap.Biome biome) {
    string token = BiomeSector.GetBiomeName(biome);
    string localized = Localization.instance?.Localize(token) ?? token;
    string numericToken = "$biome_" + ((int)biome).ToString();
    string customName = BiomeResolver.Instance.TryGetDisplayName((int)biome, out string ewdName)
        ? ewdName : null;

    return player.m_knownBiome.Any(
        known => known == token || known == numericToken
            || (!string.IsNullOrEmpty(localized)
                && known.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0)
            || (customName != null && string.Equals(known, customName, StringComparison.OrdinalIgnoreCase)));
  }

  public void ClearCache() {
    _itemNames.Clear();
  }

  string ItemName(string name) {
    if (name.StartsWith("$")) {
      return name;
    }

    if (_itemNames.TryGetValue(name, out string cached)) {
      return cached;
    }

    if (!ObjectDB.instance) {
      return null;
    }

    GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
    string itemName = prefab && prefab.TryGetComponent(out ItemDrop itemDrop) ? itemDrop.m_itemData.m_shared.m_name : null;

    if (itemName == null && _reportedUnknown.Add(name)) {
      LoadingScreen.LogWarning($"Unknown item '{name}' in a 'when' condition; use the item prefab name, like Demister.");
    }

    _itemNames[name] = itemName;
    return itemName;
  }
}
