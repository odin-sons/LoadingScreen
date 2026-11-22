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
    if (IsModEnabled.Value) SetupSceneLoader(__instance);
  }

  static Image _loadingImage;
  static SceneLoader _sceneLoader;
  static GameObject _originalGameLogo;
  static bool _originalShowLogos;
  static bool _originalShowSaveNotification;
  static bool _originalShowHealthWarning;
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
  static bool _wasEnabled;

  internal static void Disable() {
    if (_host) _host.Hide();
    if (_loadingText) _loadingText.gameObject.SetActive(false);
    if (_sceneLoader) {
      _sceneLoader.gameLogo = _originalGameLogo;
      _sceneLoader._showLogos = _originalShowLogos;
      _sceneLoader._showSaveNotification = _originalShowSaveNotification;
      _sceneLoader._showHealthWarning = _originalShowHealthWarning;
    }
    if (_indicator) {
      RectTransform rect = _indicator.transform.GetComponent<RectTransform>();
      _indicator.m_showProgressIndicator = _indicatorShowsProgress;
      rect.anchorMin = _indicatorAnchorMin;
      rect.anchorMax = _indicatorAnchorMax;
      rect.pivot = _indicatorPivot;
      rect.anchoredPosition = _indicatorPosition;
    }
    _wasEnabled = false;
  }

  internal static void Enable() {
    if (!_sceneLoader || !_host || !SceneLoaderUseLoadingImages.Value || !HasImages()) return;
    _sceneLoader._showLogos = false;
    _sceneLoader._showSaveNotification = false;
    _sceneLoader._showHealthWarning = false;
    _sceneLoader.gameLogo = _loadingImage.gameObject;
    RefreshAppearance();
  }

  static void SetupSceneLoader(SceneLoader sceneLoader) {
    _wasEnabled = true;
    _sceneLoader = sceneLoader;
    _originalGameLogo = sceneLoader.gameLogo;
    _originalShowLogos = sceneLoader._showLogos;
    _originalShowSaveNotification = sceneLoader._showSaveNotification;
    _originalShowHealthWarning = sceneLoader._showHealthWarning;
    _loadingImage = sceneLoader.gameLogo.transform.parent.Find("Bkg").GetComponent<Image>();
    _host = null;

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
    _lastImageTime = LoadingManager.Session.Stage == LoadingStage.Scene
        ? LoadingManager.Session.StartedAt : Time.realtimeSinceStartup;
  }

  internal static void RefreshAppearance() {
    if (_host) _host.RefreshAppearance();
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
    if (!IsModEnabled.Value) return;
    if (!_indicator) SetupSceneLoader(__instance);
    if (!_wasEnabled) {
      _wasEnabled = true;
      if (_loadingText) _loadingText.gameObject.SetActive(true);
      if (_host && SceneLoaderUseLoadingImages.Value && HasImages()) ShowNextImage(__instance);
    }
    float time = Time.time;

    float now = Time.realtimeSinceStartup;
    if (SceneLoaderUseLoadingImages.Value
        && _host
        && HasImages()
        && SlideshowTiming.IsDue(now, _lastImageTime, ChangeImageEverySeconds.Value)) {
      _lastImageTime = now;
      ShowNextImage(__instance);
    }

    if (SceneLoaderShowProgressText.Value && _loadingText) {
      _loadingText.text = $"<b>{__instance._fakeProgress * 100:F0}%</b>\n<size=-4>({time - _startTime:F1}s)</size>";
    }
  }

  static bool HasImages() {
    return CustomAssets.LoadingImageFiles.Count > 0 || ScreenConfigLoader.Current.General.Entries.Count > 0;
  }

  static void ShowNextImage(SceneLoader sceneLoader) {
    if (LoadingManager.FejdStartupHost && LoadingManager.FejdStartupHost.gameObject.activeInHierarchy) {
      LoadingManager.Session.MarkVisible(Time.realtimeSinceStartup);
    }
    bool continueSession = LoadingManager.Session.CanContinue(LoadingStage.Scene, Situation.General);
    if (!LoadingManager.ShowSituation(
            _host, LoadingStage.Scene, Situation.General, tipText: null, continueSession: continueSession)) {
      if (_host) {
        _host.Hide();
      }

      LoadingManager.SetLoadingImage(_loadingImage);
      sceneLoader.ScaleLerpLoadingImage(_loadingImage);
    }
  }
}
