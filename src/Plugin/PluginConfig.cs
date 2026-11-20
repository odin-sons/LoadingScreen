namespace LoadingScreen;

using System;

using BepInEx.Configuration;

using global::LoadingScreen.Extensions;

using UnityEngine;

public static class PluginConfig {
  public static ConfigEntry<bool> IsModEnabled { get; private set; }

  public static ConfigEntry<bool> SceneLoaderUseLoadingImages { get; private set; }
  public static ConfigEntry<bool> SceneLoaderShowProgressText { get; private set; }
  public static ConfigEntry<bool> SceneLoaderCenterProgressIndicator { get; private set; }
  public static ConfigEntry<Vector2> SceneLoaderProgressIndicatorOffset { get; private set; }

  public static ConfigEntry<Color> LoadingImageBaseColor { get; private set; }

  public static ConfigEntry<bool> LoadingImageUseScaleLerp { get; private set; }
  public static ConfigEntry<float> LoadingImageScaleLerpEndScale { get; private set; }
  public static ConfigEntry<float> LoadingImageScaleLerpDuration { get; private set; }

  public static ConfigEntry<Color> LoadingScreenBackgroundColor { get; private set; }

  public static ConfigEntry<bool> LoadingScreenShowPanelSeparator { get; private set; }
  public static ConfigEntry<Vector2> LoadingScreenPanelSeparatorPosition { get; private set; }
  public static ConfigEntry<bool> LoadingScreenPanelSeparatorWithLayouts { get; private set; }

  public static ConfigEntry<Vector2> LoadingTipTextPosition { get; private set; }
  public static ConfigEntry<int> LoadingTipTextFontSize { get; private set; }
  public static ConfigEntry<Color> LoadingTipTextColor { get; private set; }

  public static ConfigEntry<bool> ShowLoadingScreenImmediatelyOnDeath { get; private set; }
  public static ConfigEntry<bool> ShowLoadingScreenOnTeleport { get; private set; }

  public static ConfigEntry<bool> CaptionUseTipWhenNoText { get; private set; }

  public static ConfigEntry<bool> ServeImagesToClients { get; private set; }
  public static ConfigEntry<bool> DownloadServerImages { get; private set; }

  const string General = "1 - General";
  const string Screens = "2 - Situations";
  const string Image = "3 - Image";
  const string Background = "4 - Background";
  const string TipText = "5 - Tip text";
  const string Separator = "6 - Separator line";
  const string StartScreen = "7 - Game start screen";
  const string Server = "8 - Server images";

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

    LoadingImageBaseColor =
        config.BindInOrder(
            Image,
            "Image tint",
            Color.white,
            "Color multiplied over every loading image. White keeps the original colors; gray darkens them.");

    LoadingImageBaseColor.SettingChanged += OnLoadingImageConfigChanged;

    LoadingImageUseScaleLerp =
        config.BindInOrder(
            Image,
            "Slow zoom",
            true,
            "Slowly zoom into the loading image while it is shown. A layer's 'animate' in loadingscreen.yaml "
                + "overrides this.");

    LoadingImageScaleLerpEndScale =
        config.BindInOrder(
            Image,
            "Zoom amount",
            1.05f,
            "How large the image gets by the end of the zoom. 1.05 means 5% larger.",
            new AcceptableValueRange<float>(0.5f, 1.5f));

    LoadingImageScaleLerpDuration =
        config.BindInOrder(
            Image,
            "Zoom duration (seconds)",
            15f,
            "How long the zoom takes from start to end.");

    LoadingScreenBackgroundColor =
        config.BindInOrder(
            Background,
            "Background color",
            Color.black,
            "Color behind the loading image, visible where the image does not fill the screen.");

    LoadingScreenBackgroundColor.SettingChanged += OnLoadingBackgroundConfigChanged;

    LoadingTipTextPosition =
        config.BindInOrder(
            TipText,
            "Position",
            new Vector2(0f, 90f),
            "Where the tip line sits: x from the screen center, y up from the bottom edge, in pixels.");

    LoadingTipTextPosition.SettingChanged += OnLoadingTipConfigChanged;

    LoadingTipTextFontSize =
        config.BindInOrder(
            TipText,
            "Font size",
            24,
            "Size of the tip text.",
            new AcceptableValueRange<int>(0, 64));

    LoadingTipTextFontSize.SettingChanged += OnLoadingTipConfigChanged;

    LoadingTipTextColor =
        config.BindInOrder(TipText, "Color", Color.white, "Color of the tip text.");

    LoadingTipTextColor.SettingChanged += OnLoadingTipConfigChanged;

    LoadingScreenShowPanelSeparator =
        config.BindInOrder(
            Separator,
            "Show separator line",
            true,
            "Show the game's ornament line above the tip text.");

    LoadingScreenShowPanelSeparator.SettingChanged += OnPanelSeparatorConfigChanged;

    LoadingScreenPanelSeparatorPosition =
        config.BindInOrder(
            Separator,
            "Position",
            new Vector2(0f, 150f),
            "Where the line sits: x from the screen center, y up from the bottom edge, in pixels.");

    LoadingScreenPanelSeparatorPosition.SettingChanged += OnPanelSeparatorConfigChanged;

    LoadingScreenPanelSeparatorWithLayouts =
        config.BindInOrder(
            Separator,
            "Keep on layout screens",
            true,
            "Keep the line on screens drawn by a layout from loadingscreen.yaml. A layout with its own "
                + "'separator' layer always hides it. Off: only layouts that add the line show one.");

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

    SceneLoaderCenterProgressIndicator =
        config.BindInOrder(
            StartScreen,
            "Center the spinner",
            true,
            "Move the loading spinner from the lower-right corner to the bottom center.");

    SceneLoaderProgressIndicatorOffset =
        config.BindInOrder(
            StartScreen,
            "Spinner position",
            new Vector2(0f, 200f),
            "Offset of the centered spinner from the bottom center, in pixels.");

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

  static void OnLoadingImageConfigChanged(object sender, EventArgs args) {
    LoadingManager.SetupLoadingImage();
  }

  static void OnLoadingTipConfigChanged(object sender, EventArgs args) {
    LoadingManager.SetupTipText();
  }

  static void OnLoadingBackgroundConfigChanged(object sender, EventArgs args) {
    LoadingManager.SetupLoadingBackground();
  }

  static void OnPanelSeparatorConfigChanged(object sender, EventArgs args) {
    LoadingManager.SetupPanelSeparator();
  }
}
