namespace LoadingScreen.Tests;

using System.Linq;

using LoadingScreen.Model;

using Xunit;

public class AudioFileTests {
  [Fact]
  public void ListsEveryDistinctAudioReference() {
    ScreenConfig config = new();
    config.Layouts["forest"] = new() { Sound = "music/a.ogg" };
    config.General.Sound = "music/a.ogg";
    config.General.Entries.Add(new() { Sound = "music/b.wav", Voice = "voice/a.ogg" });

    Assert.Equal(new[] { "music/a.ogg", "music/b.wav", "voice/a.ogg" }, AudioFile.Paths(config).ToArray());
  }

  [Fact]
  public void AcceptsOnlyBoundedOggAndWaveHeaders() {
    Assert.True(AudioFile.IsValid("a.ogg", new byte[] { 79, 103, 103, 83, 0, 0, 0, 0, 0, 0, 0, 0 }));
    Assert.True(AudioFile.IsValid("a.wav", new byte[] { 82, 73, 70, 70, 0, 0, 0, 0, 87, 65, 86, 69 }));
    Assert.False(AudioFile.IsValid("a.ogg", new byte[12]));
    Assert.False(AudioFile.IsValid("a.wav", new byte[AudioFile.MaxBytes + 1]));
  }
}
