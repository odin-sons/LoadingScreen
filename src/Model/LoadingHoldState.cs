namespace LoadingScreen.Model;

public sealed class LoadingHoldState {
  bool _armed;
  bool _holding;
  float _startedAt;

  public bool Update(bool loadingVisible, bool ready, bool allowed, bool dismissed, float now, int maximumSeconds) {
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
      if (dismissed || now - _startedAt >= maximumSeconds) {
        Reset();
        return false;
      }

      return true;
    }

    if (_armed && loadingVisible) {
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
