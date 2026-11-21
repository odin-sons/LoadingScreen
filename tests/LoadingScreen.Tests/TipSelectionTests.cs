namespace LoadingScreen.Tests;

using LoadingScreen.Model;

using Xunit;

public class TipSelectionTests {
  [Theory]
  [InlineData(0, -1, 0, -1)]
  [InlineData(1, 0, 0, 0)]
  [InlineData(3, -1, 1, 1)]
  [InlineData(3, 1, 0, 0)]
  [InlineData(3, 1, 1, 2)]
  [InlineData(3, 2, 1, 1)]
  public void AvoidsRepeatingThePreviousTip(int count, int previous, int choice, int expected) {
    Assert.Equal(expected, TipSelection.NextIndex(count, previous, choice));
  }
}
