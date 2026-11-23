namespace LoadingScreen.Model;

public readonly record struct LoadingHoldDecision(bool Holding, bool ShowContinuePrompt);

public sealed class LoadingHoldState {
  bool _armed;
  bool _holding;
  float _startedAt;

  public bool Holding => _holding;

  public LoadingHoldDecision Update(
      bool loadingVisible, bool ready, bool allowed, bool holdUntilKey, bool voiceActive,
      bool dismissed, float now, int maximumSeconds, bool exitActive = false) {
    if (!allowed) {
      Reset();
      return default;
    }

    if (!ready) {
      _holding = false;
      _armed = loadingVisible;
      return default;
    }

    if (_holding) {
      if (dismissed || now - _startedAt >= maximumSeconds) {
        Reset();
        return default;
      }

      if (holdUntilKey || voiceActive || exitActive) return new(true, holdUntilKey);

      Reset();
      return default;
    }

    if (_armed && loadingVisible && (holdUntilKey || voiceActive || exitActive)) {
      _armed = false;
      _holding = true;
      _startedAt = now;
      return new(true, holdUntilKey);
    }

    _armed = false;
    return default;
  }

  public void Reset() {
    _armed = false;
    _holding = false;
  }
}
