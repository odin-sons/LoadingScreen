namespace LoadingScreen.Model;

public sealed class AudioHandover {
  object _voicedEntry;
  float _heldFade;
  bool _holding;
  float _inactiveSince = -1f;

  public static bool KeepMusic(string oldPath, string newPath, bool playingOrLoading) =>
      newPath != null && newPath == oldPath && playingOrLoading;

  public bool ShouldPlayVoice(object entry, bool continuation) {
    if (!continuation) _voicedEntry = null;
    if (entry == null || ReferenceEquals(entry, _voicedEntry)) return false;
    _voicedEntry = entry;
    return true;
  }

  public void Begin(float previousFade) {
    _heldFade = previousFade;
    _holding = true;
  }

  public float Opacity(float currentOpacity) {
    if (!_holding) return currentOpacity;
    if (currentOpacity >= _heldFade) {
      _holding = false;
      return currentOpacity;
    }
    return _heldFade;
  }

  public bool ShouldEnd(bool active, float now, float graceSeconds) {
    if (active) {
      _inactiveSince = -1f;
      return false;
    }
    if (_inactiveSince < 0f) _inactiveSince = now;
    return now - _inactiveSince >= graceSeconds;
  }

  public void Reset() {
    _voicedEntry = null;
    _holding = false;
    _heldFade = 0f;
    _inactiveSince = -1f;
  }
}
