namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class ZoomTimingTests {
  [Fact]
  public void HandoverResumesAtElapsedProgress() {
    Assert.Equal(0f, ZoomTiming.Progress(0f, 10f));
    Assert.Equal(0.5f, ZoomTiming.Progress(5f, 10f));
    Assert.Equal(1f, ZoomTiming.Progress(20f, 10f));
  }
}
