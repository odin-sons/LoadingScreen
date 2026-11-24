namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch(typeof(FejdStartup))]
static class FejdStartupPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Awake))]
  static void AwakePostfix(FejdStartup __instance) => MenuScreen.OnAwake(__instance);

  [HarmonyPrefix]
  [HarmonyPatch(nameof(FejdStartup.LoadMainScene))]
  static bool LoadMainScenePrefix(FejdStartup __instance) => !WorldSceneLoad.TryLoadInBackground(__instance);

  [HarmonyPrefix]
  [HarmonyPatch(nameof(FejdStartup.UpdateKeyboard))]
  static bool UpdateKeyboardPrefix() => !WorldSceneLoad.InProgress;

  [HarmonyPrefix]
  [HarmonyPatch(nameof(FejdStartup.UpdateGamepad))]
  static bool UpdateGamepadPrefix() => !WorldSceneLoad.InProgress;

  [HarmonyPrefix]
  [HarmonyPatch(nameof(FejdStartup.CheckPendingJoinRequest))]
  static bool CheckPendingJoinRequestPrefix() => !WorldSceneLoad.InProgress;

  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Update))]
  static void UpdatePostfix(FejdStartup __instance) => MenuScreen.OnUpdate(__instance);
}
