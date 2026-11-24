namespace LoadingScreen;

using System.Collections;

using HarmonyLib;

[HarmonyPatch(typeof(SceneLoader))]
static class SceneLoaderPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(SceneLoader.Start))]
  static void StartPrefix(SceneLoader __instance) => SceneLoaderScreen.OnStart(__instance);

  [HarmonyPostfix]
  [HarmonyPatch("LoadSceneAsync")]
  static void LoadSceneAsyncPostfix(SceneLoader __instance, ref IEnumerator __result) =>
      __result = SceneLoaderScreen.WaitForExit(__instance, __result);

  [HarmonyPostfix]
  [HarmonyPatch(nameof(SceneLoader.Update))]
  static void UpdatePostfix(SceneLoader __instance) => SceneLoaderScreen.OnUpdate(__instance);
}
