namespace LoadingScreen.Model;

using System.Globalization;

public sealed class SceneLoadTiming {
  public const float ReadCompleteProgress = 0.9f;

  public SceneLoadTiming(string mode, double startedAt) {
    Mode = mode;
    StartedAt = startedAt;
  }

  public string Mode { get; }
  public double StartedAt { get; }
  public double? ReadAt { get; private set; }
  public double? DoneAt { get; private set; }
  public bool IsDone => DoneAt.HasValue;

  public double? ReadSeconds => ReadAt - StartedAt;
  public double? ActivationSeconds => DoneAt - ReadAt;
  public double? TotalSeconds => DoneAt - StartedAt;

  public void ObserveProgress(double now, float progress) {
    if (!ReadAt.HasValue && !IsDone && progress >= ReadCompleteProgress) ReadAt = now;
  }

  public void MarkDone(double now) {
    if (!IsDone) DoneAt = now;
  }

  public (string Key, object Value)[] Details() => [
      ("mode", Mode),
      ("totalSeconds", Seconds(TotalSeconds)),
      ("readSeconds", Seconds(ReadSeconds)),
      ("activationSeconds", Seconds(ActivationSeconds))];

  static string Seconds(double? value) =>
      value.HasValue ? value.Value.ToString("0.000", CultureInfo.InvariantCulture) : "unknown";
}
