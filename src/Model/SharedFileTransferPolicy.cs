namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;

public enum SharedFileStatus {
  Busy = 1,
  NotFound = 2,
  Denied = 3,
  Invalid = 4,
}

public static class SharedFileTransferPolicy {
  public const int StatusProtocolVersion = 1;
  public const int MaxRetryAfterMilliseconds = 30000;
  public const int MaxTimeoutRetries = 3;
  public const double MaxFileSeconds = 600;

  public static bool IsHash(string hash) {
    if (hash == null || hash.Length != 64) return false;

    foreach (char value in hash) {
      if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))) return false;
    }

    return true;
  }

  public static bool TryParseStatus(
      int version, string hash, int index, int rawStatus, int retryAfterMilliseconds,
      out SharedFileStatus status) {
    status = default;
    if (version != StatusProtocolVersion || !IsHash(hash) || index < 0
        || rawStatus < (int) SharedFileStatus.Busy || rawStatus > (int) SharedFileStatus.Invalid
        || retryAfterMilliseconds < 0 || retryAfterMilliseconds > MaxRetryAfterMilliseconds
        || (rawStatus != (int) SharedFileStatus.Busy && retryAfterMilliseconds != 0)) {
      return false;
    }

    status = (SharedFileStatus) rawStatus;
    return true;
  }

  public static bool TryParseManifestBusy(int version, int rawStatus, int retryAfterMilliseconds) {
    return version == StatusProtocolVersion && rawStatus == (int) SharedFileStatus.Busy
        && retryAfterMilliseconds >= 0 && retryAfterMilliseconds <= MaxRetryAfterMilliseconds;
  }

  public static bool ShouldRetryStatus(SharedFileStatus status, double elapsedSeconds) {
    return status == SharedFileStatus.Busy && elapsedSeconds < MaxFileSeconds;
  }

  public static bool ShouldRetryTimeout(int timeoutRetries, double elapsedSeconds) {
    return timeoutRetries < MaxTimeoutRetries && elapsedSeconds < MaxFileSeconds;
  }

  public static double BackoffSeconds(int attempt, int retryAfterMilliseconds, Func<double> random) {
    if (attempt < 0 || retryAfterMilliseconds < 0 || retryAfterMilliseconds > MaxRetryAfterMilliseconds
        || random == null) throw new ArgumentOutOfRangeException();

    double sample = random();
    if (double.IsNaN(sample) || sample < 0 || sample > 1) throw new ArgumentOutOfRangeException(nameof(random));

    double baseSeconds = 1 << Math.Min(attempt, 3);
    return Math.Max(Math.Min(8, baseSeconds * (0.75 + 0.5 * sample)), retryAfterMilliseconds / 1000.0);
  }
}

public sealed class SharedFileBandwidthBudget {
  readonly object _sync = new();
  readonly int _bytesPerSecond;
  double _availableBytes;
  double _lastSeconds;

  public int BytesPerSecond => _bytesPerSecond;

  public SharedFileBandwidthBudget(int bytesPerSecond, double nowSeconds) {
    if (bytesPerSecond <= 0 || double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) {
      throw new ArgumentOutOfRangeException();
    }

    _bytesPerSecond = bytesPerSecond;
    _availableBytes = bytesPerSecond;
    _lastSeconds = nowSeconds;
  }

  void Refill(double nowSeconds) {
    if (nowSeconds > _lastSeconds) {
      _availableBytes = Math.Min(_bytesPerSecond, _availableBytes + (nowSeconds - _lastSeconds) * _bytesPerSecond);
      _lastSeconds = nowSeconds;
    }
  }

  public double AvailableBytes(double nowSeconds) {
    if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) throw new ArgumentOutOfRangeException();

    lock (_sync) {
      Refill(nowSeconds);
      return _availableBytes;
    }
  }

  public bool TrySpend(int bytes, double nowSeconds, out int retryAfterMilliseconds) {
    if (bytes <= 0 || bytes > _bytesPerSecond || double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) {
      throw new ArgumentOutOfRangeException();
    }

    lock (_sync) {
      Refill(nowSeconds);

      if (_availableBytes < bytes) {
        retryAfterMilliseconds = Math.Max(1,
            (int) Math.Ceiling((bytes - _availableBytes) * 1000 / _bytesPerSecond));
        return false;
      }

      _availableBytes -= bytes;
      retryAfterMilliseconds = 0;
      return true;
    }
  }
}

public sealed class SharedFileFairBudget {
  const int MaxWaiters = 256;
  const double WaiterSeconds = 35;

  sealed class Waiter {
    public object Peer;
    public int Bytes;
    public double LastRequestSeconds;
  }

  readonly object _sync = new();
  readonly int _bytesPerSecond;
  readonly int _maxChunkBytes;
  readonly SharedFileBandwidthBudget _global;
  readonly List<Waiter> _waiting = new();

  public int BytesPerSecond => _bytesPerSecond;

  public SharedFileFairBudget(int bytesPerSecond, int maxChunkBytes, double nowSeconds) {
    if (maxChunkBytes <= 0 || maxChunkBytes > bytesPerSecond) throw new ArgumentOutOfRangeException();

    _bytesPerSecond = bytesPerSecond;
    _maxChunkBytes = maxChunkBytes;
    _global = new SharedFileBandwidthBudget(bytesPerSecond, nowSeconds);
  }

  public bool TrySpend(object peer, int bytes, double nowSeconds, out int retryAfterMilliseconds) {
    if (peer == null || bytes <= 0 || bytes > _maxChunkBytes
        || double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) throw new ArgumentOutOfRangeException();

    lock (_sync) {
      for (int i = _waiting.Count - 1; i >= 0; i--) {
        if (nowSeconds - _waiting[i].LastRequestSeconds > WaiterSeconds) _waiting.RemoveAt(i);
      }

      Waiter own = null;
      foreach (Waiter candidate in _waiting) {
        if (ReferenceEquals(candidate.Peer, peer)) {
          own = candidate;
          own.Bytes = bytes;
          own.LastRequestSeconds = nowSeconds;
          break;
        }
      }

      int reserved = _waiting.Count > 0 && !ReferenceEquals(_waiting[0].Peer, peer)
          ? _waiting[0].Bytes : 0;
      double available = _global.AvailableBytes(nowSeconds);
      if (available >= bytes + reserved && _global.TrySpend(bytes, nowSeconds, out retryAfterMilliseconds)) {
        if (own != null) _waiting.Remove(own);
        return true;
      }

      if (own == null) {
        if (_waiting.Count >= MaxWaiters) {
          retryAfterMilliseconds = SharedFileTransferPolicy.MaxRetryAfterMilliseconds;
          return false;
        }

        _waiting.Add(new Waiter { Peer = peer, Bytes = bytes, LastRequestSeconds = nowSeconds });
      }

      retryAfterMilliseconds = Math.Min(SharedFileTransferPolicy.MaxRetryAfterMilliseconds,
          Math.Max(1, (int) Math.Ceiling((bytes + reserved - available) * 1000 / _bytesPerSecond)));
      return false;
    }
  }
}
