namespace LoadingScreen.Model;

public static class SoundSelection {
  public static string MusicFor(Selection selection, ScreenConfig config) {
    if (selection == null) return null;

    string layoutSound = selection.LayoutName != null
        && config.Layouts.TryGetValue(selection.LayoutName, out Layout layout) ? layout.Sound : null;
    return selection.Entry.Sound ?? selection.List.Sound ?? layoutSound;
  }

  public static string VoiceFor(Selection selection, bool showsEntryText) {
    return showsEntryText ? selection?.Entry.Voice : null;
  }
}
