namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch(typeof(FejdStartup))]
static class FejdStartupPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Awake))]
  static void AwakePostfix(FejdStartup __instance) => MenuScreen.OnAwake(__instance);

  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Update))]
  static void UpdatePostfix(FejdStartup __instance) => MenuScreen.OnUpdate(__instance);
}
