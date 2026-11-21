namespace LoadingScreen.Model;

public static class TipSelection {
  public static bool HasPrevious(int count, int previous) {
    return count > 1 && previous >= 0 && previous < count;
  }

  public static int NextIndex(int count, int previous, int choice) {
    if (count <= 0) return -1;
    return HasPrevious(count, previous) && choice >= previous ? choice + 1 : choice;
  }
}
