namespace LoadingScreen;

using System.Collections.Generic;
using System.Linq;

using global::LoadingScreen.Extensions;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using static PluginConfig;

public static class LoadingManager {
  sealed record VanillaLook(Sprite Sprite, Color Color, Image.Type Type, bool PreserveAspect);
  sealed record TextLook(
      TMP_FontAsset Font, Material Material, FontStyles Style, TextAlignmentOptions Alignment,
      TextWrappingModes Wrapping, string Value, float Size, Color Color, Vector2 AnchorMin, Vector2 AnchorMax,
      Vector2 Position, Vector2 SizeDelta);
  sealed record PanelLook(bool Active, Vector2 AnchorMin, Vector2 AnchorMax, Vector2 Position);

  static readonly Dictionary<Image, VanillaLook> _vanillaLooks = [];
  static readonly HashSet<Image> _backgroundImages = [];
  static readonly Dictionary<TMP_Text, TextLook> _textLooks = [];
  static readonly Dictionary<TMP_Text, Image> _tipBoxes = [];
  static readonly Dictionary<TMP_Text, Image> _tipImages = [];
  static readonly Dictionary<Transform, PanelLook> _panelLooks = [];
  static readonly Dictionary<GameObject, bool> _hiddenObjects = [];
  static TMP_Text _originalFejdStartupText;
  static Transform _fejdStartupSeparator;
  static Image _createdLoadingBackground;
  static Canvas _loadingCanvas;
  static bool _loadingCanvasOverride;
  static int _loadingCanvasOrder;
  const int LoadingSortingOrder = 30000;
  public static LoadingSession Session { get; } = new();

  static TMP_Text _cachedTipText;
  static TMP_FontAsset _tipFont;
  static Material _tipMaterial;
  static FontStyles _tipStyle;
  static Image _cachedLoadingImage;
  static Image _cachedLoadingBackground;
  static Transform _cachedPanelSeparator;

  public static LayerHost HudHost { get; private set; }
  public static LayerHost FejdStartupHost { get; private set; }
  public static TMP_Text FejdStartupTipText { get; private set; }

  public static string CurrentCaption { get; private set; }

  public static Sprite SeparatorSprite { get; private set; }

  public static void Disable() {
    PruneDestroyedUi();
    Session.Reset();
    CurrentCaption = null;
    LoadingAudio.StopAllSounds();
    if (HudHost) HudHost.Hide();
    if (FejdStartupHost) FejdStartupHost.Hide();
    SceneLoaderScreen.Disable();
    foreach (Image image in _vanillaLooks.Keys.ToList()) {
      if (image) RestoreVanillaLook(image);
    }
    foreach (KeyValuePair<TMP_Text, TextLook> pair in _textLooks) {
      if (!pair.Key) continue;
      TMP_Text text = pair.Key;
      TextLook look = pair.Value;
      text.font = look.Font;
      text.fontSharedMaterial = look.Material;
      text.fontStyle = look.Style;
      text.alignment = look.Alignment;
      text.textWrappingMode = look.Wrapping;
      text.text = look.Value;
      text.fontSize = look.Size;
      text.color = look.Color;
      RectTransform rect = text.GetComponent<RectTransform>();
      rect.anchorMin = look.AnchorMin;
      rect.anchorMax = look.AnchorMax;
      rect.anchoredPosition = look.Position;
      rect.sizeDelta = look.SizeDelta;
    }
    foreach (Image box in _tipBoxes.Values) {
      if (box) Object.Destroy(box.gameObject);
    }
    _tipBoxes.Clear();
    _tipImages.Clear();
    foreach (KeyValuePair<Transform, PanelLook> pair in _panelLooks) {
      if (!pair.Key) continue;
      pair.Key.gameObject.SetActive(pair.Value.Active);
      RectTransform rect = pair.Key.GetComponent<RectTransform>();
      rect.anchorMin = pair.Value.AnchorMin;
      rect.anchorMax = pair.Value.AnchorMax;
      rect.anchoredPosition = pair.Value.Position;
    }
    foreach (KeyValuePair<GameObject, bool> pair in _hiddenObjects) {
      if (pair.Key) pair.Key.SetActive(pair.Value);
    }
    if (_originalFejdStartupText) _originalFejdStartupText.gameObject.SetActive(true);
    if (FejdStartupTipText) FejdStartupTipText.gameObject.SetActive(false);
    if (_fejdStartupSeparator) _fejdStartupSeparator.gameObject.SetActive(false);
    if (_createdLoadingBackground) _createdLoadingBackground.gameObject.SetActive(false);
    if (_loadingCanvas) {
      _loadingCanvas.overrideSorting = _loadingCanvasOverride;
      _loadingCanvas.sortingOrder = _loadingCanvasOrder;
    }
  }

