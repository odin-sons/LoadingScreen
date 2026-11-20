namespace LoadingScreen;

using System.Collections.Generic;

using global::LoadingScreen.Screens;

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

  static readonly Dictionary<Layer, string> _lastPoolPick = [];

  readonly List<PlacedImage> _placed = [];
  RectTransform _root;
  Graphic _vanillaImage;
  Vector2 _lastSize;

  public bool IsShowing { get; private set; }

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

  public bool Show(Selection selection) {
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

      string path = layer.FromEntry ? selection.Entry.Image : PickFromPool(layer);
      Sprite sprite = path == null ? null : CustomAssets.ReadLoadingImage(ScreenConfigLoader.ResolveImagePath(path));

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

    if (!drewEntryImage && layout.ShowsEntryImage) {
      Hide();
      return false;
    }

    if (_vanillaImage) {
      _vanillaImage.enabled = false;
    }

    IsShowing = true;
    Relayout();
    return true;
  }

  public void Hide() {
    Clear();
    IsShowing = false;
    gameObject.SetActive(false);

    if (_vanillaImage) {
      _vanillaImage.enabled = true;
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

  void AddImageLayer(Layer layer, Sprite sprite, Placement placement) {
    Image image = CreateChild<Image>($"Image-{layer.Id ?? sprite.name}", _root);
    image.sprite = sprite;
    image.type = Image.Type.Simple;
    image.preserveAspect = false;
    image.raycastTarget = false;

    Color color = layer.FromEntry ? LoadingImageBaseColor.Value : Color.white;
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
      StartCoroutine(
          ImageScaleUtils.ScaleLerp(
              image.transform,
              Vector3.one,
              Vector3.one * LoadingImageScaleLerpEndScale.Value,
              LoadingImageScaleLerpDuration.Value));
    }
  }

  void Relayout() {
    Vector2 size = _root.rect.size;
    _lastSize = size;

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
    StopAllCoroutines();
    _placed.Clear();

    for (int i = _root.childCount - 1; i >= 0; i--) {
      Destroy(_root.GetChild(i).gameObject);
    }
  }

  static T CreateChild<T>(string name, Transform parent) where T : Component {
    GameObject child = new(name, typeof(RectTransform));
    child.transform.SetParent(parent, worldPositionStays: false);
    return child.AddComponent<T>();
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
