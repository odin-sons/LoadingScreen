namespace LoadingScreen;

using System;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using static PluginConfig;

internal sealed class SlideshowController {
  float _lastImageTime;
  float _lastTipTime;

  internal void Reset(float imageTime, float tipTime) {
    _lastImageTime = imageTime;
    _lastTipTime = tipTime;
  }

  internal void TipChanged(float now) => _lastTipTime = now;

  internal void Tick(
      float now, LoadingStage stage, Situation situation, LayerHost host, TMP_Text tipText,
      Image fallbackImage, bool rotateImages, bool rotateTips, Action onFallback = null,
      Action onImageRotation = null) {
    if (rotateImages && SlideshowTiming.IsDue(now, _lastImageTime, ChangeImageEverySeconds.Value)) {
      _lastImageTime = now;
      _lastTipTime = now;
      if (onImageRotation != null) {
        onImageRotation();
      } else {
        Selection current = host ? host.CurrentSelection : null;
        Selection next = ScreenConfigLoader.Selector.SelectNext(current, situation);
        if (next == null || !LoadingManager.ShowSituation(host, stage, situation, tipText, next)) {
          LoadingManager.ShowFallback(host, fallbackImage, tipText);
          onFallback?.Invoke();
        }
      }
      return;
    }

    if (rotateTips && TipRotation.IsDue(
            now, _lastTipTime, ChangeTipEverySeconds.Value, LoadingManager.CurrentCaption,
            CustomAssets.LoadingTips.Count)) {
      _lastTipTime = now;
      LoadingManager.RotateTip(host, tipText);
    }
  }
}
