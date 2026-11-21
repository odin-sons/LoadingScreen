namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class LoadingHoldStateTests {
  [Fact]
  public void HoldsOnlyAfterLoadingAndTimesOutAtTheMaximum() {
    LoadingHoldState state = new();

    Assert.False(state.Update(false, true, true, false, 0f, 30));
    Assert.False(state.Update(true, false, true, false, 5f, 30));
    Assert.True(state.Update(true, true, true, true, 10f, 30));
    Assert.True(state.Update(true, true, true, false, 39.9f, 30));
    Assert.False(state.Update(true, true, true, false, 40f, 30));
    Assert.False(state.Update(true, true, true, false, 41f, 30));
  }

  [Fact]
  public void InputDismissesTheHoldWithoutStartingItAgain() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, 0f, 30));
    Assert.True(state.Update(true, true, true, false, 1f, 30));
    Assert.False(state.Update(true, true, true, true, 2f, 30));
    Assert.False(state.Update(true, true, true, false, 3f, 30));
  }

  [Fact]
  public void DisablingOrInterruptingTheHoldClearsIt() {
    LoadingHoldState state = new();

    Assert.False(state.Update(true, false, true, false, 0f, 30));
    Assert.True(state.Update(true, true, true, false, 1f, 30));
    Assert.False(state.Update(true, true, false, false, 2f, 30));
    Assert.False(state.Update(true, true, true, false, 3f, 30));

    Assert.False(state.Update(true, false, true, false, 4f, 30));
    Assert.True(state.Update(true, true, true, false, 5f, 30));
    Assert.False(state.Update(false, false, true, false, 6f, 30));
    Assert.False(state.Update(false, true, true, false, 7f, 30));
  }
}
