namespace LoadingScreen;

using System.Collections;

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
  static Sprite _spinnerSprite;
  static Vector2 _spinnerSize;
  static Vector2 _spinnerCenter;
  static Vector2 _ringCenter;
  static Sprite _ringSprite;
  static float _ringDiameter;
  static Color _ringColor = new(1f, 0.6f, 0.1f);
  internal static Sprite SpinnerSprite => _spinnerSprite;
  internal static Vector2 SpinnerSize => _spinnerSize;
  internal static Vector2 SpinnerCenter => _spinnerCenter;
  internal static Vector2 RingCenter => _ringCenter;
  internal static Sprite RingSprite => _ringSprite;
  internal static float RingDiameter => _ringDiameter;
  internal static Color RingColor => _ringColor;
  internal static TMP_Text ProgressTextTemplate => _loadingText
      ? _loadingText : _sceneLoader ? _sceneLoader.savingNotification.GetComponentInChildren<TMP_Text>(true) : null;
  internal static ProgressValue CurrentProgress => _sceneLoader && _sceneLoader._sceneLoadOperation != null
      ? ProgressMath.Scene(_sceneLoader._sceneLoadOperation.Progress) : new(null);
  static bool _spinnerWasEnabled;
  static Vector2 _indicatorAnchorMin;
  static Vector2 _indicatorAnchorMax;
  static Vector2 _indicatorPivot;
  static Vector2 _indicatorPosition;
  static bool _indicatorShowsProgress;

  static float _startTime;
  static float _lastImageTime;
  static bool _wasEnabled;

  [HarmonyPostfix]
  [HarmonyPatch("LoadSceneAsync")]
  static void LoadSceneAsyncPostfix(SceneLoader __instance, ref IEnumerator __result) {
    __result = WaitForExit(__instance, __result);
  }

  static IEnumerator WaitForExit(SceneLoader loader, IEnumerator loading) {
    bool exitPlayed = false;
    while (true) {
      if (!exitPlayed && loader == _sceneLoader && IsModEnabled.Value
          && WaitForExitAnimation.Value && _host && _host.IsShowing
          && loader.gameLogo.activeInHierarchy && loader._sceneLoadOperation != null
          && loader._sceneLoadOperation.IsLoadedButNotActivated
          && !loader.loadingIndicator.IsVisible && !PlatformInitializer.WaitingForInputDevice
          && (!StartupMessages.Instance || !StartupMessages.Instance.StartupMessageDisplayed)) {
        exitPlayed = true;
        float startedAt = Time.realtimeSinceStartup;
        while (_host.BeginExit(Time.realtimeSinceStartup)
            && Time.realtimeSinceStartup - startedAt < MaximumHoldSeconds.Value) yield return null;
      }
      if (!loading.MoveNext()) yield break;
      yield return loading.Current;
    }
  }

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
      _indicator.m_spinner.enabled = _spinnerWasEnabled;
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
    _indicator = sceneLoader.loadingIndicator;
    RectTransform indicatorRect = _indicator.transform.GetComponent<RectTransform>();
    _indicatorAnchorMin = indicatorRect.anchorMin;
    _indicatorAnchorMax = indicatorRect.anchorMax;
    _indicatorPivot = indicatorRect.pivot;
    _indicatorPosition = indicatorRect.anchoredPosition;
    _indicatorShowsProgress = _indicator.m_showProgressIndicator;
    _spinnerWasEnabled = _indicator.m_spinner.enabled;
    _spinnerSprite = _indicator.m_spinner.sprite;
    _spinnerSize = _indicator.m_spinner.rectTransform.rect.size;
    _spinnerCenter = CenterIn(indicatorRect, _indicator.m_spinner.rectTransform);
    Image ring = _indicator.m_progressIndicator;
    _ringCenter = ring ? CenterIn(indicatorRect, ring.rectTransform) : _spinnerCenter;
    _ringDiameter = ring ? Mathf.Max(ring.rectTransform.rect.width, ring.rectTransform.rect.height) : 0f;
    if (_ringDiameter < 1f) _ringDiameter = Mathf.Max(_spinnerSize.x, _spinnerSize.y) * 1.5f;
    _ringColor = ring ? ring.color : new(1f, 0.6f, 0.1f);
    _ringColor.a = 1f;
    _ringSprite = ring ? ring.sprite : null;
    Log.Render.Debug(LogContext.Message("Captured the game start indicator.",
        ("ringFound", (bool)ring), ("ringSprite", _ringSprite ? _ringSprite.name : "none"),
        ("ringDiameter", _ringDiameter), ("ringCenter", _ringCenter), ("ringColor", _ringColor),
        ("ringType", ring ? ring.type : Image.Type.Simple), ("spinnerCenter", _spinnerCenter)));

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

    RefreshAppearance();

    _startTime = Time.time;
    _lastImageTime = LoadingManager.Session.Stage == LoadingStage.Scene
        ? LoadingManager.Session.StartedAt : Time.realtimeSinceStartup;
  }

  static Vector2 CenterIn(RectTransform container, RectTransform child) {
    Vector3 center = container.InverseTransformPoint(child.TransformPoint(child.rect.center));
    return new(center.x - container.rect.center.x, center.y - container.rect.yMin);
  }

  internal static void RefreshAppearance() {
    if (_host) _host.RefreshAppearance();
    if (!_indicator) return;

    RectTransform rectTransform = _indicator.transform.GetComponent<RectTransform>();
    SpinnerDefaults spinner = ScreenConfigLoader.Current.Defaults.Spinner;
    bool layeredSpinner = _host && _host.IsShowing && _host.HasSpinner;
    _indicator.m_spinner.enabled = !layeredSpinner && _spinnerWasEnabled;
    _indicator.m_showProgressIndicator = !layeredSpinner && (spinner.Center || _indicatorShowsProgress);
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
      float? progress = CurrentProgress.Fraction;
      _loadingText.text = progress.HasValue
          ? UiText.FormatProgress(Mathf.RoundToInt(progress.Value * 100f), Mathf.FloorToInt(time - _startTime))
          : string.Empty;
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
    RefreshAppearance();
  }
}
