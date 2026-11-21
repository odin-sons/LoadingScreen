namespace LoadingScreen;

using BepInEx.Configuration;

using global::LoadingScreen.Extensions;

public static class PluginConfig {
  public static ConfigEntry<bool> IsModEnabled { get; private set; }

  public static ConfigEntry<bool> SceneLoaderUseLoadingImages { get; private set; }
  public static ConfigEntry<bool> SceneLoaderShowProgressText { get; private set; }

  public static ConfigEntry<bool> ShowLoadingScreenImmediatelyOnDeath { get; private set; }
  public static ConfigEntry<bool> ShowLoadingScreenOnTeleport { get; private set; }
  public static ConfigEntry<bool> CaptionUseTipWhenNoText { get; private set; }

  public static ConfigEntry<bool> ServeImagesToClients { get; private set; }
  public static ConfigEntry<bool> DownloadServerImages { get; private set; }

  const string General = "1 - General";
  const string Screens = "2 - Situations";
  const string StartScreen = "3 - Game start screen";
  const string Server = "4 - Server images";

  public static void BindConfig(ConfigFile config) {
    IsModEnabled =
        config.BindInOrder(General, "Enabled", true, "Turn the whole mod on or off. Needs a game restart.");

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
  }
}
