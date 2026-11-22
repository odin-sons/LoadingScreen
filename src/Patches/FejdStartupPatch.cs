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
    if (IsModEnabled.Value) LoadingManager.SetupFejdStartup(__instance);
    _loadingWasVisible = __instance.m_loading.activeInHierarchy;
    _lastTipTime = Time.realtimeSinceStartup;
    if (_loadingWasVisible) LoadingManager.Session.MarkVisible(_lastTipTime);
  }

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
  static void UpdatePostfix(FejdStartup __instance) {
    if (!IsModEnabled.Value) {
      _loadingWasVisible = false;
      return;
    }

    if (!LoadingManager.FejdStartupHost) {
      LoadingManager.SetupFejdStartup(__instance);
      _loadingWasVisible = __instance.m_loading.activeInHierarchy;
      if (_loadingWasVisible) LoadingManager.Session.MarkVisible(Time.realtimeSinceStartup);
    }
    LoadingManager.HideOriginalMenuText();
    bool visible = __instance.m_loading.activeInHierarchy;
    float now = Time.realtimeSinceStartup;

    if (visible && !_loadingWasVisible) {
      _lastTipTime = now;
      if (!LoadingManager.ShowSituation(
              LoadingManager.FejdStartupHost, LoadingStage.Menu, Situation.General,
              LoadingManager.FejdStartupTipText)) {
        LoadingManager.ShowFallback(
            LoadingManager.FejdStartupHost,
            __instance.m_loading.transform.Find("Bkg").GetComponent<UnityEngine.UI.Image>(),
            LoadingManager.FejdStartupTipText);
      }
      LoadingManager.Session.MarkVisible(now);
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
