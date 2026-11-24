namespace LoadingScreen;

using global::LoadingScreen.Model;

using UnityEngine;

using static PluginConfig;

internal static class MenuScreen {
  static bool _loadingWasVisible;

  internal static void OnAwake(FejdStartup __instance) {
    if (IsModEnabled.Value) LoadingManager.SetupFejdStartup(__instance);
    _loadingWasVisible = __instance.m_loading.activeInHierarchy;
    float now = Time.realtimeSinceStartup;
    if (LoadingManager.FejdStartupHost) LoadingManager.FejdStartupHost.Slideshow.Reset(now, now);
    if (_loadingWasVisible) LoadingManager.Session.MarkVisible(now);
  }

  internal static void OnUpdate(FejdStartup __instance) {
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
      if (LoadingManager.FejdStartupHost) LoadingManager.FejdStartupHost.Slideshow.Reset(now, now);
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

    if (visible && LoadingManager.FejdStartupHost)
      LoadingManager.FejdStartupHost.Slideshow.Tick(now, LoadingStage.Menu, Situation.General,
          LoadingManager.FejdStartupHost, LoadingManager.FejdStartupTipText, null,
          rotateImages: false, rotateTips: true);
  }
}
