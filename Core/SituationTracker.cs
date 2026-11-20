namespace LoadingScreen;

using System.Collections.Generic;

using global::LoadingScreen.Screens;

using UnityEngine;

public static class SituationTracker {
  const float PendingLifetimeSeconds = 120f;

  static Situation? _pending;
  static float _pendingSince;

  public static void OnTeleport(Vector3 targetPosition) {
    if (!PluginConfig.ShowLoadingScreenOnTeleport.Value) {
      return;
    }

    string biome = ResolveBiome(targetPosition);
    Set(Situation.Teleport(biome));
    LoadingScreen.LogInfo($"Portal teleport to {targetPosition}, destination biome: {biome ?? "unknown"}.");
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

    Situation situation = Situation.Death(DeathCauses.Keys(lastHit.m_hitType.ToString(), damages));
    Set(situation);
    LoadingScreen.LogInfo($"Player died: {situation}.");
  }

  public static Situation Consume() {
    Situation situation =
        _pending.HasValue && Time.realtimeSinceStartup - _pendingSince <= PendingLifetimeSeconds
            ? _pending.Value
            : Situation.General;

    _pending = null;
    return situation;
  }

  public static void Clear() {
    _pending = null;
  }

  static void Set(Situation situation) {
    _pending = situation;
    _pendingSince = Time.realtimeSinceStartup;
  }

  static string ResolveBiome(Vector3 position) {
    Heightmap.Biome biome =
        WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(position) : Heightmap.FindBiome(position);

    return biome == Heightmap.Biome.None ? null : biome.ToString();
  }
}
