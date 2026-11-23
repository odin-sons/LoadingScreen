namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class LoadingHoldStateTests {
  [Fact]
  public void ExitHoldEndsWhenAnimationFinishesOrMaximumExpires() {
    LoadingHoldState state = new();
    Assert.False(state.Update(true, false, true, false, false, false, 0f, 2, true).Holding);
    Assert.True(state.Update(true, true, true, false, false, false, 1f, 2, true).Holding);
    Assert.True(state.Update(true, true, true, false, false, false, 1.5f, 2, true).Holding);
    Assert.False(state.Update(true, true, true, false, false, false, 1.6f, 2).Holding);
    Assert.False(state.Update(true, false, true, false, false, false, 2f, 2).Holding);
    Assert.True(state.Update(true, true, true, false, false, false, 3f, 2, true).Holding);
    Assert.False(state.Update(true, true, true, false, false, false, 5f, 2, true).Holding);
  }
  [Fact]
  public void HoldsOnlyAfterLoadingAndTimesOutAtTheMaximum() {
    LoadingHoldState state = new();

    Assert.False(state.Update(false, true, true, true, false, false, 0f, 30).Holding);
    Assert.False(state.Update(true, false, true, true, false, false, 5f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, false, true, 10f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, false, false, 39.9f, 30).Holding);
    Assert.False(state.Update(true, true, true, true, false, false, 40f, 30).Holding);
    Assert.False(state.Update(true, true, true, true, false, false, 41f, 30).Holding);
  }

  [Fact]
  public void InputDismissesTheHoldWithoutStartingItAgain() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, false, false, 0f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, false, false, 1f, 30).ShowContinuePrompt);
    Assert.True(state.Holding);
    Assert.False(state.Update(true, true, true, true, false, true, 2f, 30).Holding);
    Assert.False(state.Holding);
    Assert.False(state.Update(true, true, true, true, false, false, 3f, 30).Holding);
  }

  [Fact]
  public void DisablingOrInterruptingTheHoldClearsIt() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, false, false, 0f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, false, false, 1f, 30).Holding);
    Assert.False(state.Update(true, true, false, true, false, false, 2f, 30).Holding);
    Assert.False(state.Update(true, true, true, true, false, false, 3f, 30).Holding);

    Assert.False(state.Update(true, false, true, true, false, false, 4f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, false, false, 5f, 30).Holding);
    Assert.False(state.Update(false, false, true, true, false, false, 6f, 30).Holding);
    Assert.False(state.Update(false, true, true, true, false, false, 7f, 30).Holding);
  }

  [Fact]
  public void VoiceWaitIgnoresInputAndEndsWhenPlaybackFinishes() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, true, false, 0f, 30).Holding);
    LoadingHoldDecision holding = state.Update(true, true, true, false, true, false, 1f, 30);
    Assert.True(holding.Holding);
    Assert.False(holding.ShowContinuePrompt);
    Assert.True(state.Update(true, true, true, false, true, true, 2f, 30).Holding);
    Assert.False(state.Update(true, true, true, false, false, false, 3f, 30).Holding);
    Assert.False(state.Update(true, true, true, false, true, false, 4f, 30).Holding);
  }

  [Fact]
  public void VoiceWaitDoesNotStartWithoutAnActiveVoiceAndIsCapped() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, false, false, 0f, 2).Holding);
    Assert.False(state.Update(true, true, true, false, false, false, 1f, 2).Holding);

    Assert.False(state.Update(true, false, true, false, true, false, 3f, 2).Holding);
    Assert.True(state.Update(true, true, true, false, true, false, 4f, 2).Holding);
    Assert.True(state.Update(true, true, true, false, true, false, 5.9f, 2).Holding);
    Assert.False(state.Update(true, true, true, false, true, false, 6f, 2).Holding);
  }

  [Fact]
  public void KeyAndVoiceHoldsFinishIndependently() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, true, true, false, 0f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, true, false, 1f, 30).ShowContinuePrompt);
    LoadingHoldDecision afterKey = state.Update(true, true, true, true, true, true, 2f, 30);
    Assert.True(afterKey.Holding);
    Assert.False(afterKey.ShowContinuePrompt);
    Assert.False(state.Update(true, true, true, true, false, false, 3f, 30).Holding);

    Assert.False(state.Update(true, false, true, true, true, false, 4f, 30).Holding);
    Assert.True(state.Update(true, true, true, true, true, false, 5f, 30).Holding);
    LoadingHoldDecision afterVoice = state.Update(true, true, true, true, false, false, 6f, 30);
    Assert.True(afterVoice.Holding);
    Assert.True(afterVoice.ShowContinuePrompt);
  }
}
