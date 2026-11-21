namespace LoadingScreen;

using System;
using System.Collections;
using System.IO;

using global::LoadingScreen.Model;

using UnityEngine;
using UnityEngine.Networking;

using static PluginConfig;

public sealed class LoadingAudio : MonoBehaviour {
  static LoadingAudio _instance;

  AudioSource _music;
  AudioSource _voice;
  AudioClip _musicClip;
  AudioClip _voiceClip;
  Coroutine _musicLoad;
  Coroutine _voiceLoad;
  LayerHost _host;
  string _musicPath;
  string _selectedMusic;
  string _selectedVoice;
  CanvasGroup[] _screenGroups = [];
  bool _visible;
  float _fade;
  float _appliedMute;
  float _mutedMusicVolume = -1f;

  public static void Initialize(GameObject owner) {
    _instance = owner.AddComponent<LoadingAudio>();
  }

  public static void Play(LayerHost host, Selection selection, bool showsEntryText) {
    if (!_instance) return;
    _instance.Select(host, SoundSelection.MusicFor(selection, ScreenConfigLoader.Current),
        SoundSelection.VoiceFor(selection, showsEntryText));
  }

  public static void Stop(LayerHost host) {
    if (_instance && _instance._host == host) _instance.EndScreen();
  }

  public static void StopAllSounds() {
    if (!_instance) return;
    _instance._host = null;
    _instance._visible = false;
    _instance._selectedMusic = null;
    _instance._selectedVoice = null;
    _instance.StopVoice();
    _instance.StopMusic();
  }

  void Awake() {
    _music = gameObject.AddComponent<AudioSource>();
    _music.playOnAwake = false;
    _music.loop = true;
    _music.spatialBlend = 0f;
    _music.priority = 0;
    _music.bypassReverbZones = true;
    _voice = gameObject.AddComponent<AudioSource>();
    _voice.playOnAwake = false;
    _voice.spatialBlend = 0f;
    _voice.priority = 0;
    _voice.bypassReverbZones = true;
  }

  void Select(LayerHost host, string music, string voice) {
    if (_host != host) {
      StopVoice();
      StopMusic();
      _visible = false;
    }

    _host = host;
    _selectedMusic = music;
    _selectedVoice = voice;
    _screenGroups = host ? host.GetComponentsInParent<CanvasGroup>(true) : [];
    if (!PlayLoadingSounds.Value || !GameVolumeReady) {
      StopVoice();
      StopMusic();
      _visible = false;
      return;
    }

    if (host && host.gameObject.activeInHierarchy) {
      _visible = true;
      StartMusic(music);
      StartVoice(voice);
    } else {
      StopVoice();
      StopMusic();
      _visible = false;
    }
  }

  static bool GameVolumeReady => MusicMan.instance && AudioMan.instance;


  void Update() {
    if (!PlayLoadingSounds.Value || !GameVolumeReady) {
      if (_visible) {
        StopVoice();
        StopMusic();
        _visible = false;
      }
      bool showing = _host && _host.IsShowing && _host.gameObject.activeInHierarchy;
      ApplyGameMute(showing ? ScreenOpacity() : 0f);
      return;
    }

    if (_host && _host.IsShowing && _host.gameObject.activeInHierarchy) {
      if (!_visible) {
        _visible = true;
        StartMusic(_selectedMusic);
        StartVoice(_selectedVoice);
      }
    } else if (_visible) {
      EndScreen();
    }

    if (_visible) {
      _fade = ScreenOpacity();
    }

    if (_music) _music.volume = MusicVolume();
    if (_voice) _voice.volume = VoiceVolume();
    ApplyGameMute(_visible ? _fade : 0f);
  }

  void LateUpdate() {
    if (_appliedMute <= 0f || !MusicMan.instance || !Mutes(GameSounds.Music)) return;
    AudioSource music = MusicMan.instance.m_musicSource;
    if (!music || Mathf.Approximately(music.volume, _mutedMusicVolume)) return;
    _mutedMusicVolume = music.volume * (1f - _appliedMute);
    music.volume = _mutedMusicVolume;
  }

  static bool Mutes(GameSounds sounds) => (MuteGameSounds.Value & sounds) != 0;