  public static void Enable() {
    PruneDestroyedUi();
    if (_originalFejdStartupText) _originalFejdStartupText.gameObject.SetActive(false);
    if (FejdStartupTipText) FejdStartupTipText.gameObject.SetActive(true);
    if (_fejdStartupSeparator) _fejdStartupSeparator.gameObject.SetActive(true);
    if (_createdLoadingBackground) _createdLoadingBackground.gameObject.SetActive(true);
    if (_loadingCanvas) RaiseAboveHud(_loadingCanvas);
    foreach (GameObject hidden in _hiddenObjects.Keys) {
      if (hidden) hidden.SetActive(false);
    }
    foreach (TMP_Text text in _textLooks.Keys.ToList()) {
      if (text) SetupTipText(text);
    }
    foreach (Transform panel in _panelLooks.Keys.ToList()) {
      if (panel) SetupPanelSeparator(panel);
    }
    foreach (Image image in _backgroundImages.ToList()) {
      if (image) SetupLoadingBackground(image);
    }
    SceneLoaderScreen.Enable();
  }

  public static void HideOriginalMenuText() {
    if (_originalFejdStartupText) _originalFejdStartupText.gameObject.SetActive(false);
  }

  public static void RefreshAppearance() {
    if (_cachedLoadingImage) SetupLoadingImage();
    foreach (TMP_Text text in _textLooks.Keys.ToList()) {
      if (text) SetupTipText(text);
    }
    if (_cachedLoadingBackground) SetupLoadingBackground();
    if (_cachedPanelSeparator) SetupPanelSeparator();
    if (HudHost) HudHost.RefreshAppearance();
    if (FejdStartupHost) FejdStartupHost.RefreshAppearance();
    foreach (KeyValuePair<TMP_Text, Image> pair in _tipImages.ToList()) {
      if (pair.Key) ApplyTipReadability(pair.Key, pair.Value);
    }
  }

  public static bool ShowSituation(
      LayerHost host, LoadingStage stage, Situation situation, TMP_Text tipText,
      Selection preselected = null, bool continueSession = false) {
    if (!continueSession) CurrentCaption = null;

    if (!host) {
      return false;
    }

    Selection selection = continueSession ? Session.Selection : preselected ?? ScreenConfigLoader.Selector.Select(situation);

    if (selection == null) {
      host.Hide();
      return false;
    }

    string entryText = selection.Entry.GetText(CustomAssets.CurrentLanguageCode);
    bool hasEntryText = !string.IsNullOrEmpty(entryText);
    string caption = continueSession ? Session.Caption : hasEntryText ? entryText : null;

    if (!continueSession && !hasEntryText && CaptionUseTipWhenNoText.Value
        && CustomAssets.GetRandomLoadingTip(out string tip)) {
      caption = tip;
    }

    float startedAt = continueSession ? Session.StartedAt : Time.realtimeSinceStartup;
    if (!continueSession) Session.ResetImages();
    if (!host.Show(selection, caption, tipText, continueSession ? Session.AnimationElapsed : 0f,
            continueSession, stage == LoadingStage.Scene, stage, situation)) {
      return false;
    }

    LoadingAudio.Play(host, selection, hasEntryText && tipText, continueSession);

    Log.Render.Info(LogContext.Message("Showing loading screen.", ("side", "client"),
        ("screen", situation.Kind), ("stage", stage), ("situation", situation),
        ("entryList", selection.List.Name), ("image", selection.Entry.Image),
        ("layout", selection.LayoutName)));

    CurrentCaption = continueSession ? Session.CurrentCaption
        : hasEntryText ? entryText : CaptionUseTipWhenNoText.Value ? null : string.Empty;
    if (continueSession) Session.Continue(stage);
    else Session.Record(stage, selection, caption, CurrentCaption, startedAt);

    if (tipText && !host.IsShowingLayout) {
      tipText.SetText(caption ?? string.Empty);
      ApplyTipReadability(tipText, host.ReadableImageFor(tipText.rectTransform));
    } else if (tipText) {
      _tipImages.Remove(tipText);
      if (_tipBoxes.TryGetValue(tipText, out Image box) && box) box.color = Color.clear;
    }

    return true;
  }

