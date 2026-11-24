namespace LoadingScreen.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using global::LoadingScreen.Model;

using Xunit;

public class SharedFileTransferTests {
  const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  [Theory]
  [InlineData(0, 1)]
  [InlineData(1, 2)]
  [InlineData(2, 4)]
  [InlineData(3, 8)]
  [InlineData(8, 8)]
  public void BackoffDoublesThenCaps(int attempt, double expectedSeconds) {
    Assert.Equal(expectedSeconds, SharedFileTransferPolicy.BackoffSeconds(attempt, 0, () => 0.5));
  }

  [Fact]
  public void BackoffUsesInjectedJitterAndHonorsRetryAfter() {
    Assert.Equal(0.75, SharedFileTransferPolicy.BackoffSeconds(0, 0, () => 0));
    Assert.Equal(1.25, SharedFileTransferPolicy.BackoffSeconds(0, 0, () => 1));
    Assert.Equal(5, SharedFileTransferPolicy.BackoffSeconds(1, 5000, () => 0));
  }

  [Theory]
  [InlineData(SharedFileStatus.Busy, true)]
  [InlineData(SharedFileStatus.NotFound, false)]
  [InlineData(SharedFileStatus.Denied, false)]
  [InlineData(SharedFileStatus.Invalid, false)]
  public void OnlyBusyStatusRetries(SharedFileStatus status, bool expected) {
    Assert.Equal(expected, SharedFileTransferPolicy.ShouldRetryStatus(status, 20));
  }

  [Fact]
  public void BusyDoesNotConsumeTimeoutRetriesButFileDeadlineStillStopsIt() {
    Assert.True(SharedFileTransferPolicy.ShouldRetryStatus(SharedFileStatus.Busy, 599));
    Assert.False(SharedFileTransferPolicy.ShouldRetryStatus(SharedFileStatus.Busy, 600));
    Assert.True(SharedFileTransferPolicy.ShouldRetryTimeout(2, 20));
    Assert.False(SharedFileTransferPolicy.ShouldRetryTimeout(3, 20));
    Assert.False(SharedFileTransferPolicy.ShouldRetryTimeout(0, 600));
  }

