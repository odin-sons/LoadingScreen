## Changelog

### Unreleased

  * Tips and entry captions follow Valheim's language or the `[1 - General] Text language` override. Localized
    `tips.<code>.txt` files take precedence over `tips.txt`; entry `text` also accepts ISO-keyed translations.
  * Optional hold after HUD loading finishes, dismissed by a key or mouse click and capped at 30 seconds by default.
    Disabled on dedicated servers and during cinematics; `defaults.continuePrompt` styles the prompt line.
  * Optional loading image and tip while sleeping, with a `situations.sleep` list that falls back to `general`.
    Layouts can hide the game's sleep text and spinner with `showSleepUi: false`.
  * Optional tip rotation on HUD and main-menu loading screens: `[1 - General] Change tip every N seconds` (default
    0, off). Tips from `tips.txt` advance without repeating the previous tip when possible; entry text stays fixed.
  * Optional slideshow on HUD and game start loading screens: `[1 - General] Change image every N seconds` (default
    0, off). Situational entries cycle within their selected list and respect `order`; zoom restarts per image.

### 0.5.0

  * Optional looped music on layouts, lists and entries, plus one-shot voiced entry text (`sound` and `voice` in YAML).
    Audio follows the game's volume sliders; client cfg controls playback and music/voice volume. Servers can share
    bounded `.ogg` and `.wav` files with the screens.
    Voices are scaled like the game's sound effects, music like the game's music, and the game start screen stays
    silent until the game's volume settings are loaded. `Mute game sounds on loading screens` (`None` by default) fades
    out the game's effects, music or both while a screen is visible.

### 0.4.0

  * Optional config synchronization: Conditional Config Sync 1.0.10+ takes priority when installed; otherwise
    ServerSync is merged into the plugin. Slideshow timing and situation settings can follow the server, with a
    server lock; machine, language, game start, and server-screen preference settings remain local.

### 0.3.0

  * Optional full-screen vignette and color mask in YAML `defaults`, with per-layout overrides.

### 0.2.0 (2026-11-20)

  * Configurable image folder for `loadingscreen.yaml`, `tips.txt` and images; relative paths start at BepInEx.

  * Appearance settings moved from the BepInEx cfg to the YAML `defaults` section. Existing cfg appearance values
    are not migrated; the built-in YAML values preserve the former cfg defaults.
  * Entry conditions: `when` with `knownItem`, `hasItem`, `knownRecipe`, `knownBiome`, `globalKey` and `playerKey`,
    each also as `not...`; an entry is shown only when every condition holds.
  * Entries may have only a `text`; it is shown over a random plain image.
  * Death screens per enemy: `death.enemy.<creature prefab>` is tried before the damage type and the hit type.
  * `order: random | sequential`, for the whole file or one list.
  * `separator: game` layer and the setting to keep or hide the built-in separator on layout screens.
  * Settings renamed to plain names in numbered sections, each with a description. Old settings are not migrated.
  * Fixed a white loading screen when no custom images exist.

  * Situational loading screens from `BepInEx/config/LoadingScreen/loadingscreen.yaml` (YamlDotNet, new dependency
    `ValheimModding-YamlDotNet`):
    * portal teleports pick entries by destination biome, respawns by cause of death (hit type, then the dominant
      damage type for hits), everything else by the general list; random without immediate repeats;
    * config problems are logged with line numbers and never stop the rest of the config from loading;
    * a commented example config is created on first start.
  * Layered layouts: color layers, fixed images, random image pools, the entry image and a caption text layer, each
    with fit, anchor, offsets, opacity, gradient edges and the scale animation; placement falls back from the entry
    to the layer, the layout and the global defaults.
  * Entry captions replace the tip; new setting `[Situations] useTipWhenNoText`.
  * Server images: a server running the mod shares its config and images; clients download them in the background
    into `BepInEx/cache/LoadingScreen/` with size, path and hash checks. New settings `[ServerImages] serveToClients`
    and `downloadFromServer`.
  * Console command `loadingscreen reload|status`.
  * Images are checked before decoding (PNG or JPEG, at most 8192 px per side and 16.7 megapixels) and decoded
    without mipmaps or a CPU copy.
  * The chosen image is decoded when the death or teleport happens; at most 16 decoded images are kept in memory.
  * Not yet tested in the game.