  public static void ShowFallback(LayerHost host, Image loadingImage, TMP_Text tipText) {
    CurrentCaption = null;
    LoadingAudio.Stop(host);

    if (host) {
      host.Hide();
    }

    SetLoadingImage(loadingImage);
    SetLoadingTip(tipText);
    ApplyTipReadability(tipText, loadingImage);
  }

  public static void SetLoadingImage(Image loadingImage) {
    if (!loadingImage) {
      return;
    }

    if (CustomAssets.GetRandomLoadingImage(out Sprite loadingImageSprite)) {
      loadingImage.SetSprite(loadingImageSprite);
      ApplyCustomLook(loadingImage);
      CustomAssets.Pin(loadingImage, [loadingImageSprite]);
    } else {
      RestoreVanillaLook(loadingImage);
    }
  }

  public static void SetLoadingTip(TMP_Text tipText) {
    if (tipText && CustomAssets.GetRandomLoadingTip(out string loadingTipText)) {
      tipText.SetText(loadingTipText);
    }
  }

  static void ApplyTipReadability(TMP_Text tipText, Image image) {
    if (!tipText) return;
    _tipImages[tipText] = image;
    if (!ReadableText.Value) {
      if (_tipBoxes.TryGetValue(tipText, out Image hiddenBox) && hiddenBox) hiddenBox.color = Color.clear;
      return;
    }

    ColorValue background = ScreenConfigLoader.Current.Defaults.Background;
    ColorValue backing = background;
    ColorValue[] areaPixels = null;
    int areaSize = 0;
    float areaLeft = 0f, areaBottom = 0f, areaRight = 0f, areaTop = 0f;
    if (image && CustomAssets.TryGetImageSamples(image.sprite, out ColorValue[] pixels, out int size)) {
      Vector3[] textCorners = new Vector3[4];
      Vector3[] imageCorners = new Vector3[4];
      tipText.rectTransform.GetWorldCorners(textCorners);
      image.rectTransform.GetWorldCorners(imageCorners);
      float width = imageCorners[2].x - imageCorners[0].x;
      float height = imageCorners[2].y - imageCorners[0].y;
      float left = Mathf.Max(textCorners[0].x, imageCorners[0].x);
      float right = Mathf.Min(textCorners[2].x, imageCorners[2].x);
      float bottom = Mathf.Max(textCorners[0].y, imageCorners[0].y);
      float top = Mathf.Min(textCorners[2].y, imageCorners[2].y);
      float textArea = (textCorners[2].x - textCorners[0].x) * (textCorners[2].y - textCorners[0].y);
      if (width > 0f && height > 0f && textArea > 0f && left < right && bottom < top) {
        ColorValue sample = ColorMath.AverageArea(pixels, size, size,
            (left - imageCorners[0].x) / width, (bottom - imageCorners[0].y) / height,
            (right - imageCorners[0].x) / width, (top - imageCorners[0].y) / height);
        Color32 tint = image.color;
        float coverage = (right - left) * (top - bottom) / textArea;
        if (coverage > 0.999f && tint.r == 255 && tint.g == 255 && tint.b == 255 && tint.a == 255) {
          areaPixels = pixels;
          areaSize = size;
          areaLeft = (left - imageCorners[0].x) / width;
          areaBottom = (bottom - imageCorners[0].y) / height;
          areaRight = (right - imageCorners[0].x) / width;
          areaTop = (top - imageCorners[0].y) / height;
        }
        background = ColorMath.Blend(new(
            (byte) (sample.R * tint.r / 255), (byte) (sample.G * tint.g / 255),
            (byte) (sample.B * tint.b / 255), (byte) (sample.A * tint.a * coverage / 255)), background);
      }
    }

    Color32 original = ToColor(ScreenConfigLoader.Current.Defaults.Tip.Color);
    (ColorValue text, ColorValue? boxColor) = ColorMath.Readable(
        new(original.r, original.g, original.b, original.a), null, background,
        areaPixels == null ? null : candidate => ColorMath.LuminanceRange(
            areaPixels, areaSize, areaSize,
            areaLeft, areaBottom, areaRight, areaTop, candidate, backing));
    tipText.color = ToColor(text);
    if (!boxColor.HasValue) {
      if (_tipBoxes.TryGetValue(tipText, out Image existing) && existing) existing.color = Color.clear;
      return;
    }

    if (!_tipBoxes.TryGetValue(tipText, out Image box) || !box) {
      GameObject child = new("ReadableTipBackground", typeof(RectTransform), typeof(Image));
      child.transform.SetParent(tipText.transform.parent, worldPositionStays: false);
      box = child.GetComponent<Image>();
      box.raycastTarget = false;
      _tipBoxes[tipText] = box;
    }
    RectTransform textRect = tipText.rectTransform;
    RectTransform boxRect = box.rectTransform;
    boxRect.anchorMin = textRect.anchorMin;
    boxRect.anchorMax = textRect.anchorMax;
    boxRect.pivot = textRect.pivot;
    boxRect.anchoredPosition = textRect.anchoredPosition;
    boxRect.sizeDelta = textRect.sizeDelta;
    if (box.transform.GetSiblingIndex() > tipText.transform.GetSiblingIndex()) {
      box.transform.SetSiblingIndex(tipText.transform.GetSiblingIndex());
    }
    box.color = ToColor(boxColor.Value);
  }

