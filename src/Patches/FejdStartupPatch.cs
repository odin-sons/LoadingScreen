namespace LoadingScreen;

using global::LoadingScreen.Model;

using HarmonyLib;

using UnityEngine;

using static PluginConfig;

[HarmonyPatch(typeof(FejdStartup))]
static class FejdStartupPatch {
  static bool _loadingWasVisible;
  static float _lastTipTime;

  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Awake))]
  static void AwakePostfix(FejdStartup __instance) {
    LoadingManager.SetupFejdStartup(__instance);
    _loadingWasVisible = __instance.m_loading.activeInHierarchy;
    _lastTipTime = Time.realtimeSinceStartup;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(FejdStartup.Update))]
  static void UpdatePostfix(FejdStartup __instance) {
    bool visible = __instance.m_loading.activeInHierarchy;
    float now = Time.realtimeSinceStartup;

    if (visible && !_loadingWasVisible) {
      _lastTipTime = now;
    }

    _loadingWasVisible = visible;

    if (!visible || !TipRotation.IsDue(
            now, _lastTipTime, ChangeTipEverySeconds.Value, LoadingManager.CurrentCaption,
            CustomAssets.LoadingTips.Count)) {
      return;
    }

    _lastTipTime = now;
    LoadingManager.RotateTip(LoadingManager.FejdStartupHost, LoadingManager.FejdStartupTipText);
  }
}
