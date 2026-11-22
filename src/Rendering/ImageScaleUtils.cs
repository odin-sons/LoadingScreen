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
    AnimationPreset preset = ScreenConfigLoader.Current.Defaults.Animations.Resolve("zoom-in");
    float startedAt = Time.realtimeSinceStartup;
    float elapsed;
    while ((elapsed = Time.realtimeSinceStartup - startedAt) < preset.EffectiveDuration()) {
      transform.localScale = Vector3.one * preset.Sample(elapsed, 0f, 0f).Scale;
      yield return null;
    }
    transform.localScale = Vector3.one * preset.Sample(preset.EffectiveDuration(), 0f, 0f).Scale;
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
