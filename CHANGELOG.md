## Changelog

### Unreleased

Adds progress, spinner and animated layers, localized prompts, readability and audio-wait options, a reorganized
cfg and clearer logs.

#### Features

  * **Safe held screens.** A server-synced setting protects the local player from damage during a held HUD screen by
    default. When disabled, PvP or a nearby hostile monster noticing the player closes the screen automatically.
  * **Progress layer.** `progress` layers with bar, ring, and percentage styles. Game start uses real scene-load
    progress; portal and death use destination-zone and nearby-object readiness; sleep uses game time. Server joining
    shows localized connection stages. Ready area progress stays at 100% with a soft pulse during game delays.
  * **Spinner layer.** `spinner` layers for game start, portal, death and sleep layouts, with Valheim's hammer sprite,
    generated ring and dots, or a custom image. Spinners use `place` and spin by default; layer animations can replace
    the spin. Existing `defaults.spinner` settings keep their game start behavior through an implicit hammer layer
    when centered; an explicit spinner layer takes precedence.
  * **Death tip order.** Death tries enemy, dominant damage type, hit type, death biome, custom biome's base biome,
    then general.
  * **Custom biomes.** Expand World Data custom biomes use their `biome:` identifiers in `when.biome` lists and
    `knownBiome` conditions; unresolved names warn at runtime.
  * **Layer animations.** Enter, idle and exit animations for YAML layers, built-in motion presets, and custom presets
    in `animations.yaml`. Server screen sharing and reload include custom presets.
  * **Layered game start ring.** With the legacy centered spinner, the game start ring is now a `progress: ring`
    layer drawn with Valheim's ring sprite, and the hammer turns in its center; Valheim's indicator is hidden.
  * **Animation origin.** `animation.origin` picks the point a layer scales and rotates around, like CSS
    `transform-origin`; layers default to their center, so spinners turn in place.
  * **Exit animation wait.** Server-synced `Wait for the exit animation to finish` timing setting, off by default.
    Exit playback uses the existing maximum hold time as a cap.
  * **Voice wait.** Server-synced `Wait for the voice to finish` timing setting (off by default). A HUD loading screen
    can stay visible until its entry voice ends, with the existing maximum hold time as a cap.
  * **Localized prompts.** The mod's continue prompt and integer game-start progress display are localized for
    Valheim's 36 languages, with per-key `translations.yaml` overrides in the local config folder and English
    fallback.
  * **Automatic text color.** YAML `color: auto` and `background: auto` for text layers, based on the image area
    behind the text box.
  * **Accessibility settings.** Personal `Readable text` and `Minimum text size` settings (off by default).
  * **Log level.** Client-controlled `Log level` setting (Error, Warning by default, Info, Debug). Detailed screen
    selection, audio volume, server transfer, and image timing messages are available at Debug. Choosing Debug warns
    once when BepInEx.cfg filters Debug out of the disk log.
  * **Log context.** Log messages start with a short sentence followed by `key=value` details (file, YAML line, layer,
    screen, side, rejected value), stack traces go to Debug, and repeated warnings are logged once.
  * **Live toggle.** `Mod enabled` applies without a restart.
  * **Subsystem sources.** Log lines come from `LoadingScreen.Config`, `LoadingScreen.Audio`, `LoadingScreen.Server`
    and `LoadingScreen.Render` BepInEx sources, so one area can be found with a single search; startup messages stay
    on `LoadingScreen`. `Log level` filters all of them.
  * **Problem summary.** After a load or reload that logs warnings or errors for `loadingscreen.yaml`,
    `animations.yaml` or `translations.yaml`, the console shows one line with the problem count per file and points
    to LogOutput.log; the main menu shows it once in a dismissible popup when the console is closed. The text uses the
    new `configProblems` translation key.
  * **Background world load.** Experimental `[7 - Experimental] Load world in background`, off by default, loads the
    world scene asynchronously when starting or joining a world so the loading screen keeps animating, with high
    background loading priority and menu input blocked meanwhile. Falls back to the game's synchronous load with a
    warning if the background load cannot start. Each world load logs its read, activation and total time at Info.
  * **Audio settings sliders.** `Loading screen music` and `Loading screen voice` sliders in the game's Audio
    settings edit `Music volume percent` and `Voice volume percent`; `OK` saves them and `Back` discards them. Labels
    use the new `musicVolumeSlider` and `voiceVolumeSlider` translation keys. The sliders are not added while
    ValheimAudioControl, which rebuilds the Audio tab, is installed.
  * **NotSoLoud compatibility.** Loading screen music and voice stay out of the game's effects mixer group when
    NotSoLoud routes unassigned audio there, so they are not scaled twice or faded by the effects mute.

