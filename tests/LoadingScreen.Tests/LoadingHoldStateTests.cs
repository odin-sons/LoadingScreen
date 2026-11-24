namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class LoadingHoldStateTests {
  [Fact]
  public void ExitHoldEndsWhenAnimationFinishesOrMaximumExpires() {
    LoadingHoldState state = new();
    Assert.False(state.Update(true, false, true, false, false, 0f, 2, true));
    Assert.True(state.Update(true, true, true, false, false, 1f, 2, true));
    Assert.True(state.Update(true, true, true, false, false, 1.5f, 2, true));
    Assert.False(state.Update(true, true, true, false, false, 1.6f, 2));
    Assert.False(state.Update(true, false, true, false, false, 2f, 2));
    Assert.True(state.Update(true, true, true, false, false, 3f, 2, true));
    Assert.False(state.Update(true, true, true, false, false, 5f, 2, true));
  }

  [Fact]
  public void NothingToWaitForClosesTheMomentLoadingEnds() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, false, 0f, 30));
    Assert.False(state.Update(true, true, true, false, false, 1f, 30));
    Assert.False(state.Holding);
    Assert.False(state.Update(true, true, true, false, false, 2f, 30));
  }

  [Fact]
  public void HoldsOnlyAfterLoadingAndTimesOutAtTheMaximum() {
    LoadingHoldState state = new();

    Assert.False(state.Update(false, true, true, true, false, 0f, 30));
    Assert.False(state.Update(true, false, true, true, false, 5f, 30));
    Assert.True(state.Update(true, true, true, true, false, 10f, 30));
    Assert.True(state.Update(true, true, true, true, false, 39.9f, 30));
    Assert.False(state.Update(true, true, true, true, false, 40f, 30));
    Assert.False(state.Update(true, true, true, true, false, 41f, 30));
  }

  [Fact]
  public void InputDismissesTheHoldWithoutStartingItAgain() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, false, 0f, 30));
    Assert.True(state.Update(true, true, true, true, false, 1f, 30));
    Assert.True(state.Holding);
    Assert.False(state.Update(true, true, true, true, true, 2f, 30));
    Assert.False(state.Holding);
    Assert.False(state.Update(true, true, true, true, false, 3f, 30));
  }

  [Fact]
  public void DisablingOrInterruptingTheHoldClearsIt() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, false, 0f, 30));
    Assert.True(state.Update(true, true, true, true, false, 1f, 30));
    Assert.False(state.Update(true, true, false, true, false, 2f, 30));
    Assert.False(state.Update(true, true, true, true, false, 3f, 30));

    Assert.False(state.Update(true, false, true, true, false, 4f, 30));
    Assert.True(state.Update(true, true, true, true, false, 5f, 30));
    Assert.False(state.Update(false, false, true, true, false, 6f, 30));
    Assert.False(state.Update(false, true, true, true, false, 7f, 30));
  }

  [Fact]
  public void VoiceWaitEndsOnKeyOrWhenPlaybackFinishes() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, false, 0f, 30));
    Assert.True(state.Update(true, true, true, true, false, 1f, 30));
    Assert.True(state.Update(true, true, true, true, false, 2f, 30));
    Assert.False(state.Update(true, true, true, false, false, 3f, 30));

    Assert.False(state.Update(true, false, true, true, false, 4f, 30));
    Assert.True(state.Update(true, true, true, true, false, 5f, 30));
    Assert.False(state.Update(true, true, true, true, true, 6f, 30));
  }

  [Fact]
  public void VoiceWaitDoesNotStartWithoutAnActiveVoiceAndIsCapped() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, false, 0f, 2));
    Assert.False(state.Update(true, true, true, false, false, 1f, 2));

    Assert.False(state.Update(true, false, true, true, false, 3f, 2));
    Assert.True(state.Update(true, true, true, true, false, 4f, 2));
    Assert.True(state.Update(true, true, true, true, false, 5.9f, 2));
    Assert.False(state.Update(true, true, true, true, false, 6f, 2));
  }
}
