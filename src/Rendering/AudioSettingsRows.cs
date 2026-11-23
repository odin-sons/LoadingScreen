namespace LoadingScreen;

using System;

using BepInEx.Bootstrap;
using BepInEx.Configuration;

using global::LoadingScreen.Model;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using static PluginConfig;

using ValheimAudioSettings = Valheim.SettingsGui.AudioSettings;

public sealed class AudioSettingsRows : MonoBehaviour {
  public const string ValheimAudioControlGuid = "com.kimbauer.valheim.audiolodcontrol";

  Row _music;
  Row _voice;

  sealed class Row {
    public ConfigEntry<int> Setting;
    public Slider Slider;
    public TMP_Text Value;

    public void Show(float value) {
      if (Value) Value.text = VolumePercent.Format(value);
    }

    public void Load() {
      if (!Slider) return;
      Slider.SetValueWithoutNotify(VolumePercent.ToSlider(Setting.Value));
      Show(Slider.value);
    }

    public void Save() {
      if (Slider) Setting.Value = VolumePercent.FromSlider(Slider.value);
    }
  }

  public static void Attach(ValheimAudioSettings settings) {
    if (!settings || !IsModEnabled.Value || settings.GetComponent<AudioSettingsRows>()) return;
    if (Chainloader.PluginInfos.ContainsKey(ValheimAudioControlGuid)) {
      Log.Audio.Debug(LogContext.Message("Loading screen volume sliders skipped in the Audio settings.",
          ("reason", "ValheimAudioControl manages the Audio tab layout"), ("plugin", ValheimAudioControlGuid)));
      return;
    }

    Slider template = settings.m_musicVolumeSlider;
    Transform list = template ? template.transform.parent : null;
    if (!list || !list.GetComponent<VerticalLayoutGroup>()) {
      Log.Audio.WarningOnce("audio-settings-layout", LogContext.Message(
          "Loading screen volume sliders skipped in the Audio settings.", ("reason", "unexpected Audio tab layout"),
          ("settings", "[5 - Sound] Music volume percent, Voice volume percent")));
      return;
    }

    AudioSettingsRows rows = settings.gameObject.AddComponent<AudioSettingsRows>();
    try {
      rows._music = CreateRow(template, list, "LoadingScreen_MusicVolume", "musicVolumeSlider", MusicVolumePercent);
      rows._voice = CreateRow(template, list, "LoadingScreen_VoiceVolume", "voiceVolumeSlider", VoiceVolumePercent);
      rows.Subscribe();
    } catch (Exception exception) {
      Log.Audio.WarningOnce("audio-settings-rows", LogContext.Message(
          "Loading screen volume sliders skipped in the Audio settings.", ("error", exception.Message)));
      Log.Audio.Debug(exception);
      rows.Remove();
    }
  }

  public void LinkNavigation(Selectable above, Selectable back, Selectable ok) {
    if (!_music?.Slider || !_voice?.Slider) return;
    if (above) {
      GuiUtils.SetNavigationDown(above, _music.Slider);
      GuiUtils.SetNavigationUp(_music.Slider, above);
    }
    GuiUtils.SetNavigationVertical(_music.Slider, _voice.Slider);
    if (back) {
      GuiUtils.SetNavigationDown(_voice.Slider, back);
      GuiUtils.SetNavigationUp(back, _voice.Slider);
    }
    if (ok) GuiUtils.SetNavigationUp(ok, _voice.Slider);
  }

  public void Save() {
    _music?.Save();
    _voice?.Save();
  }

  static Row CreateRow(Slider template, Transform list, string name, string labelKey, ConfigEntry<int> setting) {
    GameObject clone = Instantiate(template.gameObject, list, false);
    clone.name = name;
    clone.transform.SetAsLastSibling();
    foreach (Localize localize in clone.GetComponentsInChildren<Localize>(true)) Destroy(localize);

    Row row = new() { Setting = setting, Slider = clone.GetComponent<Slider>(), Value = FindText(clone.transform, "Value") };
    if (!row.Slider) throw new InvalidOperationException("The music volume row has no slider.");
    row.Slider.onValueChanged = new Slider.SliderEvent();
    row.Slider.onValueChanged.AddListener(row.Show);
    TMP_Text label = FindText(clone.transform, "Label");
    if (label) label.text = UiText.Get(labelKey);
    row.Load();
    return row;
  }

  static TMP_Text FindText(Transform root, string name) {
    Transform child = root.Find(name);
    return child ? child.GetComponent<TMP_Text>() : null;
  }

  void Subscribe() {
    MusicVolumePercent.SettingChanged += OnSettingChanged;
    VoiceVolumePercent.SettingChanged += OnSettingChanged;
  }

  void OnSettingChanged(object sender, EventArgs args) {
    _music?.Load();
    _voice?.Load();
  }

  void Remove() {
    if (_music?.Slider) Destroy(_music.Slider.gameObject);
    if (_voice?.Slider) Destroy(_voice.Slider.gameObject);
    _music = null;
    _voice = null;
    Destroy(this);
  }

  void OnDestroy() {
    MusicVolumePercent.SettingChanged -= OnSettingChanged;
    VoiceVolumePercent.SettingChanged -= OnSettingChanged;
  }
}
