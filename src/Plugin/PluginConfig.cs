namespace LoadingScreen;

using BepInEx.Configuration;

using global::LoadingScreen.Extensions;
using global::LoadingScreen.Model;

public static class PluginConfig {
  public static ConfigEntry<bool> IsModEnabled { get; private set; }
  public static ConfigEntry<string> ImageFolder { get; private set; }
  public static ConfigEntry<string> TextLanguage { get; private set; }
  public static ConfigEntry<bool> BuiltinTips { get; private set; }
  public static ConfigEntry<ModLogLevel> LogLevel { get; private set; }
  public static ConfigEntry<int> ChangeImageEverySeconds { get; private set; }
  public static ConfigEntry<int> ChangeTipEverySeconds { get; private set; }

  public static ConfigEntry<bool> SceneLoaderUseLoadingImages { get; private set; }
  public static ConfigEntry<bool> SceneLoaderShowProgressText { get; private set; }

  public static ConfigEntry<bool> ShowLoadingScreenImmediatelyOnDeath { get; private set; }
  public static ConfigEntry<bool> ShowLoadingScreenOnTeleport { get; private set; }
  public static ConfigEntry<bool> ShowLoadingScreenWhileSleeping { get; private set; }
  public static ConfigEntry<bool> InvulnerableWhileHeld { get; private set; }
  public static ConfigEntry<bool> WaitForVoice { get; private set; }
  public static ConfigEntry<bool> WaitForExitAnimation { get; private set; }
  public static ConfigEntry<int> MaximumHoldSeconds { get; private set; }
  public static ConfigEntry<bool> CaptionUseTipWhenNoText { get; private set; }

  public static ConfigEntry<bool> ShareScreensWithPlayers { get; private set; }
  public static ConfigEntry<int> SharedFileBandwidthKiBPerSecond { get; private set; }
  public static ConfigEntry<bool> LockSyncedSettings { get; private set; }
  public static ConfigEntry<bool> PlayLoadingSounds { get; private set; }
  public static ConfigEntry<int> MusicVolumePercent { get; private set; }
  public static ConfigEntry<int> VoiceVolumePercent { get; private set; }
  public static ConfigEntry<GameSounds> MuteGameSounds { get; private set; }
  public static ConfigEntry<bool> ReadableText { get; private set; }
  public static ConfigEntry<int> MinimumTextSize { get; private set; }

  const string General = "1 - General";
  const string Screens = "2 - When to show";
  const string Timing = "3 - Timing";
  const string StartScreen = "4 - Game start screen";
  const string Sound = "5 - Sound";
  const string Accessibility = "6 - Accessibility";

