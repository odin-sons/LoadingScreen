namespace LoadingScreen.Model;

public static class ZoomTiming {
  public static float Progress(float elapsed, float duration) {
    if (duration <= 0f) return 1f;
    float t = elapsed / duration;
    if (t < 0f) t = 0f;
    if (t > 1f) t = 1f;
    return t * t * (3f - (2f * t));
  }
}