### 0.1.0 (2026-11-20)

  * Modified fork of Intermission 1.10.0 by redseiko (ComfyMods, upstream commit `cc8647b`), changed by Odin_Sons.
  * Renamed the plugin so it can be installed next to Intermission:
    * GUID `Odin_Sons.LoadingScreen`, name, namespace and assembly `LoadingScreen`.
    * Config file `BepInEx/config/Odin_Sons.LoadingScreen.cfg`, image and tip folder `BepInEx/config/LoadingScreen/`.
  * Build: game assemblies are publicized at build time with `BepInEx.AssemblyPublicizer.MSBuild`; new `BepInExPath`
    property for BepInEx installs outside the game folder (mod manager profiles).
  * No functional changes yet.

## Intermission changelog (upstream)

### 1.10.0

  * Updated for the `v1.0.21` patch.
  * Migrated to SDK-style project.
  * Updated mod icon.
  * Added new logic to toggle showing the loading-screen immediately on-death.    
  * Added new logic to toggle showing the loading-screen instead of the vanilla portal animation.
  * Added new config-options for the above:
    * `[Hud.LoadingScreen] showImmediatelyOnDeath`
    * `[Hud.LoadingScreen] showOnTeleport`
  * Code clean-up and refactoring.

### 1.9.0

  * Fixed for the `0.221.10` patch.

### 1.8.0

  * Updated for the `v0.219.16` patch.
  * Fixed an issue with custom loading tips not being shown when manually advanced.
  * Loading tips can now be manually advanced when teleporting (vanilla is limited to respawning only).
  * Removed vanilla logging of loading tips when they are manually advanced.

### 1.7.0

  * Updated for the `v0.218.21` patch.
  * Fixed the `LoadingIndicator` not centering on the initial `SceneLoader` screen.
  * Added a new config option `SceneLoadeer.progressIndicatorOffset` for the centered indicator.

### 1.6.0

  * Updated for the `v0.218.19` patch.
  * Added a solid color background to the loading screen to handle images that do not fill the entire screen.
    * Color can be changed with config option: `LoadingScreen.Background.backgroundColor`
  * Small code clean-up and refactornig.

### 1.5.0

  * Updated for the `v0.217.46` patch.
  * Added new `SceneLoader` patch for custom loading images and progress indicator.
  * Added new config options to control new feature:
    * `SceneLoader.useLoadingImages`
    * `SceneLoader.showProgressText`
    * `SceneLoader.centerProgressIndicator`

### 1.4.0

  * Updated for the `v0.217.38` patch.
  * Moved changelog into `CHANGELOG.md`.
  * Bumped up `<LangVersion>` to C# 10.
  * Custom loading images will now be randomly shown at least once before resorting into a new random order.
  * Code clean-up and refactoring.

### 1.3.0

  * Fixed for the `v0.217.24` patch.
  * Added work-around for the `FejdStartup` loading text UI state being locked due to the `menuAnimator`.
  * Removed `shadowEffectColor` and `shadowEffectDistance` config options due to the change to `TextMeshPro`.

### 1.2.0

  * Added support for `.jpg` image files.

### 1.1.0

  * Updated for Valheim `v0.216.9` patch.

### 1.0.2

  * Modified TipText to horizontally wrap with width set to entire screen.
  * Updated BepInEx dependency to `denikson-BepInExPack_Valheim-5.4.2100`.

### 1.0.1

  * Fixed a bug where config changes to LoadingImage and PanelSeparator were not reflected accurately.

### 1.0.0

  * Initial release.