  public static void RotateTip(LayerHost host, TMP_Text tipText) {
    if (!CustomAssets.GetRandomLoadingTip(out string tip)) {
      return;
    }

    if (tipText) tipText.SetText(tip);
    if (host) host.SetCaption(tip);
    if (Session.Stage == LoadingStage.Menu) Session.SetCaption(tip);
  }

  public static void SetupLoadingImage(Image loadingImage = default) {
    PruneDestroyedUi();
    if (loadingImage) {
      _cachedLoadingImage = loadingImage;
    } else if (_cachedLoadingImage) {
      loadingImage = _cachedLoadingImage;
    } else {
      Log.Render.WarningOnce("ui:LoadingImage", LogContext.Message(
          "Could not apply loading image appearance; game UI element is unavailable.",
          ("side", "client"), ("element", "LoadingImage")));
      return;
    }

    SaveVanillaLook(loadingImage);
    VanillaLook vanilla = _vanillaLooks[loadingImage];

    if (loadingImage.sprite != vanilla.Sprite) {
      ApplyCustomLook(loadingImage);
    }
  }

  static void ApplyCustomLook(Image loadingImage) {
    loadingImage
        .SetType(Image.Type.Simple)
        .SetColor(ToColor(ScreenConfigLoader.Current.Defaults.Tint))
        .SetPreserveAspect(true);
  }

  static void SaveVanillaLook(Image image) {
    if (!_vanillaLooks.ContainsKey(image)) {
      _vanillaLooks[image] = new(image.sprite, image.color, image.type, image.preserveAspect);
    }
  }

  static void PruneDestroyedUi() {
    foreach (Image destroyed in _vanillaLooks.Keys.Where(key => !key).ToList()) _vanillaLooks.Remove(destroyed);
    _backgroundImages.RemoveWhere(image => !image);
    foreach (TMP_Text destroyed in _textLooks.Keys.Where(key => !key).ToList()) _textLooks.Remove(destroyed);
    foreach (Transform destroyed in _panelLooks.Keys.Where(key => !key).ToList()) _panelLooks.Remove(destroyed);
    foreach (GameObject destroyed in _hiddenObjects.Keys.Where(key => !key).ToList()) _hiddenObjects.Remove(destroyed);
    foreach (TMP_Text destroyed in _tipBoxes.Keys.Where(key => !key).ToList()) _tipBoxes.Remove(destroyed);
    foreach (TMP_Text destroyed in _tipImages.Keys.Where(key => !key).ToList()) _tipImages.Remove(destroyed);
    if (!_createdLoadingBackground) _createdLoadingBackground = null;
  }

  static void RestoreVanillaLook(Image loadingImage) {
    if (_vanillaLooks.TryGetValue(loadingImage, out VanillaLook vanilla)) {
      loadingImage
          .SetSprite(vanilla.Sprite)
          .SetType(vanilla.Type)
          .SetColor(vanilla.Color)
          .SetPreserveAspect(vanilla.PreserveAspect);
    }

    CustomAssets.Pin(loadingImage, []);
  }

