namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class LoadingHoldSafetyTests {
  [Theory]
  [InlineData(false, true, true, true, LoadingHoldSafetyAction.Keep)]
  [InlineData(true, true, false, false, LoadingHoldSafetyAction.Protect)]
  [InlineData(true, true, true, true, LoadingHoldSafetyAction.Protect)]
  [InlineData(true, false, true, true, LoadingHoldSafetyAction.CloseForPvp)]
  [InlineData(true, false, false, true, LoadingHoldSafetyAction.CloseForMonster)]
  [InlineData(true, false, false, false, LoadingHoldSafetyAction.Keep)]
  public void ChoosesSafetyAction(
      bool holding, bool invulnerable, bool pvp, bool monster, LoadingHoldSafetyAction expected) {
    Assert.Equal(expected, LoadingHoldSafety.Decide(holding, invulnerable, pvp, monster));
  }
}