#### Changes

  * **Incompatible mods.** LoadingScreen does not load while Intermission or CustomLoadingScreens is installed;
    BepInEx logs the reason. The mods patched the same loading screens and broke each other.
  * **Biome lists.** `when.biome` replaces `when.teleport` for destination biome lists and death biome tips.
  * **`when` key.** The YAML top-level `situations` key is renamed to `when`; existing YAML files must use `when`.
  * **Zoom fields.** `animation` replaces the old `animate` and `defaults.zoom` fields. Files from 0.6.0 still
    load without warnings: `animate` maps to the `zoom-in` idle preset, `zoom.enabled: false` turns it off and
    `zoom.seconds` sets the speed; `zoom.amount` is ignored.
  * **Cfg layout.** The cfg is reorganized into General, When to show, Timing, Game start screen and Sound. Each
    setting states whether it is synchronized. One synced `Share screens with players` switch controls transfer.
  * **Fatal startup errors.** Failures that disable the mod (configuration or image folder setup, Harmony patching)
    are logged at BepInEx `Fatal` level with their context and stack trace; other errors stay at `Error`.
  * **Load summary.** The configuration summary after each load is logged at BepInEx `Message` level. It appears at
    `Log level` Warning and above when it reports problems, and at Info and above otherwise.

#### Fixes

  * **Plain image animation.** Plain images follow the idle animation and speed from `defaults.animation`, so
    `animation: none` stops their slow zoom.
  * **Above HUD mods.** The loading screen now paints above canvases added by HUD mods instead of under them.
  * **Setting sync.** Server settings and the lock now actually synchronize, while personal settings stay local.
  * **Log format.** Log lines follow the plain BepInEx format without the mod's own timestamp.

### 0.6.0 (2026-11-21)

Adds localized text, a continue prompt, sleep screens, tip and image rotation, simpler YAML layers, and seamless
server joins.

#### Features

  * **Text language.** Tips and entry captions follow Valheim's language or the `[1 - General] Text language`
    override. Localized `tips.<code>.txt` files take precedence over `tips.txt`; entry `text` also accepts ISO-keyed
    translations.
  * **Continue prompt.** Optional hold after HUD loading finishes, dismissed by a key or mouse click and capped at
    30 seconds by default. Disabled on dedicated servers and during cinematics; `defaults.continuePrompt` styles the
    prompt line.
  * **Sleep screen.** Optional loading image and tip while sleeping, with a `when.sleep` list that falls back to
    `general`. Layouts can hide the game's sleep text and spinner with `showSleepUi: false`.
  * **Tip rotation.** Optional on HUD and main-menu loading screens: `[3 - Timing] Change tip every N seconds`
    (default 0, off). Tips from `tips.txt` advance without repeating the previous tip when possible; entry text stays
    fixed.
  * **Slideshow.** Optional on HUD and game start loading screens: `[3 - Timing] Change image every N seconds`
    (default 0, off). Situational entries cycle within their selected list and respect `order`; zoom restarts per
    image.

#### Changes

  * **Layer content.** YAML layer content is simplified to `image`, `text` or `color`; `image` accepts a path, path
    list, `entry` or `separator`.
  * **Enemy and placement keys.** Enemy prefab lists now live under `when.enemy`, and placement at every scope uses
    `place`.

#### Fixes

  * **Join consistency.** A server join keeps the same loading image, caption and zoom across the menu, scene loader
    and HUD.
  * **Audio handover.** Music and voice continue through host handovers without replaying the entry voice or dipping
    in volume during the fade.

### 0.5.0 (2026-11-21)

Adds music and voiced entry text to loading screens, matched to the game's volume.

#### Features

  * **Music and voice.** Optional looped music on layouts, lists and entries, plus one-shot voiced entry text (`sound`
    and `voice` in YAML). Audio follows the game's volume sliders; client cfg controls playback and music/voice
    volume.
  * **Shared audio.** Servers can share bounded `.ogg` and `.wav` files with the screens.
  * **Game sound muting.** `Mute game sounds on loading screens` (`None` by default) fades out the game's effects,
    music or both while a screen is visible.

