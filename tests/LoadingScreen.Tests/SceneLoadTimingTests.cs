namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class SceneLoadTimingTests {
  [Fact]
  public void SplitsReadingFromActivationAtNinetyPercent() {
    SceneLoadTiming timing = new("async", 10.0);
    timing.ObserveProgress(11.0, 0.5f);
    timing.ObserveProgress(13.0, 0.9f);
    timing.ObserveProgress(14.0, 0.9f);
    timing.MarkDone(14.5);

    Assert.Equal(3.0, timing.ReadSeconds);
    Assert.Equal(1.5, timing.ActivationSeconds);
    Assert.Equal(4.5, timing.TotalSeconds);
  }

  [Fact]
  public void SynchronousLoadReportsOnlyTotal() {
    SceneLoadTiming timing = new("sync", 2.0);
    timing.MarkDone(9.25);

    Assert.Equal(
        "Loaded. mode=sync totalSeconds=7.250 readSeconds=unknown activationSeconds=unknown",
        LogContext.Message("Loaded.", timing.Details()));
  }

  [Fact]
  public void IgnoresProgressAndSecondCompletionAfterDone() {
    SceneLoadTiming timing = new("async", 0.0);
    timing.MarkDone(5.0);
    timing.ObserveProgress(6.0, 1f);
    timing.MarkDone(7.0);

    Assert.True(timing.IsDone);
    Assert.Null(timing.ReadSeconds);
    Assert.Equal(5.0, timing.TotalSeconds);
  }
}
