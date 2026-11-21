namespace LoadingScreen;

using global::LoadingScreen.Model;

using UnityEngine;
using UnityEngine.UI;

sealed class ScreenEffectOverlay {
  const int Resolution = 256;

  readonly RectTransform _root;
  readonly Image _mask;
  readonly RawImage _vignette;
  Texture2D _texture;
  float _textureSize = -1f;

  public ScreenEffectOverlay(Transform parent) {
    GameObject root = new("LoadingScreenEffects", typeof(RectTransform));
    root.transform.SetParent(parent, worldPositionStays: false);
    _root = root.GetComponent<RectTransform>();
    _root.anchorMin = Vector2.zero;
    _root.anchorMax = Vector2.one;
    _root.pivot = new(0.5f, 0.5f);
    _root.anchoredPosition = Vector2.zero;
    _root.sizeDelta = Vector2.zero;

    _mask = AddGraphic<Image>("Mask");
    _vignette = AddGraphic<RawImage>("Vignette");
    _root.gameObject.SetActive(false);
  }

  public void Apply(ScreenDefaults defaults, Layout layout) {
    MaskSettings mask = ScreenEffects.ResolveMask(defaults.Mask, layout?.Mask);
    VignetteSettings vignette = ScreenEffects.ResolveVignette(defaults.Vignette, layout?.Vignette);
    bool showMask = mask != null && mask.Opacity > 0f && mask.Color.Value.A > 0;
    bool showVignette = vignette != null && vignette.Strength > 0f && vignette.Color.Value.A > 0;

    _mask.gameObject.SetActive(showMask);
    if (showMask) {
      Color color = ToColor(mask.Color.Value);
      color.a *= mask.Opacity.Value;
      _mask.color = color;
    }

    _vignette.gameObject.SetActive(showVignette);
    if (showVignette) {
      float size = vignette.Size.Value;
      if (_texture == null || _textureSize != size) {
        if (_texture) Object.Destroy(_texture);
        _texture = MakeVignetteTexture(size);
        _textureSize = size;
      }

      _vignette.texture = _texture;
      Color color = ToColor(vignette.Color.Value);
      color.a *= vignette.Strength.Value;
      _vignette.color = color;
    }

    _root.gameObject.SetActive(showMask || showVignette);
    if (_root.gameObject.activeSelf) _root.SetAsLastSibling();
  }

  public void Destroy() {
    if (_texture) Object.Destroy(_texture);
    if (_root) Object.Destroy(_root.gameObject);
  }

  T AddGraphic<T>(string name) where T : Graphic {
    GameObject child = new(name, typeof(RectTransform));
    child.transform.SetParent(_root, worldPositionStays: false);
    RectTransform rect = child.GetComponent<RectTransform>();
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.pivot = new(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = Vector2.zero;
    T graphic = child.AddComponent<T>();
    graphic.raycastTarget = false;
    return graphic;
  }

  static Texture2D MakeVignetteTexture(float size) {
    Texture2D texture = new(Resolution, Resolution, TextureFormat.RGBA32, mipChain: false) {
      name = "loadingscreen.vignette",
      wrapMode = TextureWrapMode.Clamp,
      filterMode = FilterMode.Bilinear,
    };
    Color32[] pixels = new Color32[Resolution * Resolution];
    for (int y = 0; y < Resolution; y++) {
      float ny = Mathf.Abs((2f * y / (Resolution - 1f)) - 1f);
      for (int x = 0; x < Resolution; x++) {
        float nx = Mathf.Abs((2f * x / (Resolution - 1f)) - 1f);
        byte alpha = (byte) Mathf.RoundToInt(255f * ScreenEffects.VignetteAlpha(nx, ny, size, 1f));
        pixels[(y * Resolution) + x] = new Color32(255, 255, 255, alpha);
      }
    }

    texture.SetPixels32(pixels);
    texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
    return texture;
  }

  static Color ToColor(ColorValue value) {
    return new Color32(value.R, value.G, value.B, value.A);
  }
}