  internal static void BindConfig(ConfigFile config, ConfigSynchronization sync) {
    LockSyncedSettings =
        config.BindInOrder(
            General,
            "Lock Configuration",
            true,
            "Allow only server admins to change synchronized settings while connected. [Synced with Server]");

    IsModEnabled =
        config.BindInOrder(General, "Mod enabled", true, "Turn the whole mod on or off. [Synced with Server]");

    ImageFolder =
        config.BindInOrder(
            General,
            "Image folder",
            ImageFolderPath.DefaultFolder,
            "Folder with loadingscreen.yaml, tips.txt, and images. Relative paths start at the BepInEx folder. "
                + "Needs a game restart. [Not Synced with Server]");

    TextLanguage =
        config.BindInOrder(
            General,
            "Text language",
            "Game",
            "Language for tips and entry text. Game follows Valheim's language; enter an ISO language code "
                + "such as ru or pt-BR to override it. Unknown codes use the game language. [Not Synced with Server]");

    BuiltinTips =
        config.BindInOrder(
            General,
            "Built-in tips",
            true,
            "Show built-in, translated gameplay tips when loadingscreen.yaml has no entries of its own. "
                + "Turn off to use only your own tips.txt and images. [Not Synced with Server]");
    BuiltinTips.SettingChanged += (_, _) => ScreenConfigLoader.Reload();

    LogLevel =
        config.BindInOrder(
            General,
            "Log level",
            ModLogLevel.Warning,
            "Show LoadingScreen messages at this level and above: Error, Warning, Info, or Debug. The configuration summary after each load shows at Warning when it reports problems and at Info otherwise. Debug also needs Debug in LogLevels under [Logging.Disk] in BepInEx.cfg. [Not Synced with Server]");

    ShareScreensWithPlayers =
        config.BindInOrder(
            General,
            "Share screens with players",
            true,
            "Send the server's loadingscreen.yaml and its images to players with this mod; players use them while connected. [Synced with Server]");

    SharedFileBandwidthKiBPerSecond =
        config.BindInOrder(
            General,
            "Shared file bandwidth KiB per second",
            4096,
            "Maximum combined transfer rate for shared images and audio across all players, in KiB per second. Applies on the server only. [Not Synced with Server]",
            new AcceptableValueRange<int>(256, 65536));

    ShowLoadingScreenOnTeleport =
        config.BindInOrder(
            Screens,
            "Loading screen on portal teleport",
            true,
            "Show a loading screen instead of the game's portal swirl. Needed for the per-biome teleport screens. [Synced with Server]");

    ShowLoadingScreenWhileSleeping =
        config.BindInOrder(
            Screens,
            "Loading screen while sleeping",
            false,
            "Show a loading image and tip while sleeping. Uses the sleep situation, then general; keeps the game's sleep text and spinner unless the selected layout hides them. [Synced with Server]");

    ShowLoadingScreenImmediatelyOnDeath =
        config.BindInOrder(
            Screens,
            "Loading screen right after death",
            true,
            "Show the loading screen as soon as you die, instead of after the death camera. [Synced with Server]");

    CaptionUseTipWhenNoText =
        config.BindInOrder(
            Screens,
            "Random tip when an entry has no text",
            true,
            "When a screen from loadingscreen.yaml has no text of its own, show a random line from tips.txt. "
                + "Off: show no text. [Synced with Server]");

    ChangeImageEverySeconds =
        config.BindInOrder(
            Timing,
            "Change image every N seconds",
            0,
            "Change the image while a loading screen stays visible. 0 turns the slideshow off. "
                + "Applies to in-game and game start loading screens. [Synced with Server]");

    ChangeTipEverySeconds =
        config.BindInOrder(
            Timing,
            "Change tip every N seconds",
            0,
            "Show another tip from tips.txt while a loading screen stays visible. 0 turns tip rotation off. "
                + "Entry text stays fixed. [Synced with Server]");

    InvulnerableWhileHeld =
        config.BindInOrder(
            Timing,
            "Invulnerable while held",
            true,
            "Protect the local player from damage while the screen waits for the voice or exit animation. When off, PvP or a monster noticing the player closes the screen. [Synced with Server]");

    WaitForVoice =
        config.BindInOrder(
            Timing,
            "Wait for the voice to finish",
            false,
            "After loading finishes, keep the screen visible while its entry voice is playing or loading. A key press skips it. Never holds during cinematics or on a dedicated server. [Synced with Server]");

    WaitForExitAnimation =
        config.BindInOrder(
            Timing,
            "Wait for the exit animation to finish",
            false,
            "After loading finishes, play layer exit animations before closing the screen. A key press skips it. Limited by Maximum hold time in seconds. Never holds during cinematics or on a dedicated server. [Synced with Server]");

    MaximumHoldSeconds =
        config.BindInOrder(
            Timing,
            "Maximum hold time in seconds",
            30,
            "Close a loading screen that waits for the voice or exit animation after this many seconds. [Synced with Server]",
            new AcceptableValueRange<int>(1, 300));

    SceneLoaderUseLoadingImages =
        config.BindInOrder(
            StartScreen,
            "Use loading images",
            true,
            "Show your loading images while the game itself starts, instead of the game's logos. [Not Synced with Server]");

    SceneLoaderShowProgressText =
        config.BindInOrder(
            StartScreen,
            "Show progress",
            true,
            "Show the loading percentage and elapsed seconds while the game starts. [Not Synced with Server]");

    PlayLoadingSounds =
        config.BindInOrder(Sound, "Play loading screen sounds", true,
            "Play music and voiced tips from loadingscreen.yaml while their loading screen is visible. [Not Synced with Server]");
    MusicVolumePercent =
        config.BindInOrder(Sound, "Music volume percent", 100,
            "Volume of loading screen music, from 0 to 100 percent. Also follows the game's master and music volume. [Not Synced with Server]",
            new AcceptableValueRange<int>(0, 100));
    VoiceVolumePercent =
        config.BindInOrder(Sound, "Voice volume percent", 100,
            "Volume of voiced tips, from 0 to 100 percent. Also follows the game's master and effects volume. [Not Synced with Server]",
            new AcceptableValueRange<int>(0, 100));
    MuteGameSounds =
        config.BindInOrder(Sound, "Mute game sounds on loading screens", GameSounds.None,
            "Game sounds to fade out while a loading screen is visible: None, or Effects (such as the portal hum), Music, or both separated by a comma. [Not Synced with Server]");

    ReadableText =
        config.BindInOrder(Accessibility, "Readable text", false,
            "Adjust text lightness for at least 4.5:1 contrast, and strengthen its background box if needed. [Not Synced with Server]");
    MinimumTextSize =
        config.BindInOrder(Accessibility, "Minimum text size", 0,
            "Smallest font size for text layers, tips and captions. 0 turns the minimum off. [Not Synced with Server]",
            new AcceptableValueRange<int>(0, 200));

    sync.Register(ChangeImageEverySeconds);
    sync.Register(ChangeTipEverySeconds);
    sync.Register(ShowLoadingScreenOnTeleport);
    sync.Register(ShowLoadingScreenWhileSleeping);
    sync.Register(InvulnerableWhileHeld);
    sync.Register(WaitForVoice);
    sync.Register(WaitForExitAnimation);
    sync.Register(MaximumHoldSeconds);
    sync.Register(ShowLoadingScreenImmediatelyOnDeath);
    sync.Register(CaptionUseTipWhenNoText);
    sync.RegisterLock(LockSyncedSettings);

    sync.Register(IsModEnabled);
    sync.Register(ImageFolder, clientControlled: true);
    sync.Register(TextLanguage, clientControlled: true);
    sync.Register(LogLevel, clientControlled: true);
    sync.Register(SceneLoaderUseLoadingImages, clientControlled: true);
    sync.Register(SceneLoaderShowProgressText, clientControlled: true);
    sync.Register(ShareScreensWithPlayers);
    sync.Register(PlayLoadingSounds, clientControlled: true);
    sync.Register(MusicVolumePercent, clientControlled: true);
    sync.Register(VoiceVolumePercent, clientControlled: true);
    sync.Register(MuteGameSounds, clientControlled: true);
    sync.Register(ReadableText, clientControlled: true);
    sync.Register(MinimumTextSize, clientControlled: true);
  }
}
