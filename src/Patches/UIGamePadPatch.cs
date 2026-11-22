namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch(typeof(UIGamePad))]
static class UIGamePadPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(UIGamePad.ButtonPressed))]
  static bool ButtonPressedPrefix(ref bool __result) {
    if (!WorldSceneLoad.InProgress) return true;
    __result = false;
    return false;
  }
}