  void ApplyGameMute(float mute) {
    if (MuteGameSounds.Value == GameSounds.None) mute = 0f;
    if (Mathf.Approximately(mute, _appliedMute) || !AudioMan.instance) return;
    _appliedMute = mute;
    if (!Mutes(GameSounds.Effects)) mute = 0f;
    float effects = PlatformPrefs.GetFloat("MasterVolume", 1f) * PlatformPrefs.GetFloat("SfxVolume", 1f);
    AudioMan.SetSFXVolume(mute >= 0.999f ? 0f : effects * (1f - mute));
  }

  float ScreenOpacity() {
    float opacity = 1f;
    foreach (CanvasGroup group in _screenGroups) {
      if (!group) continue;
      opacity *= group.alpha;
      if (group.ignoreParentGroups) break;
    }
    return opacity;
  }

  float MusicVolume() => _fade * MusicVolumePercent.Value / 100f * MusicMan.m_masterMusicVolume;

  // The game's effects slider is applied as power, not amplitude.
  float VoiceVolume() => _fade * VoiceVolumePercent.Value / 100f * Mathf.Sqrt(AudioMan.GetSFXVolume());

  void EndScreen() {
    ApplyGameMute(0f);
    _host = null;
    _visible = false;
    _selectedMusic = null;
    _selectedVoice = null;
    StopVoice();
    StopMusic();
  }

  void StartMusic(string relativePath) {
    string path = ScreenConfigLoader.ResolveImagePath(relativePath);
    if (path == _musicPath && (_music.isPlaying || _musicLoad != null)) {
      _fade = 1f;
      return;
    }

    StopMusic();
    _musicPath = path;
    if (path != null) _musicLoad = StartCoroutine(LoadClip(path, _music, true));
  }

  void StartVoice(string relativePath) {
    StopVoice();
    string path = ScreenConfigLoader.ResolveImagePath(relativePath);
    if (path != null) _voiceLoad = StartCoroutine(LoadClip(path, _voice, false));
  }

  IEnumerator LoadClip(string path, AudioSource source, bool music) {
    if (!File.Exists(path) || new FileInfo(path).Length > AudioFile.MaxBytes) {
      LoadingScreen.LogWarning($"Loading screen audio unavailable or over {AudioFile.MaxBytes / 1024 / 1024} MiB: {path}");
      yield break;
    }

    AudioType type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : AudioType.WAV;
    using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
    yield return request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success) {
      LoadingScreen.LogWarning($"Could not load audio {path}: {request.error}");
      if (music) _musicLoad = null;
      else _voiceLoad = null;
      yield break;
    }

    if (!_host || !_host.gameObject.activeInHierarchy || !PlayLoadingSounds.Value) {
      if (music) _musicLoad = null;
      else _voiceLoad = null;
      yield break;
    }

    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
    if (!clip) {
      LoadingScreen.LogWarning($"Could not decode audio {path}.");
      if (music) _musicLoad = null;
      else _voiceLoad = null;
      yield break;
    }
    _fade = ScreenOpacity();
    source.volume = music ? MusicVolume() : VoiceVolume();
    source.clip = clip;
    source.Play();
    if (music) {
      _musicClip = clip;
      _musicLoad = null;
      _fade = 1f;
    } else {
      _voiceClip = clip;
      _voiceLoad = null;
    }
  }

  void StopMusic() {
    if (_musicLoad != null) StopCoroutine(_musicLoad);
    _musicLoad = null;
    if (_music) {
      _music.Stop();
      _music.clip = null;
    }
    if (_musicClip) Destroy(_musicClip);
    _musicClip = null;
    _musicPath = null;
    _fade = 0f;
  }

  void StopVoice() {
    if (_voiceLoad != null) StopCoroutine(_voiceLoad);
    _voiceLoad = null;
    if (_voice) {
      _voice.Stop();
      _voice.clip = null;
    }
    if (_voiceClip) Destroy(_voiceClip);
    _voiceClip = null;
  }

  void OnDestroy() {
    ApplyGameMute(0f);
    StopAllSounds();
    if (_instance == this) _instance = null;
  }
}
