namespace LoadingScreen;

using HarmonyLib;

using UnityEngine.UI;

using ValheimAudioSettings = Valheim.SettingsGui.AudioSettings;

[HarmonyPatch(typeof(ValheimAudioSettings))]
static class AudioSettingsPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(ValheimAudioSettings.Initialize))]
  static void InitializePostfix(ValheimAudioSettings __instance) => AudioSettingsRows.Attach(__instance);

  [HarmonyPostfix]
  [HarmonyPatch(nameof(ValheimAudioSettings.OnTabOpen))]
  static void OnTabOpenPostfix(ValheimAudioSettings __instance, Button backButton, Button okButton) {
    if (__instance.TryGetComponent(out AudioSettingsRows rows)) {
      rows.LinkNavigation(__instance.m_continousMusic, backButton, okButton);
    }
  }

  [HarmonyPrefix]
  [HarmonyPatch(nameof(ValheimAudioSettings.OnOkAsync))]
  static void OnOkAsyncPrefix(ValheimAudioSettings __instance) {
    if (__instance.TryGetComponent(out AudioSettingsRows rows)) rows.Save();
  }
}
