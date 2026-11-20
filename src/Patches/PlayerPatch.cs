namespace LoadingScreen;

using HarmonyLib;

using UnityEngine;

[HarmonyPatch(typeof(Player))]
static class PlayerPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(Player.TeleportTo))]
  static void TeleportToPostfix(Player __instance, Vector3 pos, bool distantTeleport, bool __result) {
    if (__result && distantTeleport && __instance == Player.m_localPlayer) {
      SituationTracker.OnTeleport(pos);
    }
  }

  [HarmonyPrefix]
  [HarmonyPatch(nameof(Player.OnDeath))]
  static void OnDeathPrefix(Player __instance) {
    if (__instance == Player.m_localPlayer && __instance.m_nview && __instance.m_nview.IsOwner()) {
      SituationTracker.OnDeath(__instance.m_lastHit);
    }
  }
}
