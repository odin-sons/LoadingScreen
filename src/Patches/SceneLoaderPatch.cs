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
  static LoadingIndicator _indicator;
  static Vector2 _indicatorAnchorMin;
  static Vector2 _indicatorAnchorMax;
  static Vector2 _indicatorPivot;
  static Vector2 _indicatorPosition;
  static bool _indicatorShowsProgress;

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

    _indicator = sceneLoader.loadingIndicator;
    RectTransform indicatorRect = _indicator.transform.GetComponent<RectTransform>();
    _indicatorAnchorMin = indicatorRect.anchorMin;
    _indicatorAnchorMax = indicatorRect.anchorMax;
    _indicatorPivot = indicatorRect.pivot;
    _indicatorPosition = indicatorRect.anchoredPosition;
    _indicatorShowsProgress = _indicator.m_showProgressIndicator;
    RefreshAppearance();

    _startTime = Time.time;
    _lastImageTime = _startTime;
  }

  internal static void RefreshAppearance() {
    if (!_indicator) return;

    RectTransform rectTransform = _indicator.transform.GetComponent<RectTransform>();
    SpinnerDefaults spinner = ScreenConfigLoader.Current.Defaults.Spinner;
    _indicator.m_showProgressIndicator = spinner.Center || _indicatorShowsProgress;
    rectTransform.anchorMin = spinner.Center ? new(0.5f, 0f) : _indicatorAnchorMin;
    rectTransform.anchorMax = spinner.Center ? new(0.5f, 0f) : _indicatorAnchorMax;
    rectTransform.pivot = spinner.Center ? new(0.5f, 0f) : _indicatorPivot;
    rectTransform.anchoredPosition = spinner.Center ? new(spinner.X, spinner.Bottom) : _indicatorPosition;
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
