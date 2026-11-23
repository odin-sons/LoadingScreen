namespace LoadingScreen.Model;

using System;

public enum ProgressStage { Connecting, Password, ReceivingWorld }

public readonly struct ProgressValue {
  public float? Fraction { get; }
  public string StageKey { get; }
  public bool Pulse { get; }

  public ProgressValue(float? fraction, string stageKey = null, bool pulse = false) {
    Fraction = fraction;
    StageKey = stageKey;
    Pulse = pulse;
  }
}

public static class ProgressMath {
  public static ProgressValue Scene(float fraction) => new(Clamp(fraction));

  public static ProgressValue Area(bool zoneReady, int instantiated, int required) {
    if (!zoneReady) return new(0f);
    float fraction = required <= 0 ? 1f : 0.5f + 0.5f * Clamp((float)instantiated / required);
    return new(fraction, pulse: fraction >= 1f);
  }

  public static ProgressValue Sleep(double start, double current, double morning) =>
      new(morning <= start ? 0f : Clamp((float)((current - start) / (morning - start))));

  public static ProgressValue Connection(ProgressStage stage) => new(null, stage switch {
    ProgressStage.Password => "passwordStage",
    ProgressStage.ReceivingWorld => "receivingWorldStage",
    _ => "connectingStage",
  });

  static float Clamp(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
}
