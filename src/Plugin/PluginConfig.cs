namespace LoadingScreen;

using BepInEx.Configuration;

using global::LoadingScreen.Extensions;
using global::LoadingScreen.Model;

public static class PluginConfig {
  public static ConfigEntry<bool> IsModEnabled { get; private set; }
  public static ConfigEntry<string> ImageFolder { get; private set; }

  public static ConfigEntry<bool> SceneLoaderUseLoadingImages { get; private set; }
  public static ConfigEntry<bool> SceneLoaderShowProgressText { get; private set; }

  public static ConfigEntry<bool> ShowLoadingScreenImmediatelyOnDeath { get; private set; }
  public static ConfigEntry<bool> ShowLoadingScreenOnTeleport { get; private set; }
  public static ConfigEntry<bool> CaptionUseTipWhenNoText { get; private set; }

  public static ConfigEntry<bool> ServeImagesToClients { get; private set; }
  public static ConfigEntry<bool> DownloadServerImages { get; private set; }
  public static ConfigEntry<bool> LockSyncedSettings { get; private set; }
  public static ConfigEntry<bool> PlayLoadingSounds { get; private set; }
  public static ConfigEntry<int> MusicVolumePercent { get; private set; }
  public static ConfigEntry<int> VoiceVolumePercent { get; private set; }
  public static ConfigEntry<GameSounds> MuteGameSounds { get; private set; }

  const string General = "1 - General";
  const string Screens = "2 - Situations";
  const string StartScreen = "3 - Game start screen";
  const string Server = "4 - Server images";
  const string Synchronization = "5 - Config synchronization";
  const string Sound = "6 - Sound";

  internal static void BindConfig(ConfigFile config, ConfigSynchronization sync) {
    IsModEnabled =
        config.BindInOrder(General, "Enabled", true, "Turn the whole mod on or off. Needs a game restart.");

    ImageFolder =
        config.BindInOrder(
            General,
            "Image folder",
            ImageFolderPath.DefaultFolder,
            "Folder with loadingscreen.yaml, tips.txt, and images. Relative paths start at the BepInEx folder. "
                + "Needs a game restart.");

    ShowLoadingScreenOnTeleport =
        config.BindInOrder(
            Screens,
            "Loading screen on portal teleport",
            true,
            "Show a loading screen instead of the game's portal swirl. Needed for the per-biome teleport screens.");

    ShowLoadingScreenImmediatelyOnDeath =
        config.BindInOrder(
            Screens,
            "Loading screen right after death",
            true,
            "Show the loading screen as soon as you die, instead of after the death camera.");

    CaptionUseTipWhenNoText =
        config.BindInOrder(
            Screens,
            "Random tip when an entry has no text",
            true,
            "When a screen from loadingscreen.yaml has no text of its own, show a random line from tips.txt. "
                + "Off: show no text.");

    SceneLoaderUseLoadingImages =
        config.BindInOrder(
            StartScreen,
            "Use loading images",
            true,
            "Show your loading images while the game itself starts, instead of the game's logos.");

    SceneLoaderShowProgressText =
        config.BindInOrder(
            StartScreen,
            "Show progress",
            true,
            "Show the loading percentage and elapsed seconds while the game starts.");

    ServeImagesToClients =
        config.BindInOrder(
            Server,
            "Share with players (server)",
            true,
            "On a server: send loadingscreen.yaml and its images to players who have this mod.");

    DownloadServerImages =
        config.BindInOrder(
            Server,
            "Use the server's screens (player)",
            true,
            "As a player: while connected, use the server's loading screens and download their images in the "
                + "background into BepInEx/cache/LoadingScreen.");

    LockSyncedSettings =
        config.BindInOrder(
            Synchronization,
            "Lock synced settings",
            true,
            "Allow only server admins to change synchronized settings while connected.");

    PlayLoadingSounds =
        config.BindInOrder(Sound, "Play loading screen sounds", true,
            "Play music and voiced tips from loadingscreen.yaml while their loading screen is visible.");
    MusicVolumePercent =
        config.BindInOrder(Sound, "Music volume percent", 100,
            "Volume of loading screen music, from 0 to 100 percent. Also follows the game's master and music volume.",
            new AcceptableValueRange<int>(0, 100));
    VoiceVolumePercent =
        config.BindInOrder(Sound, "Voice volume percent", 100,
            "Volume of voiced tips, from 0 to 100 percent. Also follows the game's master and effects volume.",
            new AcceptableValueRange<int>(0, 100));
    MuteGameSounds =
        config.BindInOrder(Sound, "Mute game sounds on loading screens", GameSounds.None,
            "Game sounds to fade out while a loading screen is visible: None, or Effects (such as the portal hum), Music, or both separated by a comma.");

    sync.Register(ShowLoadingScreenOnTeleport);
    sync.Register(ShowLoadingScreenImmediatelyOnDeath);
    sync.Register(CaptionUseTipWhenNoText);
    sync.RegisterLock(LockSyncedSettings);

    sync.Register(IsModEnabled, clientControlled: true);
    sync.Register(ImageFolder, clientControlled: true);
    sync.Register(SceneLoaderUseLoadingImages, clientControlled: true);
    sync.Register(SceneLoaderShowProgressText, clientControlled: true);
    sync.Register(ServeImagesToClients, clientControlled: true);
    sync.Register(DownloadServerImages, clientControlled: true);
    sync.Register(PlayLoadingSounds, clientControlled: true);
    sync.Register(MusicVolumePercent, clientControlled: true);
    sync.Register(VoiceVolumePercent, clientControlled: true);
    sync.Register(MuteGameSounds, clientControlled: true);
  }
}
