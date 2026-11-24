## Changelog

### 0.10.0 (2026-11-24)

Safer server file sharing, lower memory use for images, and one set of config rules for the mod and the editor.

#### Features

  * **Shared file bandwidth.** The server caps the total traffic for shared images and audio with the new
    `Shared file bandwidth KiB per second` setting and shares it fairly between players. A busy server tells clients
    to retry later instead of staying silent, so downloads slow down rather than fail, and a missing or refused file
    stops at once instead of after three timeouts.
  * **Config schema.** A JSON Schema generated from the mod's config types is published with the editor, and the
    example config points to it, so editors with YAML support complete keys and flag mistakes. The online editor
    takes its option lists from the same schema.

#### Fixes

  * **Image memory.** Images are no longer copied pixel by pixel when they load, the image cache is limited by size
    instead of by count, a screen shares a memory budget between its images, and a layout uses at most 32 layers.
    An image that cannot be decoded is skipped with a log message.
  * **Paths.** Absolute image and audio paths such as `C:/x.png` are rejected the same way on every OS and in the
    editor.
  * **Translations.** A broken or oversized `translations.yaml` no longer crashes the game at startup; the built-in
    text is used instead.
  * **Logs.** Text sent by a server is escaped and shortened in log lines, so it cannot forge log entries.
  * **Performance.** The progress layer no longer formats text and scans the area every frame, and stale UI
    references are dropped after scene reloads.
  * **Editor.** Moving a layer or entry up or down keeps its comments with it.

### 0.9.2 (2026-11-23)

Fixes in the online editor from the architecture and security review.

#### Fixes

  * **YAML source.** Edits in the YAML box apply to the form as soon as they parse, and the form is locked while
    the YAML has an error, so a form edit can no longer overwrite unapplied text. Files with aliases, nesting
    deeper than 32 levels or over 512 KB are rejected instead of freezing the tab.
  * **Validation.** The editor checks configs with the same rules as the mod, including empty values,
    case-insensitive keys, language names and the tip, prompt, separator and spinner settings, and reports the
    line of the offending value. Text such as `Press *any* key` is no longer mistaken for an alias.
  * **Round trip.** Form edits keep comments, quoting and untouched values in the YAML, renamed layouts and lists
    keep their place, and values such as `2024-05-01` or `0x10` stay as written.
  * **Privacy.** Colours from a loaded file reach the preview only as `#RRGGBB` or `#RRGGBBAA`, so a shared config
    cannot make the browser load a remote address. The page also sets a Content-Security-Policy.
  * **Robustness.** Switching tabs no longer rewrites short forms in the YAML, values the form cannot show get a
    note instead of breaking the section, and a failed data load shows a message.
  * **Accessibility.** Keyboard focus stays in place when the form updates, the section tabs work as a tab list
    with arrow keys, buttons name the card they act on, and validation results are announced once.

### 0.9.1 (2026-11-23)

Fixes from the architecture and security review.

#### Fixes

  * **Held screen.** Movement, attacks and interaction are blocked while a loading screen is held, so the
    damage protection can no longer be used in combat. Any key, mouse button, gamepad stick or trigger ends the
    hold, including voice and exit-animation waits.
  * **Game updates.** If any patch fails after a Valheim update, all patches are rolled back and the mod stays
    fully off instead of half-working.
  * **Shared files.** The server sends its loading screen files only to players who passed the password,
    whitelist and ban checks.
  * **Text colour.** Automatic text and box colours no longer sample layers from the previous screen.

### 0.9.0 (2026-11-23)

Adds a browser-based helper for creating loading screen configurations.

#### Features

  * **Online editor.** Build defaults, layouts and situational entries in a static browser editor at
    <https://odin-sons.github.io/LoadingScreen/> with YAML output, validation and a local image preview.

### 0.8.0 (2026-11-23)

Adds translated gameplay tips that make loading screens useful immediately after installation.

#### Features

  * **Built-in tips.** Text-only tips cover ordinary loads, biome travel, deaths, and sleep until you define your own
    entries; a local cfg switch turns them off.

### 0.7.0 (2026-11-23)

Adds progress, spinner and animated layers, layouts that draw only their own layers, a safer key hold, audio
sliders, localized prompts and readability options, a reorganized cfg and clearer logs.

#### Features

  * **Continue prompt layer.** `text: prompt` shows the localized key prompt only while a layout waits for input;
    it accepts the same styling and animation as `text: caption`.
  * **Safe held screens.** A server-synced setting protects the local player from damage during a held HUD screen by
    default. When disabled, PvP or a nearby hostile monster noticing the player closes the screen automatically.
  * **Progress layer.** `progress` layers with bar, ring, and percentage styles. Game start uses real scene-load
    progress; portal and death use destination-zone and nearby-object readiness; sleep uses game time. Server joining
    shows localized connection stages. Ready area progress stays at 100% with a soft pulse during game delays.
  * **Spinner layer.** `spinner` layers for game start, portal, death and sleep layouts, with Valheim's hammer sprite,
    generated ring and dots, or a custom image. Spinners use `place` and spin by default; layer animations can replace
    the spin. `defaults.spinner` continues to style game start screens without a layout.
  * **Death tip order.** Death tries enemy, dominant damage type, hit type, death biome, custom biome's base biome,
    then general.
  * **Custom biomes.** Expand World Data custom biomes use their `biome:` identifiers in `when.biome` lists and
    `knownBiome` conditions; unresolved names warn at runtime.
  * **Layer animations.** Enter, idle and exit animations for YAML layers, built-in motion presets, and custom presets
    in `animations.yaml`. Server screen sharing and reload include custom presets.
  * **Layered game start ring.** A `progress: ring` layer draws a game start ring, and `spinner: hammer` can turn in
    its center. Valheim's indicator is hidden while a layout is shown.
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

  * **Layout-only layers.** Layout screens now draw only their declared layers, including text, separators, progress,
    spinners, and key prompts. Existing layouts must add layers for elements they previously received implicitly;
    screens without a layout keep their previous appearance.
  * **Incompatible mods.** LoadingScreen does not load next to Intermission, CustomLoadingScreens and its forks, or
    other loading screen replacers; BepInEx logs the reason. The mods patched the same screens and broke each other.
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