#### Fixes

  * **Volume matching.** Voices are scaled like the game's sound effects, music like the game's music, and the game
    start screen stays silent until the game's volume settings are loaded.

### 0.4.0 (2026-11-21)

Adds optional config synchronization with the server.

#### Features

  * **Config sync.** Conditional Config Sync 1.0.10+ takes priority when installed; otherwise ServerSync is merged
    into the plugin.
  * **Server lock.** Slideshow timing and situation settings can follow the server, with a server lock; machine,
    language, game start, sound and key-hold settings remain local.

### 0.3.0 (2026-11-21)

Adds a vignette and color mask.

#### Features

  * **Vignette and mask.** Optional full-screen vignette and color mask in YAML `defaults`, with per-layout overrides.

### 0.2.0 (2026-11-21)

Adds situational, layered loading screens from a YAML config with server-shared images.

#### Features

  * **Situational screens.** Loading screens from `BepInEx/config/LoadingScreen/loadingscreen.yaml` (YamlDotNet, new
    dependency `ValheimModding-YamlDotNet`):
    * portal teleports pick entries by destination biome, respawns by cause of death (hit type, then the dominant
      damage type for hits), everything else by the general list; random without immediate repeats;
    * config problems are logged with line numbers and never stop the rest of the config from loading;
    * a commented example config is created on first start.
  * **Layered layouts.** Color layers, fixed images, random image pools, the entry image and a caption text layer,
    each with fit, anchor, offsets, opacity, gradient edges and the scale animation; placement falls back from the
    entry to the layer, the layout and the global defaults.
  * **Image folder.** Configurable image folder for `loadingscreen.yaml`, `tips.txt` and images; relative paths start
    at BepInEx.
  * **Entry conditions.** `when` with `knownItem`, `hasItem`, `knownRecipe`, `knownBiome`, `globalKey` and
    `playerKey`, each also as `not...`; an entry is shown only when every condition holds.
  * **Text-only entries.** Entries may have only a `text`; it is shown over a random plain image.
  * **Entry captions.** Entry captions replace the tip; new setting `[When to show] useTipWhenNoText`.
  * **Enemy deaths.** Death screens per enemy: `when.enemy.<creature prefab>` is tried before the damage type and the
    hit type.
  * **Order.** `order: random | sequential`, for the whole file or one list.
  * **Separator layer.** `image: separator` layer and the setting to keep or hide the built-in separator on layout
    screens.
  * **Server images.** A server running the mod shares its config and images; clients download them in the
    background into `BepInEx/cache/LoadingScreen/` with size, path and hash checks. New settings
    `[ServerImages] serveToClients` and `downloadFromServer`.
  * **Console command.** `loadingscreen reload|status`.

#### Changes

  * **Appearance in YAML.** Appearance settings moved from the BepInEx cfg to the YAML `defaults` section. Existing
    cfg appearance values are not migrated; the built-in YAML values preserve the former cfg defaults.
  * **Setting names.** Settings renamed to plain names in numbered sections, each with a description. Old settings
    are not migrated.

#### Fixes

  * **White screen.** Fixed a white loading screen when no custom images exist.
  * **Image checks.** Images are checked before decoding (PNG or JPEG, at most 8192 px per side and 16.7 megapixels)
    and decoded without mipmaps or a CPU copy.
  * **Memory use.** The chosen image is decoded when the death or teleport happens; at most 16 decoded images are
    kept in memory.

### 0.1.0 (2026-11-20)

First release: Intermission renamed so both mods can be installed side by side. No functional changes yet.

#### Features

  * **Fork.** Modified fork of Intermission 1.10.0 by redseiko (ComfyMods, upstream commit `cc8647b`), changed by
    Odin_Sons.
  * **New identity.** The plugin is renamed so it can be installed next to Intermission:
    * GUID `Odin_Sons.LoadingScreen`, name, namespace and assembly `LoadingScreen`.
    * Config file `BepInEx/config/Odin_Sons.LoadingScreen.cfg`, image and tip folder `BepInEx/config/LoadingScreen/`.
  * **Build.** Game assemblies are publicized at build time with `BepInEx.AssemblyPublicizer.MSBuild`; new
    `BepInExPath` property for BepInEx installs outside the game folder (mod manager profiles).

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
