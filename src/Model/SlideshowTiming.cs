namespace LoadingScreen.Model;

public static class SlideshowTiming {
  public static bool IsDue(float now, float lastImageTime, int intervalSeconds) {
    return intervalSeconds > 0 && now - lastImageTime >= intervalSeconds;
  }
}
