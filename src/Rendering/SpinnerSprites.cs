namespace LoadingScreen;

using global::LoadingScreen.Model;

using UnityEngine;

static class SpinnerSprites {
  const int Size = 64;
  static Sprite _ring;
  static Sprite _dots;

  public static Sprite Ring => _ring ? _ring : _ring = Create(SpinnerStyle.Ring);
  public static Sprite Dots => _dots ? _dots : _dots = Create(SpinnerStyle.Dots);

  static Sprite Create(SpinnerStyle style) {
    Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false) {
      name = $"LoadingScreen-{style}",
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp,
    };
    Color32[] pixels = new Color32[Size * Size];

    for (int y = 0; y < Size; y++) {
      for (int x = 0; x < Size; x++) {
        float dx = x + 0.5f - Size / 2f;
        float dy = y + 0.5f - Size / 2f;
        float alpha = style == SpinnerStyle.Ring ? RingAlpha(dx, dy) : DotsAlpha(dx, dy);
        pixels[y * Size + x] = new(255, 255, 255, (byte) Mathf.RoundToInt(alpha * 255f));
      }
    }

    texture.SetPixels32(pixels);
    texture.Apply();
    return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
  }

  static float RingAlpha(float dx, float dy) {
    float radius = Mathf.Sqrt(dx * dx + dy * dy);
    float edge = Mathf.Clamp01(Mathf.Min(radius - 17f, 23f - radius));
    float angle = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);
    if (angle < 0f) angle += 1f;
    return edge * Mathf.Clamp01((angle - 0.1f) / 0.75f);
  }

  static float DotsAlpha(float dx, float dy) {
    float alpha = 0f;
    for (int i = 0; i < 8; i++) {
      float angle = i * Mathf.PI / 4f;
      float x = dx - Mathf.Cos(angle) * 20f;
      float y = dy - Mathf.Sin(angle) * 20f;
      float distance = Mathf.Sqrt(x * x + y * y);
      alpha = Mathf.Max(alpha, Mathf.Clamp01(4f - distance) * (i + 1) / 8f);
    }
    return alpha;
  }
}
