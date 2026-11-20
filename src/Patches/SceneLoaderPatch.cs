namespace LoadingScreen;

using HarmonyLib;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using static PluginConfig;

[HarmonyPatch(typeof(SceneLoader))]
static class SceneLoaderPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(SceneLoader.Start))]
  static void StartPrefix(SceneLoader __instance) {
    SetupSceneLoader(__instance);
  }

  static Image _loadingImage;
  static LayerHost _host;
  static TMP_Text _loadingText;

  static float _startTime;
  static float _lastImageTime;

  static void SetupSceneLoader(SceneLoader sceneLoader) {
    _loadingImage = sceneLoader.gameLogo.transform.parent.Find("Bkg").GetComponent<Image>();

    if (SceneLoaderUseLoadingImages.Value && HasImages()) {
      sceneLoader._showLogos = false;
      sceneLoader._showSaveNotification = false;
      sceneLoader._showHealthWarning = false;

      LoadingManager.SetupLoadingBackground(_loadingImage.transform.parent);
      LoadingManager.SetupLoadingImage(_loadingImage);
      _host = LayerHost.Attach(_loadingImage);
      ShowNextImage(sceneLoader);

      sceneLoader.gameLogo = _loadingImage.gameObject;
    }

    if (SceneLoaderShowProgressText.Value) {
      TMP_Text sourceLoadingText = sceneLoader.savingNotification.GetComponentInChildren<TMP_Text>();
      _loadingText = Object.Instantiate(sourceLoadingText, _loadingImage.transform.parent);

      LoadingManager.SetupTipText(_loadingText);
      LoadingManager.SetLoadingTip(_loadingText);
    }

    if (SceneLoaderCenterProgressIndicator.Value) {
      SetupLoadingIndicator(sceneLoader.loadingIndicator);
    }

    _startTime = Time.time;
    _lastImageTime = _startTime;
  }

  static void SetupLoadingIndicator(LoadingIndicator indicator) {
    indicator.m_showProgressIndicator = true;

    RectTransform rectTransform = indicator.transform.GetComponent<RectTransform>();
    rectTransform.anchorMin = new(0.5f, 0f);
    rectTransform.anchorMax = new(0.5f, 0f);
    rectTransform.pivot = new(0.5f, 0f);
    rectTransform.anchoredPosition = SceneLoaderProgressIndicatorOffset.Value;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(SceneLoader.Update))]
  static void UpdatePostfix(SceneLoader __instance) {
    float time = Time.time;

    if (SceneLoaderUseLoadingImages.Value
        && CustomAssets.LoadingImageFiles.Count > 0
        && time - _lastImageTime >= 10f) {
      _lastImageTime = time;

      LoadingManager.SetLoadingImage(_loadingImage);
      __instance.ScaleLerpLoadingImage(_loadingImage);
    }

    if (SceneLoaderShowProgressText.Value && _loadingText) {
      _loadingText.text = $"<b>{__instance._fakeProgress * 100:F0}%</b>\n<size=-4>({time - _startTime:F1}s)</size>";
    }
  }

  static bool HasImages() {
    return CustomAssets.LoadingImageFiles.Count > 0 || ScreenConfigLoader.Current.General.Entries.Count > 0;
  }

  static void ShowNextImage(SceneLoader sceneLoader) {
    if (!LoadingManager.ShowSituation(_host, Situation.General, tipText: null)) {
      if (_host) {
        _host.Hide();
      }

      LoadingManager.SetLoadingImage(_loadingImage);
      sceneLoader.ScaleLerpLoadingImage(_loadingImage);
    }
  }
}
