namespace LoadingScreen.Model;

public static class TipRotation {
  public static bool IsDue(float now, float lastTipTime, int intervalSeconds, string currentCaption, int tipCount) {
    return currentCaption == null && tipCount > 0 && SlideshowTiming.IsDue(now, lastTipTime, intervalSeconds);
  }
}
