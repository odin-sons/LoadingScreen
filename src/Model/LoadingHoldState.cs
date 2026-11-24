namespace LoadingScreen.Model;

public sealed class LoadingHoldState {
  bool _armed;
  bool _holding;
  float _startedAt;

  public bool Holding => _holding;

  public bool Update(
      bool loadingVisible, bool ready, bool allowed, bool voiceActive,
      bool dismissed, float now, int maximumSeconds, bool exitActive = false) {
    if (!allowed) {
      Reset();
      return false;
    }

    if (!ready) {
      _holding = false;
      _armed = loadingVisible;
      return false;
    }

    if (_holding) {
      if (dismissed || now - _startedAt >= maximumSeconds || !(voiceActive || exitActive)) {
        Reset();
        return false;
      }

      return true;
    }

    if (_armed && loadingVisible && (voiceActive || exitActive)) {
      _armed = false;
      _holding = true;
      _startedAt = now;
      return true;
    }

    _armed = false;
    return false;
  }

  public void Reset() {
    _armed = false;
    _holding = false;
  }
}
