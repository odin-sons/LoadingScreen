namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch]
static class NetworkPatch {
  [HarmonyPostfix]
  [HarmonyPatch(typeof(Game), nameof(Game.Start))]
  static void GameStartPostfix() {
    ServerImages.RegisterRpcs();
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
  static void PlayerOnSpawnedPostfix(Player __instance) {
    if (__instance == Player.m_localPlayer) {
      ServerImages.OnLocalPlayerSpawned();
    }
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnDestroy))]
  static void ZNetOnDestroyPostfix() {
    ServerImages.Reset();
    SituationTracker.Clear();
  }
}
