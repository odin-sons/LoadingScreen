namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;

public enum LoadingStage { Menu, Scene, Hud }

public sealed class LoadingSession {
  readonly Dictionary<Layer, string> _images = [];
  public Selection Selection { get; private set; }
  public string Caption { get; private set; }
  public string CurrentCaption { get; private set; }
  public float StartedAt { get; private set; }
  public float AnimationElapsed { get; private set; }
  public LoadingStage Stage { get; private set; }
  public bool WasVisible { get; private set; }

  public bool CanContinue(LoadingStage next, Situation situation) =>
      Selection != null && WasVisible && situation.Kind == SituationKind.General
          && ((Stage == LoadingStage.Menu && next == LoadingStage.Scene)
              || (Stage == LoadingStage.Menu && next == LoadingStage.Hud)
              || (Stage == LoadingStage.Scene && next == LoadingStage.Hud));

  public void Record(LoadingStage stage, Selection selection, string caption, string currentCaption, float now) {
    Stage = stage;
    Selection = selection;
    Caption = caption;
    CurrentCaption = currentCaption;
    StartedAt = now;
    AnimationElapsed = 0f;
    WasVisible = stage != LoadingStage.Menu;
  }

  public void Tick(float animationElapsed) => AnimationElapsed = animationElapsed;

  public void Continue(LoadingStage stage) => Stage = stage;

  public void MarkVisible(float now) {
    if (Stage != LoadingStage.Menu || WasVisible) return;
    WasVisible = true;
    StartedAt = now;
    AnimationElapsed = 0f;
  }

  public void SetCaption(string caption) => Caption = caption;

  public void ResetImages() => _images.Clear();

  public string ImageFor(Layer layer, Func<string> choose) {
    if (_images.TryGetValue(layer, out string path)) return path;
    path = choose();
    _images[layer] = path;
    return path;
  }

  public void Reset() {
    Selection = null;
    Caption = null;
    CurrentCaption = null;
    StartedAt = 0f;
    AnimationElapsed = 0f;
    WasVisible = false;
    _images.Clear();
  }
}
