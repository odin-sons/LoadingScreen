namespace LoadingScreen;

using System.Collections.Generic;

using global::LoadingScreen.Model;

using UnityEngine;

public static class SituationTracker {
  const float PendingLifetimeSeconds = 120f;

  static Situation? _pending;
  static Selection _preselected;
  static float _pendingSince;

  public static void OnTeleport(Vector3 targetPosition) {
    if (!PluginConfig.ShowLoadingScreenOnTeleport.Value) {
      return;
    }

    int? biome = ResolveBiome(targetPosition);
    Set(biome.HasValue ? Situation.Teleport(biome.Value) : Situation.Teleport((string)null));
    LoadingScreen.LogInfo($"Portal teleport to {targetPosition}, destination biome: {biome?.ToString() ?? "unknown"}.");
  }

  public static void OnDeath(HitData lastHit) {
    if (lastHit == null) {
      Set(Situation.Death());
      return;
    }

    HitData.DamageTypes damage = lastHit.m_damage;

    Dictionary<string, float> damages = new() {
      ["Blunt"] = damage.m_blunt,
      ["Slash"] = damage.m_slash,
      ["Pierce"] = damage.m_pierce,
      ["Chop"] = damage.m_chop,
      ["Pickaxe"] = damage.m_pickaxe,
      ["Fire"] = damage.m_fire,
      ["Frost"] = damage.m_frost,
      ["Lightning"] = damage.m_lightning,
      ["Poison"] = damage.m_poison,
      ["Spirit"] = damage.m_spirit,
    };

    Situation situation = Situation.Death(
        DeathCauses.Keys(lastHit.m_hitType.ToString(), damages), AttackerPrefab(lastHit));
    Set(situation);
    LoadingScreen.LogInfo($"Player died: {situation}.");
  }

  public static Situation Consume(out Selection preselected) {
    bool isFresh = _pending.HasValue && Time.realtimeSinceStartup - _pendingSince <= PendingLifetimeSeconds;
    Situation situation = isFresh ? _pending.Value : Situation.General;
    preselected = isFresh ? _preselected : null;

    Clear();
    return situation;
  }

  public static void Clear() {
    _pending = null;
    _preselected = null;
  }

  static void Set(Situation situation) {
    _pending = situation;
    _pendingSince = Time.realtimeSinceStartup;
    _preselected = ScreenConfigLoader.Selector.Select(situation);

    if (_preselected?.Entry.Image != null) {
      CustomAssets.ReadLoadingImage(ScreenConfigLoader.ResolveImagePath(_preselected.Entry.Image));
    }
  }

  static string AttackerPrefab(HitData hit) {
    Character attacker = hit.GetAttacker();

    if (!attacker || attacker.IsPlayer()) {
      return null;
    }

    string name = attacker.gameObject.name;
    int clone = name.IndexOf('(');
    return (clone > 0 ? name.Substring(0, clone) : name).Trim();
  }

  static int? ResolveBiome(Vector3 position) {
    Heightmap.Biome biome =
        WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(position) : Heightmap.FindBiome(position);

    return biome == Heightmap.Biome.None ? null : (int)biome;
  }
}
