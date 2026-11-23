namespace LoadingScreen;

using System.Collections;

using UnityEngine;
using UnityEngine.UI;

using global::LoadingScreen.Model;

public static class ImageScaleUtils {
  static Coroutine _scaleLerpCoroutine;
  static MonoBehaviour _component;
  static Image _image;

  public static void RefreshAppearance() {
    if (_component && _image && _image.gameObject.activeInHierarchy) {
      _component.ScaleLerpLoadingImage(_image);
    }
  }

  static IEnumerator AnimateDefaultZoom(Transform transform) {
    ScreenDefaults defaults = ScreenConfigLoader.Current.Defaults;
    AnimationSpec spec = AnimationSpec.Merge(defaults.Animation, new() { Idle = "zoom-in" });
    AnimationPreset preset = defaults.Animations.Resolve(spec.Idle);
    float speed = spec.Speed ?? 1f;
    float duration = preset.EffectiveDuration(speed);
    bool repeats = preset.Repeat != AnimationRepeat.Once;
    float startedAt = Time.realtimeSinceStartup;
    float elapsed;
    while (repeats || (elapsed = Time.realtimeSinceStartup - startedAt) < duration) {
      elapsed = Time.realtimeSinceStartup - startedAt;
      transform.localScale = Vector3.one * preset.Sample(elapsed, 0f, 0f, layerSpeed: speed).Scale;
      yield return null;
    }
    transform.localScale = Vector3.one * preset.Sample(duration, 0f, 0f, layerSpeed: speed).Scale;
  }

  public static void ScaleLerpLoadingImage(this MonoBehaviour component, Image loadingImage) {
    if (_scaleLerpCoroutine != null) {
      if (_component) _component.StopCoroutine(_scaleLerpCoroutine);
      _scaleLerpCoroutine = null;
    }

    _component = component;
    _image = loadingImage;
    if (loadingImage) _scaleLerpCoroutine = component.StartCoroutine(AnimateDefaultZoom(loadingImage.transform));
  }
}
