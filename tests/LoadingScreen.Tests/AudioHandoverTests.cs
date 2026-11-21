namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class AudioHandoverTests {
  [Fact]
  public void MatchingMusicPathKeepsPlaybackAcrossHosts() {
    Assert.True(AudioHandover.KeepMusic("music.ogg", "music.ogg", true));
    Assert.False(AudioHandover.KeepMusic("music.ogg", "other.ogg", true));
    Assert.False(AudioHandover.KeepMusic("music.ogg", "music.ogg", false));
  }

  [Fact]
  public void VoicePlaysOncePerEntryUntilNewSession() {
    AudioHandover handover = new();
    object first = new();
    object second = new();

    Assert.True(handover.ShouldPlayVoice(first, false));
    Assert.False(handover.ShouldPlayVoice(first, true));
    Assert.True(handover.ShouldPlayVoice(second, true));
    Assert.True(handover.ShouldPlayVoice(first, false));
  }

  [Fact]
  public void FadeStaysAtPreviousLevelUntilNewHostCatchesUp() {
    AudioHandover handover = new();
    handover.Begin(0.8f);

    Assert.Equal(0.8f, handover.Opacity(0.1f));
    Assert.Equal(0.8f, handover.Opacity(0.7f));
    Assert.Equal(0.9f, handover.Opacity(0.9f));
    Assert.Equal(0.3f, handover.Opacity(0.3f));
  }

  [Fact]
  public void BriefGapBetweenHostsKeepsSessionAlive() {
    AudioHandover handover = new();

    Assert.False(handover.ShouldEnd(false, 10f, 3f));
    Assert.False(handover.ShouldEnd(false, 12f, 3f));
    Assert.False(handover.ShouldEnd(true, 12.5f, 3f));
    Assert.False(handover.ShouldEnd(false, 20f, 3f));
    Assert.True(handover.ShouldEnd(false, 23f, 3f));
  }
}
