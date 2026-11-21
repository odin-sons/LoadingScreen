namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class SoundSelectionTests {
  [Fact]
  public void EntrySoundOverridesListAndLayoutSound() {
    ScreenConfig config = new();
    config.Layouts["forest"] = new() { Name = "forest", Sound = "music/layout.ogg" };
    EntryList list = new() { Sound = "music/list.wav" };
    Entry entry = new() { Sound = "music/entry.ogg", Voice = "voice/tip.wav" };
    Selection selection = new() { Entry = entry, List = list, LayoutName = "forest" };

    Assert.Equal("music/entry.ogg", SoundSelection.MusicFor(selection, config));
    entry.Sound = null;
    Assert.Equal("music/list.wav", SoundSelection.MusicFor(selection, config));
    list.Sound = null;
    Assert.Equal("music/layout.ogg", SoundSelection.MusicFor(selection, config));
    Assert.Equal("voice/tip.wav", SoundSelection.VoiceFor(selection, true));
    Assert.Null(SoundSelection.VoiceFor(selection, false));
  }
}
