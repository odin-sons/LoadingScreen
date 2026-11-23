namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch]
static class PlayerInputPatch {
  [HarmonyPostfix]
  [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
  static void PlayerControllerTakeInputPostfix(ref bool __result) {
    if (HudPatch.IsHolding) __result = false;
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
  static void PlayerTakeInputPostfix(ref bool __result) {
    if (HudPatch.IsHolding) __result = false;
  }
}