  public static void SetupLoadingBackground(Transform parentTransform) {
    if (!_cachedLoadingBackground) {
      GameObject background = new("Background", typeof(RectTransform));
      _cachedLoadingBackground = background.AddComponent<Image>();
      _createdLoadingBackground = _cachedLoadingBackground;
    }

    _cachedLoadingBackground.transform.SetParent(parentTransform, worldPositionStays: false);
    _cachedLoadingBackground.transform.SetAsFirstSibling();

    _cachedLoadingBackground.GetComponent<RectTransform>()
        .SetAnchorMin(Vector2.zero)
        .SetAnchorMax(Vector2.one)
        .SetPivot(new(0.5f, 0.5f))
        .SetPosition(Vector2.zero)
        .SetSizeDelta(Vector2.zero);

    SetupLoadingBackground();
  }

  public static void SetupLoadingBackground(Image loadingBackground = default) {
    PruneDestroyedUi();
    if (loadingBackground) {
      _cachedLoadingBackground = loadingBackground;
    } else if (_cachedLoadingBackground) {
      loadingBackground = _cachedLoadingBackground;
    } else {
      Log.Render.WarningOnce("ui:LoadingBackground", LogContext.Message(
          "Could not apply loading background appearance; game UI element is unavailable.",
          ("side", "client"), ("element", "LoadingBackground")));
    }

    if (loadingBackground) {
      SaveVanillaLook(loadingBackground);
      _backgroundImages.Add(loadingBackground);
      loadingBackground
          .SetType(Image.Type.Simple)
          .SetColor(ToColor(ScreenConfigLoader.Current.Defaults.Background));
    }
  }

  public static void SetupPanelSeparator(Transform panelSeparator = default) {
    PruneDestroyedUi();
    if (panelSeparator) {
      _cachedPanelSeparator = panelSeparator;
    } else if (_cachedPanelSeparator) {
      panelSeparator = _cachedPanelSeparator;
    } else {
      Log.Render.WarningOnce("ui:PanelSeparator", LogContext.Message(
          "Could not apply separator appearance; game UI element is unavailable.",
          ("side", "client"), ("element", "PanelSeparator")));
      return;
    }

    if (!SeparatorSprite) {
      SeparatorSprite = panelSeparator.GetComponentInChildren<Image>(includeInactive: true).Ref()?.sprite;
    }

    if (!_panelLooks.ContainsKey(panelSeparator)) {
      RectTransform rect = panelSeparator.GetComponent<RectTransform>();
      _panelLooks[panelSeparator] = new(
          panelSeparator.gameObject.activeSelf, rect.anchorMin, rect.anchorMax, rect.anchoredPosition);
    }

    SeparatorDefaults defaults = ScreenConfigLoader.Current.Defaults.Separator;
    panelSeparator.gameObject.SetActive(defaults.Show);
    panelSeparator.GetComponent<RectTransform>()
        .SetAnchorMin(new(0.5f, 0f))
        .SetAnchorMax(new(0.5f, 0f))
        .SetPosition(new(defaults.X, defaults.Bottom));
  }

  public static void MatchTipFont(TMP_Text text) {
    if (!text) return;
    if (!_tipFont) {
      _tipFont = text.font;
      _tipMaterial = text.fontSharedMaterial;
      _tipStyle = text.fontStyle;
      return;
    }
    if (text.font != _tipFont) {
      text.font = _tipFont;
      text.fontSharedMaterial = _tipMaterial;
    }
    text.fontStyle = _tipStyle;
  }

  public static void SetupTipText(TMP_Text tipText = default) {
    PruneDestroyedUi();
    if (tipText) {
      _cachedTipText = tipText;
    } else if (_cachedTipText) {
      tipText = _cachedTipText;
    } else {
      Log.Render.WarningOnce("ui:TipText", LogContext.Message(
          "Could not apply tip appearance; game UI element is unavailable.",
          ("side", "client"), ("element", "TipText")));
    }

    if (!_textLooks.ContainsKey(tipText)) {
      RectTransform rect = tipText.GetComponent<RectTransform>();
      _textLooks[tipText] = new(
          tipText.font, tipText.fontSharedMaterial, tipText.fontStyle, tipText.alignment,
          tipText.textWrappingMode, tipText.text, tipText.fontSize, tipText.color,
          rect.anchorMin, rect.anchorMax, rect.anchoredPosition, rect.sizeDelta);
    }

    MatchTipFont(tipText);
    TipDefaults defaults = ScreenConfigLoader.Current.Defaults.Tip;
    tipText
        .SetAlignment(TextAlignmentOptions.Top)
        .SetTextWrappingMode(TextWrappingModes.Normal)
        .SetFontSize(Mathf.Max(defaults.Size, PluginConfig.MinimumTextSize.Value))
        .SetColor(ToColor(defaults.Color));

    tipText.GetComponent<RectTransform>()
        .SetAnchorMin(Vector2.zero)
        .SetAnchorMax(Vector2.right)
        .SetPosition(new(defaults.X, defaults.Bottom))
        .SetSizeDelta(new(-50f, 78f));
  }

