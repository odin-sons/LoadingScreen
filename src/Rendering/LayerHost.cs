namespace LoadingScreen;

using System.Collections.Generic;
using System.Linq;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using static PluginConfig;

public sealed class LayerHost : MonoBehaviour {
  sealed class PlacedImage {
    public RectTransform Rect;
    public Placement Placement;
    public Vector2 ImageSize;
    public readonly List<(Sides Side, RectTransform Rect, Length? Size)> Fades = [];
  }

  sealed class PlacedText {
    public RectTransform Rect;
    public TMP_Text Text;
    public Placement Placement;
    public float Padding;
  }

  const float TextBackgroundPadding = 12f;

  static readonly Dictionary<Layer, string> _lastPoolPick = [];

  readonly List<PlacedText> _texts = [];
  TMP_Text _hiddenTemplate;

  readonly List<PlacedImage> _placed = [];
  readonly List<Transform> _animated = [];
  RectTransform _root;
  Graphic _vanillaImage;
  Vector2 _lastSize;

  public bool IsShowing { get; private set; }

  public Transform Separator { get; set; }

  static readonly Placement _separatorDefaults = new() {
    Fit = new() { Mode = FitMode.None },
    Anchor = Anchor.Bottom,
    Y = new Length(-150f, LengthUnit.Pixels),
    Animate = false,
  };

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
    host._vanillaImage = vanillaImage;
    root.SetActive(false);

    return host;
  }

  public bool Show(Selection selection, string caption = null, TMP_Text textTemplate = null) {
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

      if (layer.GameSeparator) {
        if (LoadingManager.SeparatorSprite) {
          AddImageLayer(layer, LoadingManager.SeparatorSprite, Placement.Merge(layer.Placement, _separatorDefaults));
        }

        continue;
      }

      Sprite sprite;

      if (layer.FromEntry && selection.Entry.Image == null) {
        CustomAssets.GetRandomLoadingImage(out sprite);
      } else {
        string path = layer.FromEntry ? selection.Entry.Image : PickFromPool(layer);
        sprite = CustomAssets.ReadLoadingImage(ScreenConfigLoader.ResolveImagePath(path));
      }

      if (!sprite) {
        continue;
      }

      Placement placement =
          layer.FromEntry
              ? Placement.Merge(selection.Entry.Place, layer.Placement, layout.Placement, config.Defaults)
              : Placement.Merge(layer.Placement, layout.Placement, config.Defaults);

      AddImageLayer(layer, sprite, placement);
      drewEntryImage |= layer.FromEntry;
    }

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
            .Select(placed => placed.Rect.GetComponent<Image>().sprite)
            .Where(sprite => sprite != LoadingManager.SeparatorSprite));
    SetSeparatorVisible(!layout.ShowsSeparator);
    IsShowing = true;
    Relayout();
    StartAnimations();
    return true;
  }

  public void Hide() {
    Clear();
    IsShowing = false;
    gameObject.SetActive(false);
    SetSeparatorVisible(true);

    if (_vanillaImage) {
      _vanillaImage.enabled = true;
    }
  }

  void SetSeparatorVisible(bool visible) {
    if (Separator) {
      Separator.gameObject.SetActive(visible && LoadingScreenShowPanelSeparator.Value);
    }
  }

  void LateUpdate() {
    if (IsShowing && _root.rect.size != _lastSize) {
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

  void AddColorLayer(Layer layer) {
    Image image = CreateChild<Image>($"Color-{layer.Id}", _root);
    RectTransform rect = image.rectTransform;
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.sizeDelta = Vector2.zero;
    image.color = ToColor(layer.Color);
    image.raycastTarget = false;
  }

  public bool ShowsOwnText => _texts.Count > 0;

  void AddTextLayer(Layer layer, Placement placement, string caption, TMP_Text template) {
    RectTransform container = CreateChild<RectTransform>($"Text-{layer.Id}", _root);
    TextStyle style = layer.Text ?? new();
    float padding = 0f;

    if (style.Background.HasValue) {
      Image background = container.gameObject.AddComponent<Image>();
      background.color = ToColor(style.Background.Value);
      background.raycastTarget = false;
      padding = TextBackgroundPadding;
    }

    TMP_Text text = Instantiate(template, container, worldPositionStays: false);
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

    if (style.FontSize.HasValue) {
      text.fontSize = style.FontSize.Value;
    }

    Color color = style.Color.HasValue ? ToColor(style.Color.Value) : template.color;
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

    _texts.Add(new() { Rect = container, Text = text, Placement = placement, Padding = padding });
  }

  void AddImageLayer(Layer layer, Sprite sprite, Placement placement) {
    Image image = CreateChild<Image>($"Image-{layer.Id ?? sprite.name}", _root);
    image.sprite = sprite;
    image.type = Image.Type.Simple;
    image.preserveAspect = false;
    image.raycastTarget = false;

    Color color = layer.FromEntry ? LoadingImageBaseColor.Value : layer.HasTint ? ToColor(layer.Color) : Color.white;
    color.a *= placement.Opacity ?? 1f;
    image.color = color;

    PlacedImage placed = new() {
      Rect = image.rectTransform,
      Placement = placement,
      ImageSize = new(sprite.rect.width, sprite.rect.height),
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

    if (placement.Animate ?? LoadingImageUseScaleLerp.Value) {
      _animated.Add(image.transform);
    }
  }

  void OnDestroy() {
    CustomAssets.Pin(this, []);
  }

  void OnEnable() {
    if (IsShowing) {
      StartAnimations();
    }
  }

  void StartAnimations() {
    StopAllCoroutines();

    if (!isActiveAndEnabled) {
      return;
    }

    foreach (Transform target in _animated) {
      if (target) {
        StartCoroutine(
            ImageScaleUtils.ScaleLerp(
                target,
                Vector3.one,
                Vector3.one * LoadingImageScaleLerpEndScale.Value,
                LoadingImageScaleLerpDuration.Value));
      }
    }
  }

  void Relayout() {
    Vector2 size = _root.rect.size;
    _lastSize = size;

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
  }

  void Clear() {
    CustomAssets.Pin(this, []);
    StopAllCoroutines();
    _placed.Clear();
    _animated.Clear();
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
