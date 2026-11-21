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

  static readonly Dictionary<Image, VanillaLook> _vanillaLooks = [];
  public static LoadingSession Session { get; } = new();

  static TMP_Text _cachedTipText;
  static TMP_Text _continuePrompt;
  static Image _cachedLoadingImage;
  static Image _cachedLoadingBackground;
  static Transform _cachedPanelSeparator;

  public static LayerHost HudHost { get; private set; }
  public static LayerHost FejdStartupHost { get; private set; }
  public static TMP_Text FejdStartupTipText { get; private set; }

  public static string CurrentCaption { get; private set; }

  public static Sprite SeparatorSprite { get; private set; }

  public static void RefreshAppearance() {
    if (_cachedLoadingImage) SetupLoadingImage();
    if (_cachedTipText) SetupTipText();
    if (_continuePrompt) SetupContinuePromptAppearance();
    if (_cachedLoadingBackground) SetupLoadingBackground();
    if (_cachedPanelSeparator) SetupPanelSeparator();
    if (HudHost) HudHost.RefreshAppearance();
    if (FejdStartupHost) FejdStartupHost.RefreshAppearance();
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
    if (!host.Show(selection, caption, tipText, Time.realtimeSinceStartup - startedAt)) {
      return false;
    }

    LoadingAudio.Play(host, selection, hasEntryText && tipText, continueSession);

    LoadingScreen.LogInfo($"Loading screen for {situation}: {selection.List.Name} -> {selection.Entry.Image}");

    CurrentCaption = continueSession ? Session.CurrentCaption
        : hasEntryText ? entryText : CaptionUseTipWhenNoText.Value ? null : string.Empty;
    if (continueSession) Session.Continue(stage);
    else Session.Record(stage, selection, caption, CurrentCaption, startedAt);

    if (tipText && !host.ShowsOwnText) {
      tipText.SetText(caption ?? string.Empty);
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

  public static void RotateTip(LayerHost host, TMP_Text tipText) {
    if (!CustomAssets.GetRandomLoadingTip(out string tip)) {
      return;
    }

    if (tipText) tipText.SetText(tip);
    if (host) host.SetCaption(tip);
    if (Session.Stage == LoadingStage.Menu) Session.SetCaption(tip);
  }

  public static void SetupLoadingImage(Image loadingImage = default) {
    if (loadingImage) {
      _cachedLoadingImage = loadingImage;
    } else if (_cachedLoadingImage) {
      loadingImage = _cachedLoadingImage;
    } else {
      LoadingScreen.LogError($"Could not find a LoadingImage to setup!");
      return;
    }

    foreach (Image destroyed in _vanillaLooks.Keys.Where(key => !key).ToList()) {
      _vanillaLooks.Remove(destroyed);
    }

    if (!_vanillaLooks.TryGetValue(loadingImage, out VanillaLook vanilla)) {
      vanilla = new(loadingImage.sprite, loadingImage.color, loadingImage.type, loadingImage.preserveAspect);
      _vanillaLooks[loadingImage] = vanilla;
    }

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
    if (loadingBackground) {
      _cachedLoadingBackground = loadingBackground;
    } else if (_cachedLoadingBackground) {
      loadingBackground = _cachedLoadingBackground;
    } else {
      LoadingScreen.LogError($"Could not find a LoadingBackground to setup!");
    }

    loadingBackground.Ref()?
        .SetType(Image.Type.Simple)
        .SetColor(ToColor(ScreenConfigLoader.Current.Defaults.Background));
  }

  public static void SetupPanelSeparator(Transform panelSeparator = default) {
    if (panelSeparator) {
      _cachedPanelSeparator = panelSeparator;
    } else if (_cachedPanelSeparator) {
      panelSeparator = _cachedPanelSeparator;
    } else {
      LoadingScreen.LogError($"Could not find a PanelSeparator to setup!");
      return;
    }

    if (!SeparatorSprite) {
      SeparatorSprite = panelSeparator.GetComponentInChildren<Image>(includeInactive: true).Ref()?.sprite;
    }

    SeparatorDefaults defaults = ScreenConfigLoader.Current.Defaults.Separator;
    panelSeparator.gameObject.SetActive(defaults.Show);
    panelSeparator.GetComponent<RectTransform>()
        .SetAnchorMin(new(0.5f, 0f))
        .SetAnchorMax(new(0.5f, 0f))
        .SetPosition(new(defaults.X, defaults.Bottom));
  }

  public static void SetupTipText(TMP_Text tipText = default) {
    if (tipText) {
      _cachedTipText = tipText;
    } else if (_cachedTipText) {
      tipText = _cachedTipText;
    } else {
      LoadingScreen.LogError($"Could not find a TipText to setup!");
    }

    TipDefaults defaults = ScreenConfigLoader.Current.Defaults.Tip;
    tipText
        .SetAlignment(TextAlignmentOptions.Top)
        .SetTextWrappingMode(TextWrappingModes.Normal)
        .SetFontSize(defaults.Size)
        .SetColor(ToColor(defaults.Color));

    tipText.GetComponent<RectTransform>()
        .SetAnchorMin(Vector2.zero)
        .SetAnchorMax(Vector2.right)
        .SetPosition(new(defaults.X, defaults.Bottom))
        .SetSizeDelta(new(-50f, 78f));
  }

  public static void ShowContinuePrompt(Hud hud, bool show) {
    if (show && !_continuePrompt) {
      _continuePrompt = Object.Instantiate(hud.m_loadingTip, hud.m_loadingProgress.transform);
      _continuePrompt.name = "ContinuePrompt";
      _continuePrompt.SetText("Press any key to continue");
      SetupContinuePromptAppearance();
    }

    if (_continuePrompt) {
      _continuePrompt.gameObject.SetActive(show);
    }
  }

  static void SetupContinuePromptAppearance() {
    ContinuePromptDefaults defaults = ScreenConfigLoader.Current.Defaults.ContinuePrompt;
    _continuePrompt
        .SetAlignment(TextAlignmentOptions.Center)
        .SetTextWrappingMode(TextWrappingModes.Normal)
        .SetFontSize(defaults.Size)
        .SetColor(ToColor(defaults.Color));

    _continuePrompt.GetComponent<RectTransform>()
        .SetAnchorMin(Vector2.zero)
        .SetAnchorMax(Vector2.right)
        .SetPosition(new(defaults.X, defaults.Bottom))
        .SetSizeDelta(new(-50f, 40f));
  }

  internal static Color ToColor(ColorValue value) {
    return new Color32(value.R, value.G, value.B, value.A);
  }

  public static void SetupFejdStartup(FejdStartup fejdStartup) {
    Image loadingImage = fejdStartup.m_loading.transform.Find("Bkg").GetComponent<Image>();

    // FejdStartup.m_menuAnimator locks the vanilla TMP_Text UI state, so work-around is to clone it to a new object.
    TMP_Text sourceLoadingText = fejdStartup.m_loading.transform.Find("Text").GetComponent<TMP_Text>();
    TMP_Text loadingText = Object.Instantiate(sourceLoadingText, fejdStartup.m_loading.transform);
    FejdStartupTipText = loadingText;
    loadingText.name = sourceLoadingText.name;
    Object.Destroy(sourceLoadingText.gameObject);

    Transform panelSeparator =
        Object.Instantiate(
            fejdStartup.m_menuList.transform.Find("ornament"), fejdStartup.m_loading.transform);

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

    loadingProgress.Find("TopFade").Ref()?.gameObject.SetActive(false);
    loadingProgress.Find("BottomFade").Ref()?.gameObject.SetActive(false);
    loadingProgress.Find("text_darken").Ref()?.gameObject.SetActive(false);

    SetupLoadingBackground(hud.transform.Find("LoadingBlack/Bkg").GetComponent<Image>());
  }
}