  internal static Color ToColor(ColorValue value) {
    return new Color32(value.R, value.G, value.B, value.A);
  }

  public static void SetupFejdStartup(FejdStartup fejdStartup) {
    Image loadingImage = fejdStartup.m_loading.transform.Find("Bkg").GetComponent<Image>();

    // FejdStartup.m_menuAnimator locks the vanilla TMP_Text UI state, so work-around is to clone it to a new object.
    TMP_Text sourceLoadingText = fejdStartup.m_loading.transform.Find("Text").GetComponent<TMP_Text>();
    _originalFejdStartupText = sourceLoadingText;
    TMP_Text loadingText = Object.Instantiate(sourceLoadingText, fejdStartup.m_loading.transform);
    FejdStartupTipText = loadingText;
    loadingText.name = sourceLoadingText.name;
    sourceLoadingText.gameObject.SetActive(false);

    Transform panelSeparator =
        Object.Instantiate(
            fejdStartup.m_menuList.transform.Find("ornament"), fejdStartup.m_loading.transform);
    _fejdStartupSeparator = panelSeparator;

    SetupTipText(loadingText);
    SetupLoadingBackground(loadingImage.transform.parent);
    SetupLoadingImage(loadingImage);
    SetupPanelSeparator(panelSeparator);

    FejdStartupHost = LayerHost.Attach(loadingImage);
    FejdStartupHost.Separator = panelSeparator;

    if (!ShowSituation(FejdStartupHost, LoadingStage.Menu, Situation.General, loadingText)) {
      ShowFallback(FejdStartupHost, loadingImage, loadingText);
    }
  }

  public static void SetupHud(Hud hud) {
    Transform loadingProgress = hud.m_loadingProgress.transform;
    Transform panelSeparator = loadingProgress.Find("panel_separator");

    SetupTipText(hud.m_loadingTip);
    SetupLoadingImage(hud.m_loadingImage);
    SetupPanelSeparator(panelSeparator);

    HudHost = LayerHost.Attach(hud.m_loadingImage);
    HudHost.Separator = panelSeparator;
    ShowFallback(HudHost, hud.m_loadingImage, hud.m_loadingTip);

    HideForMod(loadingProgress.Find("TopFade"));
    HideForMod(loadingProgress.Find("BottomFade"));
    HideForMod(loadingProgress.Find("text_darken"));

    SetupLoadingBackground(hud.transform.Find("LoadingBlack/Bkg").GetComponent<Image>());
    SetupLoadingCanvas(hud.m_loadingScreen);
  }

  static void SetupLoadingCanvas(CanvasGroup loadingScreen) {
    if (!loadingScreen) return;
    GameObject gameObject = loadingScreen.gameObject;
    Canvas canvas = gameObject.GetComponent<Canvas>();
    if (canvas) {
      _loadingCanvasOverride = canvas.overrideSorting;
      _loadingCanvasOrder = canvas.sortingOrder;
    } else {
      canvas = gameObject.AddComponent<Canvas>();
      _loadingCanvasOverride = false;
      _loadingCanvasOrder = 0;
    }
    if (!gameObject.GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
    _loadingCanvas = canvas;
    RaiseAboveHud(canvas);
  }

  static void RaiseAboveHud(Canvas canvas) {
    canvas.overrideSorting = true;
    canvas.sortingOrder = LoadingSortingOrder;
  }

  static void HideForMod(Transform transform) {
    if (!transform) return;
    GameObject gameObject = transform.gameObject;
    if (!_hiddenObjects.ContainsKey(gameObject)) _hiddenObjects[gameObject] = gameObject.activeSelf;
    gameObject.SetActive(false);
  }
}