  [Fact]
  public void StatusParserRejectsUnknownAndMalformedFields() {
    Assert.True(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, (int) SharedFileStatus.Busy, 1000,
        out SharedFileStatus status));
    Assert.Equal(SharedFileStatus.Busy, status);
    Assert.False(SharedFileTransferPolicy.TryParseStatus(2, Hash, 0, 1, 1000, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, "bad", 0, 1, 1000, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, -1, 1, 1000, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, 0, 1000, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, 5, 1000, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, 1, -1, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, 1, 30001, out _));
    Assert.False(SharedFileTransferPolicy.TryParseStatus(1, Hash, 0, 2, 1, out _));
  }

  [Fact]
  public void ManifestBusyParserRejectsInvalidStatusAndRetryBounds() {
    Assert.True(SharedFileTransferPolicy.TryParseManifestBusy(1, (int) SharedFileStatus.Busy, 1000));
    Assert.False(SharedFileTransferPolicy.TryParseManifestBusy(2, 1, 1000));
    Assert.False(SharedFileTransferPolicy.TryParseManifestBusy(1, 2, 1000));
    Assert.False(SharedFileTransferPolicy.TryParseManifestBusy(1, 1, -1));
    Assert.False(SharedFileTransferPolicy.TryParseManifestBusy(1, 1, 30001));
  }

  [Fact]
  public void OneBudgetIsSharedAcrossCallersAndRefillsAtTheConfiguredRate() {
    SharedFileBandwidthBudget budget = new(1024, 0);
    Assert.True(budget.TrySpend(768, 0, out _));
    Assert.False(budget.TrySpend(512, 0, out int retryAfterMs));
    Assert.Equal(250, retryAfterMs);
    Assert.True(budget.TrySpend(512, 0.25, out _));
    Assert.False(budget.TrySpend(1, 0.25, out retryAfterMs));
    Assert.Equal(1, retryAfterMs);
  }

  [Fact]
  public void BudgetCannotAccumulateMoreThanOneSecondOfBurst() {
    SharedFileBandwidthBudget budget = new(1024, 0);
    Assert.True(budget.TrySpend(1024, 0, out _));
    Assert.True(budget.TrySpend(1024, 10, out _));
    Assert.False(budget.TrySpend(1, 10, out _));
  }

  [Fact]
  public void ManifestBudgetLimitsAggregateReplies() {
    SharedFileBandwidthBudget budget = new(4 * 1024 * 1024, 0);
    Assert.True(budget.TrySpend(3 * 1024 * 1024, 0, out _));
    Assert.False(budget.TrySpend(3 * 1024 * 1024, 0, out int retryAfterMs));
    Assert.Equal(500, retryAfterMs);
    Assert.True(budget.TrySpend(3 * 1024 * 1024, 0.5, out _));
  }

  [Fact]
  public void ConcurrentCallersCannotSpendMoreThanTheSharedBudget() {
    SharedFileBandwidthBudget budget = new(1024, 0);
    int accepted = 0;

    Parallel.For(0, 1000, iteration => {
      if (budget.TrySpend(16, 0, out _)) Interlocked.Increment(ref accepted);
    });

    Assert.Equal(64, accepted);
  }

  [Fact]
  public void BusyPeerGetsBudgetWhileAnotherPeerContinuesRequesting() {
    SharedFileFairBudget budget = new(256 * 1024, 32 * 1024, 0);
    object first = new();
    object second = new();

    for (int i = 0; i < 8; i++) Assert.True(budget.TrySpend(first, 32 * 1024, 0, out _));
    Assert.False(budget.TrySpend(second, 32 * 1024, 0, out _));
    Assert.False(budget.TrySpend(first, 32 * 1024, 0.125, out _));
    Assert.True(budget.TrySpend(second, 32 * 1024, 0.125, out _));
    Assert.True(budget.TrySpend(first, 32 * 1024, 0.25, out _));
    Assert.True(budget.TrySpend(second, 32 * 1024, 0.375, out _));
  }

  [Fact]
  public void IdlePeerShareBecomesAvailableToTheRemainingPeer() {
    SharedFileFairBudget budget = new(256 * 1024, 32 * 1024, 0);
    object first = new();
    object second = new();

    Assert.True(budget.TrySpend(first, 32 * 1024, 0, out _));
    Assert.True(budget.TrySpend(second, 32 * 1024, 0, out _));

    for (int i = 0; i < 8; i++) Assert.True(budget.TrySpend(first, 32 * 1024, 2, out _));
    Assert.False(budget.TrySpend(first, 32 * 1024, 2, out _));
  }

  [Fact]
  public void LowDemandWaitersDoNotWithholdTheirUnusedShare() {
    SharedFileFairBudget budget = new(256 * 1024, 32 * 1024, 0);
    object downloader = new();
    object[] waiters = new object[9];

    for (int i = 0; i < 8; i++) Assert.True(budget.TrySpend(downloader, 32 * 1024, 0, out _));
    for (int i = 0; i < waiters.Length; i++) {
      waiters[i] = new object();
      Assert.False(budget.TrySpend(waiters[i], 32 * 1024, 0, out _));
    }

    for (int i = 0; i < 7; i++) Assert.True(budget.TrySpend(downloader, 32 * 1024, 1, out _));
    Assert.False(budget.TrySpend(downloader, 32 * 1024, 1, out _));
    Assert.False(budget.TrySpend(waiters[1], 32 * 1024, 1, out _));
    Assert.True(budget.TrySpend(waiters[0], 32 * 1024, 1, out _));
    Assert.True(budget.TrySpend(waiters[1], 32 * 1024, 1.125, out _));
  }

  [Fact]
  public void ExpiredRetryReservationReturnsToTheDownloader() {
    SharedFileFairBudget budget = new(256 * 1024, 32 * 1024, 0);
    object downloader = new();
    object waiter = new();

    for (int i = 0; i < 8; i++) Assert.True(budget.TrySpend(downloader, 32 * 1024, 0, out _));
    Assert.False(budget.TrySpend(waiter, 32 * 1024, 0, out _));
    for (int i = 0; i < 8; i++) Assert.True(budget.TrySpend(downloader, 32 * 1024, 36, out _));
  }
}
