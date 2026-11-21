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

  public static IEnumerator ScaleLerp(
      Transform transform, Vector3 startScale, Vector3 endScale, float lerpDuration,
      float elapsed = 0f) {
    transform.localScale = startScale;
    float timeElapsed = Mathf.Max(0f, elapsed);

    while (timeElapsed < lerpDuration) {
      float t = ZoomTiming.Progress(timeElapsed, lerpDuration);

      transform.localScale = Vector3.Lerp(startScale, endScale, t);
      timeElapsed += Time.deltaTime;

      yield return null;
    }

    transform.localScale = endScale;
  }

  public static void ScaleLerpLoadingImage(this MonoBehaviour component, Image loadingImage) {
    if (_scaleLerpCoroutine != null) {
      if (_component) _component.StopCoroutine(_scaleLerpCoroutine);
      _scaleLerpCoroutine = null;
    }

    _component = component;
    _image = loadingImage;
    ZoomDefaults zoom = ScreenConfigLoader.Current.Defaults.Zoom;
    if (zoom.Enabled && loadingImage) {
      _scaleLerpCoroutine =
          component.StartCoroutine(
              ScaleLerp(
                  loadingImage.transform,
                  Vector3.one,
                  Vector3.one * zoom.Amount,
                  zoom.Seconds));
    } else if (loadingImage) {
      loadingImage.transform.localScale = Vector3.one;
    }
  }
}
