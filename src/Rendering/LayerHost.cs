namespace LoadingScreen;

using System.Collections.Generic;
using System.Linq;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public sealed class LayerHost : MonoBehaviour {
  sealed class PlacedImage {
    public RectTransform Rect;
    public Layer Layer;
    public Placement Placement;
    public Vector2 ImageSize;
    public readonly List<(Sides Side, RectTransform Rect, Length? Size)> Fades = [];
  }

  sealed class PlacedText {
    public RectTransform Rect;
    public TMP_Text Text;
    public Placement Placement;
    public float Padding;
    public TextStyle Style;
    public Image Background;
    public ColorValue BaseColor;
    public float BaseSize;
  }

  sealed class AnimatedLayer {
    public RectTransform Rect;
    public CanvasGroup Group;
    public AnimationPreset Enter;
    public AnimationPreset Idle;
    public AnimationPreset Exit;
    public float Speed;
    public Vector2 BasePosition;
  }

  const float TextBackgroundPadding = 12f;

  static readonly Dictionary<Layer, string> _lastPoolPick = [];

  readonly List<PlacedText> _texts = [];
  TMP_Text _hiddenTemplate;

  readonly List<PlacedImage> _placed = [];
  readonly List<AnimatedLayer> _animated = [];
  RectTransform _root;
  ScreenEffectOverlay _effects;
  Graphic _vanillaImage;
  Vector2 _lastSize;
  bool _layoutShowsSeparator;
  float _animationStartedAt;
  float _idleBase;
  float _idleAtExit;
  float _enterDuration;
  float _exitDuration;
  float _exitStartedAt = -1f;
  bool _skipEnter;

  public bool IsShowing { get; private set; }
  public bool ShowSleepUi { get; private set; } = true;
  public bool HasSpinner { get; private set; }
  public bool HasExplicitSpinner { get; private set; }
  public Selection CurrentSelection { get; private set; }
  public Image ReadableImageFor(RectTransform text) {
    Vector3[] textCorners = new Vector3[4];
    text.GetWorldCorners(textCorners);
    foreach (PlacedImage placed in _placed.AsEnumerable().Reverse()) {
      Vector3[] imageCorners = new Vector3[4];
      placed.Rect.GetWorldCorners(imageCorners);
      if (textCorners[0].x < imageCorners[2].x && textCorners[2].x > imageCorners[0].x
          && textCorners[0].y < imageCorners[2].y && textCorners[2].y > imageCorners[0].y) {
        return placed.Rect.GetComponent<Image>();
      }
    }
    return null;
  }

  public Transform Separator { get; set; }

  static Placement SeparatorPlacement {
    get {
      SeparatorDefaults defaults = ScreenConfigLoader.Current.Defaults.Separator;
      return new() {
        Fit = new() { Mode = FitMode.None },
        Anchor = Anchor.Bottom,
        X = new Length(defaults.X, LengthUnit.Pixels),
        Y = new Length(-defaults.Bottom, LengthUnit.Pixels),
      };
    }
  }

  public static LayerHost Attach(Graphic vanillaImage) {
    Transform parent = vanillaImage.transform.parent;
    Transform existing = parent.Find("LoadingScreenLayers");

    if (existing && existing.TryGetComponent(out LayerHost existingHost)) {
      return existingHost;
    }

    GameObject root = new("LoadingScreenLayers", typeof(RectTransform), typeof(RectMask2D));
    root.transform.SetParent(parent, worldPositionStays: false);
    root.transform.SetSiblingIndex(vanillaImage.transform.GetSiblingIndex() + 1);

    RectTransform rect = root.GetComponent<RectTransform>();
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.pivot = new(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = Vector2.zero;

    LayerHost host = root.AddComponent<LayerHost>();
    host._root = rect;
    host._effects = new ScreenEffectOverlay(parent);
    host._vanillaImage = vanillaImage;
    root.SetActive(false);

    return host;
  }

  public bool Show(Selection selection, string caption = null, TMP_Text textTemplate = null,
      float animationElapsed = 0f, bool continued = false, bool gameStart = false) {
    Clear();

    if (selection == null) {
      Hide();
      return false;
    }

    ScreenConfig config = ScreenConfigLoader.Current;
    Layout layout =
        selection.LayoutName != null && config.Layouts.TryGetValue(selection.LayoutName, out Layout named)
            ? named
            : DefaultLayout;

    gameObject.SetActive(true);

    bool drewEntryImage = false;

    foreach (Layer layer in layout.Layers) {
      if (layer.Kind == LayerKind.Color) {
        AddColorLayer(layer);
        continue;
      }

      if (layer.Kind == LayerKind.Text) {
        if (textTemplate) {
          AddTextLayer(layer, Placement.Merge(layer.Placement, layout.Placement), caption ?? string.Empty, textTemplate);
        }

        continue;
      }

      if (layer.Kind == LayerKind.Spinner) {
        AddSpinnerLayer(layer, layout);
        continue;
      }

      if (layer.GameSeparator) {
        if (LoadingManager.SeparatorSprite) {
          AddImageLayer(layer, LoadingManager.SeparatorSprite, Placement.Merge(layer.Placement, SeparatorPlacement));
        }

        continue;
      }

      string path = layer.FromEntry
          ? selection.Entry.Image ?? LoadingManager.Session.ImageFor(layer, CustomAssets.NextLoadingImagePath)
          : LoadingManager.Session.ImageFor(layer, () => PickFromPool(layer));
      Sprite sprite = CustomAssets.ReadLoadingImage(
          layer.FromEntry && selection.Entry.Image == null ? path : ScreenConfigLoader.ResolveImagePath(path));

      if (!sprite) {
        continue;
      }

      Placement placement =
          layer.FromEntry
              ? Placement.Merge(selection.Entry.Place, layer.Placement, layout.Placement, config.Defaults)
              : Placement.Merge(layer.Placement, layout.Placement, config.Defaults);

      AddImageLayer(layer, sprite, placement, layout == DefaultLayout && layer.FromEntry);
      drewEntryImage |= layer.FromEntry;
    }

    if (gameStart && !layout.ShowsSpinner) {
      Layer legacySpinner = config.Defaults.Spinner.ToLayer();
      if (legacySpinner != null) AddSpinnerLayer(legacySpinner, layout);
    }
    HasExplicitSpinner = gameStart && layout.ShowsSpinner && HasSpinner;

    if (!drewEntryImage && layout.ShowsEntryImage && selection.Entry.Image != null) {
      Hide();
      return false;
    }

    if (_vanillaImage && _placed.Count > 0) {
      _vanillaImage.enabled = false;
    }

    CustomAssets.Pin(
        this,
        _placed
            .Where(placed => placed.Layer.Kind != LayerKind.Spinner || placed.Layer.SpinnerStyle == SpinnerStyle.Image)
            .Select(placed => placed.Rect.GetComponent<Image>().sprite)
            .Where(sprite => sprite != LoadingManager.SeparatorSprite));
    _layoutShowsSeparator = layout.ShowsSeparator;
    ShowSleepUi = layout.ShowSleepUi;
    SetSeparatorVisible(!_layoutShowsSeparator && config.Defaults.Separator.OnLayouts);
    CurrentSelection = selection;
    IsShowing = true;
    _animationStartedAt = Time.realtimeSinceStartup;
    _idleBase = continued ? animationElapsed : 0f;
    _skipEnter = continued;
    _exitStartedAt = -1f;
    Canvas.ForceUpdateCanvases();
    Relayout();
    RenderAnimations(Time.realtimeSinceStartup);
    _effects.Apply(config.Defaults, layout);
    return true;
  }

  public void Hide() {
    Clear();
    IsShowing = false;
    CurrentSelection = null;
    ShowSleepUi = true;
    _exitStartedAt = -1f;
    gameObject.SetActive(false);
    SetSeparatorVisible(true);

    if (_vanillaImage) {
      _vanillaImage.enabled = true;
    }

    _effects.Apply(ScreenConfigLoader.Current.Defaults, null);
  }

  public void RefreshAppearance() {
    ScreenConfig config = ScreenConfigLoader.Current;
    Layout layout = CurrentSelection?.LayoutName != null
        && config.Layouts.TryGetValue(CurrentSelection.LayoutName, out Layout named) ? named : null;
    _effects.Apply(config.Defaults, layout);

    if (Separator) {
      LoadingManager.SetupPanelSeparator(Separator);
      SetSeparatorVisible(!IsShowing || (!_layoutShowsSeparator && ScreenConfigLoader.Current.Defaults.Separator.OnLayouts));
    }

    if (!IsShowing) {
      return;
    }

    foreach (PlacedImage placed in _placed) {
      if (placed.Layer.FromEntry) {
        Color color = ToColor(ScreenConfigLoader.Current.Defaults.Tint);
        color.a *= placed.Placement.Opacity ?? 1f;
        placed.Rect.GetComponent<Image>().color = color;
      }
    }

    foreach (PlacedText placed in _texts) {
      placed.Text.fontSize = Mathf.Max(placed.BaseSize, PluginConfig.MinimumTextSize.Value);
      placed.Padding = placed.Style.Background.HasValue || placed.Style.AutoBackground || PluginConfig.ReadableText.Value
          ? TextBackgroundPadding : 0f;
      placed.Text.rectTransform.offsetMin = new(placed.Padding, placed.Padding);
      placed.Text.rectTransform.offsetMax = new(-placed.Padding, -placed.Padding);
    }
    Relayout();

    RenderAnimations(Time.realtimeSinceStartup);
  }

  void SetSeparatorVisible(bool visible) {
    if (Separator) {
      Separator.gameObject.SetActive(visible && ScreenConfigLoader.Current.Defaults.Separator.Show);
    }
  }

  void LateUpdate() {
    if (!IsShowing) return;
    if (_root.rect.size != _lastSize) Relayout();
    float now = Time.realtimeSinceStartup;
    RenderAnimations(now);
    if (CurrentSelection == LoadingManager.Session.Selection) {
      LoadingManager.Session.Tick(IdleElapsed(now));
    }
  }

  void OnRectTransformDimensionsChange() {
    if (IsShowing && _root && _root.rect.size != _lastSize) {
      Relayout();
    }
  }

  static Layout _defaultLayout;

  static Layout DefaultLayout {
    get {
      if (_defaultLayout == null) {
        _defaultLayout = new() { Name = "default" };
        _defaultLayout.Layers.Add(new() { Kind = LayerKind.Image, FromEntry = true });
      }

      return _defaultLayout;
    }
  }

  static string PickFromPool(Layer layer) {
    if (layer.Images.Count == 0) {
      return null;
    }

    List<string> candidates = [.. layer.Images];

    if (candidates.Count > 1 && _lastPoolPick.TryGetValue(layer, out string last)) {
      candidates.Remove(last);
    }

    string picked = candidates[Random.Range(0, candidates.Count)];
    _lastPoolPick[layer] = picked;

    return picked;
  }

  void AddSpinnerLayer(Layer layer, Layout layout) {
    Sprite sprite = layer.SpinnerStyle switch {
      SpinnerStyle.Hammer => SceneLoaderPatch.SpinnerSprite,
      SpinnerStyle.Ring => SpinnerSprites.Ring,
      SpinnerStyle.Dots => SpinnerSprites.Dots,
      _ => CustomAssets.ReadLoadingImage(ScreenConfigLoader.ResolveImagePath(layer.Images.FirstOrDefault())),
    };
    if (!sprite) return;

    Placement placement = Placement.Merge(
        layer.Placement, layout.Placement, new() { Fit = new() { Mode = FitMode.None } },
        ScreenConfigLoader.Current.Defaults);
    Vector2? imageSize = layer.SpinnerStyle == SpinnerStyle.Hammer && SceneLoaderPatch.SpinnerSize.x > 0f
        && SceneLoaderPatch.SpinnerSize.y > 0f ? SceneLoaderPatch.SpinnerSize : null;
    AddImageLayer(layer, sprite, placement, imageSize: imageSize);
    HasSpinner = true;
  }

  void AddColorLayer(Layer layer) {
    Image image = CreateChild<Image>($"Color-{layer.Id}", _root);
    RectTransform rect = image.rectTransform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.sizeDelta = Vector2.zero;
    image.color = ToColor(layer.Color);
    image.raycastTarget = false;
    RegisterAnimation(layer, rect);
  }

  public bool ShowsOwnText => _texts.Count > 0;

  public void SetCaption(string caption) {
    foreach (PlacedText placed in _texts) {
      placed.Text.SetText(caption);
    }

    if (_texts.Count > 0) {
      Relayout();
    }
  }

  void AddTextLayer(Layer layer, Placement placement, string caption, TMP_Text template) {
    RectTransform container = CreateChild<RectTransform>($"Text-{layer.Id}", _root);
    TextStyle style = layer.Text ?? new();
    float padding = 0f;

    Image background = container.gameObject.AddComponent<Image>();
    background.color = style.Background.HasValue ? ToColor(style.Background.Value) : Color.clear;
    background.raycastTarget = false;
    if (style.Background.HasValue || style.AutoBackground || PluginConfig.ReadableText.Value) padding = TextBackgroundPadding;

    TMP_Text text = Instantiate(template, container, worldPositionStays: false);
    LoadingManager.MatchTipFont(text);
    text.name = "Caption";
    text.enabled = true;
    text.gameObject.SetActive(true);
    text.textWrappingMode = TextWrappingModes.Normal;
    text.raycastTarget = false;
    text.alignment = (style.Align ?? TextAlign.Center) switch {
      TextAlign.Left => TextAlignmentOptions.TopLeft,
      TextAlign.Right => TextAlignmentOptions.TopRight,
      _ => TextAlignmentOptions.Top,
    };

    text.fontSize = Mathf.Max(style.FontSize ?? template.fontSize, PluginConfig.MinimumTextSize.Value);

    Color color = style.Color.HasValue ? ToColor(style.Color.Value) : template.color;
    Color32 baseColor = color;
    color.a *= placement.Opacity ?? 1f;
    text.color = color;
    text.SetText(caption);

    RectTransform textRect = text.rectTransform;
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.pivot = new(0.5f, 0.5f);
    textRect.anchoredPosition = Vector2.zero;
    textRect.offsetMin = new(padding, padding);
    textRect.offsetMax = new(-padding, -padding);

    if (!_hiddenTemplate) {
      _hiddenTemplate = template;
      template.enabled = false;
    }

    _texts.Add(new() { Rect = container, Text = text, Placement = placement, Padding = padding,
        Style = style, Background = background,
        BaseColor = new(baseColor.r, baseColor.g, baseColor.b, baseColor.a),
        BaseSize = style.FontSize ?? template.fontSize });
    RegisterAnimation(layer, container);
  }

  void AddImageLayer(Layer layer, Sprite sprite, Placement placement, bool defaultEntry = false,
      Vector2? imageSize = null) {
    Image image = CreateChild<Image>($"Image-{layer.Id ?? sprite.name}", _root);
    image.sprite = sprite;
    image.type = Image.Type.Simple;
    image.preserveAspect = false;
    image.raycastTarget = false;

    Color color = layer.FromEntry ? ToColor(ScreenConfigLoader.Current.Defaults.Tint) : layer.HasTint ? ToColor(layer.Color) : Color.white;
    color.a *= placement.Opacity ?? 1f;
    image.color = color;

    PlacedImage placed = new() {
      Rect = image.rectTransform,
      Layer = layer,
      Placement = placement,
      ImageSize = imageSize ?? new(sprite.rect.width, sprite.rect.height),
    };

    if (placement.Fade != null) {
      Color fadeColor = placement.Fade.Color.HasValue ? ToColor(placement.Fade.Color.Value) : Color.black;
      FadeCurve curve = placement.Fade.Curve ?? FadeCurve.Smooth;

      foreach (Sides side in new[] { Sides.Left, Sides.Right, Sides.Top, Sides.Bottom }) {
        if ((placement.Fade.Sides & side) == 0) {
          continue;
        }

        RawImage fade = CreateChild<RawImage>($"Fade-{side}", image.rectTransform);
        fade.texture = GradientTextures.Get(side is Sides.Left or Sides.Right, curve);
        fade.uvRect = side switch {
          Sides.Right => new(1f, 0f, -1f, 1f),
          Sides.Bottom => new(0f, 1f, 1f, -1f),
          _ => new(0f, 0f, 1f, 1f),
        };
        fade.color = fadeColor;
        fade.raycastTarget = false;

        placed.Fades.Add((side, fade.rectTransform, placement.Fade.Size));
      }
    }

    _placed.Add(placed);
    RegisterAnimation(layer, image.rectTransform, defaultEntry);
  }

  void OnDestroy() {
    CustomAssets.Pin(this, []);
    _effects?.Destroy();
  }

  void OnEnable() {
    if (IsShowing) {
      if (this == LoadingManager.FejdStartupHost && LoadingManager.Session.Stage == LoadingStage.Menu
          && !LoadingManager.Session.WasVisible) {
        _animationStartedAt = Time.realtimeSinceStartup;
        _idleBase = 0f;
        LoadingManager.Session.MarkVisible(_animationStartedAt);
      }
      RenderAnimations(Time.realtimeSinceStartup);
    }
  }

  void RegisterAnimation(Layer layer, RectTransform rect, bool defaultEntry = false) {
    ScreenDefaults defaults = ScreenConfigLoader.Current.Defaults;
    AnimationSpec fallback = AnimationSpec.Merge(
        defaults.Animation, defaultEntry ? new() { Idle = "zoom-in" } : null);
    AnimationSpec spec = AnimationSpec.Merge(layer.Animation, fallback);
    AnimationCatalog catalog = defaults.Animations;
    AnimatedLayer animated = new() {
      Rect = rect,
      Group = rect.gameObject.AddComponent<CanvasGroup>(),
      Enter = catalog.Resolve(spec.Enter, LoadingScreen.LogWarning),
      Idle = catalog.Resolve(spec.Idle, LoadingScreen.LogWarning),
      Exit = catalog.Resolve(spec.Exit, LoadingScreen.LogWarning),
      Speed = spec.Speed ?? 1f,
    };
    _animated.Add(animated);
    if (spec.Enter != "none") _enterDuration = Mathf.Max(_enterDuration, animated.Enter.EffectiveDuration(animated.Speed));
    if (spec.Exit != "none") _exitDuration = Mathf.Max(_exitDuration, animated.Exit.EffectiveDuration(animated.Speed));
  }

  float IdleElapsed(float now) =>
      _exitStartedAt >= 0f ? _idleAtExit
          : _idleBase + Mathf.Max(0f, now - _animationStartedAt - (_skipEnter ? 0f : _enterDuration));

  public bool BeginExit(float now) {
    if (!IsShowing || _exitDuration <= 0f) return false;
    if (_exitStartedAt < 0f) {
      _idleAtExit = IdleElapsed(now);
      _exitStartedAt = now;
    }
    return now - _exitStartedAt < _exitDuration;
  }

  void RenderAnimations(float now) {
    if (!IsShowing) return;
    float elapsed = Mathf.Max(0f, now - _animationStartedAt);
    float idleElapsed = IdleElapsed(now);
    float exitElapsed = _exitStartedAt < 0f ? -1f : now - _exitStartedAt;
    foreach (AnimatedLayer layer in _animated) {
      if (!layer.Rect) continue;
      AnimationPreset preset;
      float phaseTime;
      bool playOnce;
      if (exitElapsed >= 0f) {
        preset = layer.Exit;
        phaseTime = exitElapsed;
        playOnce = true;
      } else if (!_skipEnter && elapsed < _enterDuration) {
        preset = layer.Enter;
        phaseTime = elapsed;
        playOnce = true;
      } else {
        preset = layer.Idle;
        phaseTime = idleElapsed;
        playOnce = false;
      }
      layer.Rect.anchoredPosition = layer.BasePosition;
      Vector3 pivotPosition = layer.Rect.localPosition;
      Vector2 pivot = layer.Rect.pivot;
      Vector2 center = new(
          pivotPosition.x + (0.5f - pivot.x) * layer.Rect.rect.width,
          pivotPosition.y + (0.5f - pivot.y) * layer.Rect.rect.height);
      AnimationSample sample = preset.Sample(
          phaseTime, _root.rect.width, _root.rect.height, layer.Rect.rect.width, layer.Rect.rect.height,
          center.x, center.y, layer.Speed, playOnce);
      layer.Rect.anchoredPosition = layer.BasePosition + new Vector2(sample.X, -sample.Y);
      layer.Rect.localScale = Vector3.one * sample.Scale;
      layer.Rect.localEulerAngles = new Vector3(0f, 0f, -sample.Rotation);
      layer.Group.alpha = sample.Opacity;
    }
  }

  void Relayout() {
    Vector2 size = _root.rect.size;
    _lastSize = size;
    foreach (AnimatedLayer layer in _animated) {
      layer.Rect.anchoredPosition = layer.BasePosition;
      layer.Rect.localScale = Vector3.one;
      layer.Rect.localEulerAngles = Vector3.zero;
    }

    foreach (PlacedText placed in _texts) {
      Fit fit = placed.Placement.Fit;
      float width = fit?.Mode == FitMode.Box && fit.Width.HasValue ? fit.Width.Value.Resolve(size.x) : size.x * 0.8f;
      float innerWidth = Mathf.Max(1f, width - (2f * placed.Padding));
      float height = placed.Text.GetPreferredValues(placed.Text.text, innerWidth, 0f).y + (2f * placed.Padding);

      Placement textPlacement = Placement.Merge(new() { Fit = new() { Mode = FitMode.None } }, placed.Placement);
      LayerRect textRect = PlacementSolver.Solve(textPlacement, size.x, size.y, width, height);
      Vector2 textAnchor = new(textRect.AnchorX, textRect.AnchorY);

      placed.Rect.anchorMin = textAnchor;
      placed.Rect.anchorMax = textAnchor;
      placed.Rect.pivot = textAnchor;
      placed.Rect.anchoredPosition = new(textRect.OffsetX, textRect.OffsetY);
      placed.Rect.sizeDelta = new(textRect.Width, textRect.Height);
    }

    foreach (PlacedImage placed in _placed) {
      LayerRect layerRect =
          PlacementSolver.Solve(placed.Placement, size.x, size.y, placed.ImageSize.x, placed.ImageSize.y);

      Vector2 anchor = new(layerRect.AnchorX, layerRect.AnchorY);
      placed.Rect.anchorMin = anchor;
      placed.Rect.anchorMax = anchor;
      placed.Rect.pivot = anchor;
      placed.Rect.anchoredPosition = new(layerRect.OffsetX, layerRect.OffsetY);
      placed.Rect.sizeDelta = new(layerRect.Width, layerRect.Height);

      foreach ((Sides side, RectTransform fade, Length? fadeSize) in placed.Fades) {
        bool horizontal = side is Sides.Left or Sides.Right;
        float thickness = PlacementSolver.FadeSize(fadeSize, horizontal ? layerRect.Width : layerRect.Height);

        (Vector2 min, Vector2 max) = side switch {
          Sides.Left => (new Vector2(0f, 0f), new Vector2(0f, 1f)),
          Sides.Right => (new Vector2(1f, 0f), new Vector2(1f, 1f)),
          Sides.Top => (new Vector2(0f, 1f), new Vector2(1f, 1f)),
          _ => (new Vector2(0f, 0f), new Vector2(1f, 0f)),
        };

        fade.anchorMin = min;
        fade.anchorMax = max;
        fade.pivot = new(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
        fade.anchoredPosition = Vector2.zero;
        fade.sizeDelta = horizontal ? new(thickness, 0f) : new(0f, thickness);
      }
    }

    foreach (PlacedText placed in _texts) UpdateTextColor(placed);
    foreach (AnimatedLayer layer in _animated) layer.BasePosition = layer.Rect.anchoredPosition;
  }

  void UpdateTextColor(PlacedText placed) {
    ColorValue image = ScreenConfigLoader.Current.Defaults.Background;
    ColorValue[] topSamples = null;
    ColorValue topBacking = image;
    int topSampleSize = 0;
    float sampleLeft = 0f, sampleBottom = 0f, sampleRight = 0f, sampleTop = 0f;
    Vector3[] textCorners = new Vector3[4];
    placed.Rect.GetWorldCorners(textCorners);
    int textIndex = placed.Rect.GetSiblingIndex();
    for (int i = 0; i < textIndex; i++) {
      if (!_root.GetChild(i).TryGetComponent(out Image graphic)) continue;
      if (_root.GetChild(i).name.StartsWith("Text-")) continue;
      if (graphic.sprite == null) {
        Color32 fill = graphic.color;
        image = ColorMath.Blend(new(fill.r, fill.g, fill.b, fill.a), image);
        topSamples = null;
        continue;
      }
      if (!CustomAssets.TryGetImageSamples(graphic.sprite, out ColorValue[] samples, out int sampleSize)) continue;
      Vector3[] imageCorners = new Vector3[4];
      graphic.rectTransform.GetWorldCorners(imageCorners);
      float left = Mathf.Max(textCorners[0].x, imageCorners[0].x);
      float right = Mathf.Min(textCorners[2].x, imageCorners[2].x);
      float bottom = Mathf.Max(textCorners[0].y, imageCorners[0].y);
      float top = Mathf.Min(textCorners[2].y, imageCorners[2].y);
      if (left >= right || bottom >= top) continue;
      float width = imageCorners[2].x - imageCorners[0].x;
      float height = imageCorners[2].y - imageCorners[0].y;
      if (width <= 0f || height <= 0f) continue;
      ColorValue sample = ColorMath.AverageArea(samples, sampleSize, sampleSize,
          (left - imageCorners[0].x) / width, (bottom - imageCorners[0].y) / height,
          (right - imageCorners[0].x) / width, (top - imageCorners[0].y) / height);
      Color32 tint = graphic.color;
      float coverage = (right - left) * (top - bottom) /
          ((textCorners[2].x - textCorners[0].x) * (textCorners[2].y - textCorners[0].y));
      if (coverage > 0.999f && tint.r == 255 && tint.g == 255 && tint.b == 255 && tint.a == 255) {
        topSamples = samples;
        topBacking = image;
        topSampleSize = sampleSize;
        sampleLeft = (left - imageCorners[0].x) / width;
        sampleBottom = (bottom - imageCorners[0].y) / height;
        sampleRight = (right - imageCorners[0].x) / width;
        sampleTop = (top - imageCorners[0].y) / height;
      } else {
        topSamples = null;
      }
      image = ColorMath.Blend(new(
          (byte) (sample.R * tint.r / 255), (byte) (sample.G * tint.g / 255),
          (byte) (sample.B * tint.b / 255), (byte) (sample.A * tint.a * coverage / 255)), image);
    }

    TextStyle style = placed.Style;
    double imageLuminance = topSamples == null ? ColorMath.RelativeLuminance(image)
        : ColorMath.AverageLuminance(topSamples, topSampleSize, topSampleSize,
            sampleLeft, sampleBottom, sampleRight, sampleTop, backing: topBacking);
    ColorValue? box = style.AutoBackground
        ? ColorMath.AutoBackground(imageLuminance) : style.Background;
    ColorValue effective = box.HasValue ? ColorMath.Blend(box.Value, image) : image;
    double effectiveLuminance = topSamples == null ? ColorMath.RelativeLuminance(effective)
        : ColorMath.AverageLuminance(topSamples, topSampleSize, topSampleSize,
            sampleLeft, sampleBottom, sampleRight, sampleTop, box, topBacking);
    ColorValue text = style.AutoColor ? ColorMath.AutoText(effectiveLuminance) : placed.BaseColor;
    if (PluginConfig.ReadableText.Value) {
      (text, box) = ColorMath.Readable(text, box, image,
          topSamples == null ? null : candidate => ColorMath.AverageLuminance(
              topSamples, topSampleSize, topSampleSize,
              sampleLeft, sampleBottom, sampleRight, sampleTop, candidate, topBacking));
    }
    placed.Background.color = box.HasValue ? ToColor(box.Value) : Color.clear;
    Color textColor = ToColor(text);
    if (!PluginConfig.ReadableText.Value) textColor.a *= placed.Placement.Opacity ?? 1f;
    placed.Text.color = textColor;
  }

  void Clear() {
    CustomAssets.Pin(this, []);
    StopAllCoroutines();
    _placed.Clear();
    HasSpinner = false;
    HasExplicitSpinner = false;
    _animated.Clear();
    _enterDuration = 0f;
    _exitDuration = 0f;
    _texts.Clear();

    if (_hiddenTemplate) {
      _hiddenTemplate.enabled = true;
      _hiddenTemplate = null;
    }

    for (int i = _root.childCount - 1; i >= 0; i--) {
      Destroy(_root.GetChild(i).gameObject);
    }
  }

  static T CreateChild<T>(string name, Transform parent) where T : Component {
    GameObject child = new(name, typeof(RectTransform));
    child.transform.SetParent(parent, worldPositionStays: false);
    return child.TryGetComponent(out T existing) ? existing : child.AddComponent<T>();
  }

  static Color ToColor(ColorValue value) {
    return new Color32(value.R, value.G, value.B, value.A);
  }
}

static class GradientTextures {
  const int Resolution = 256;

  static readonly Dictionary<(bool, FadeCurve), Texture2D> _cache = [];

  public static Texture2D Get(bool horizontal, FadeCurve curve) {
    if (_cache.TryGetValue((horizontal, curve), out Texture2D cached) && cached) {
      return cached;
    }

    Texture2D texture =
        new(horizontal ? Resolution : 1, horizontal ? 1 : Resolution, TextureFormat.RGBA32, mipChain: false) {
          name = $"loadingscreen.fade-{(horizontal ? "h" : "v")}-{curve}",
          wrapMode = TextureWrapMode.Clamp,
          filterMode = FilterMode.Bilinear,
        };

    for (int i = 0; i < Resolution; i++) {
      float t = i / (Resolution - 1f);
      float alpha = curve == FadeCurve.Smooth ? 1f - (t * t * (3f - (2f * t))) : 1f - t;
      Color color = new(1f, 1f, 1f, alpha);

      if (horizontal) {
        texture.SetPixel(i, 0, color);
      } else {
        texture.SetPixel(0, Resolution - 1 - i, color);
      }
    }

    texture.Apply();
    _cache[(horizontal, curve)] = texture;

    return texture;
  }
}
