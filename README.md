# LoadingScreen

Give Valheim's loading screens a look that fits the journey: show images and tips for a destination biome, a
death, or any ordinary load. Server owners can share their screens with players, while each player can customize
layouts, captions, progress, and audio locally.

## Features

- **Screens for the moment.** Show biome images for portal travel, death images for respawns, and a general pool
  elsewhere. Lists can cycle in order or pick without immediate repeats.
- **Custom layouts.** Combine images, captions, progress, and spinners with placement, fades, and animations.
- **Useful tips.** Show translated captions or tips, and target entries to players by items, recipes, biomes, or
  world progress.
- **Built-in tips.** Start with translated, situation-aware gameplay tips before adding any custom entries.
- **Music and voice.** Play looping music and voiced entry captions with independent volume controls.
- **Shared server screens.** Server owners can distribute their config and images to connected players in the
  background.
- **Intermission compatibility.** Existing plain images and tips remain a fallback; reload files in game with
  `loadingscreen reload`.

## Installation

Requires `denikson-BepInExPack_Valheim` and `ValheimModding-YamlDotNet`. Put `LoadingScreen.dll` into
`BepInEx/plugins`. Coming from Intermission? See [Migrating from Intermission](https://github.com/odin-sons/LoadingScreen/blob/main/docs/migrating-from-intermission.md).

LoadingScreen does not load next to another loading screen mod: they patch the same screens and break each other.
BepInEx then writes `Could not load [LoadingScreen ...] because it is incompatible with: ...` to
`BepInEx/LogOutput.log`. Remove the other mod to use LoadingScreen. The check covers Intermission
(`redseiko.valheim.intermission`), CustomLoadingScreens and the packs that bundle it (`aedenthorn.CustomLoadingScreens`,
`cjayride.CustomLoadingScreens`), Loading Tales, Immersive Loading, Runeheim Loading Screen and Server Loading Screen.
Image packs that depend on Intermission, such as ComfyLoadingScreens, install Intermission and count as it.

On first start the mod creates `BepInEx/config/LoadingScreen/loadingscreen.yaml` with a commented example. While
everything in it stays commented out, built-in gameplay tips appear over your images or the game's image.

## Quick start

Put images in `BepInEx/config/LoadingScreen/`, then replace the commented YAML example with a config like this:

```yaml
layouts:
  simple:
    layers:
      - image: entry
        place: { fit: cover }
      - text: caption
        place: { position: bottom }
      - progress: ring
        place: { position: center }
      - spinner: hammer
        place: { position: center }
when:
  general:
    layout: simple
    entries:
      - image: forest.jpg
        text: Welcome to Valheim.
```

The image and caption layers show the selected entry. On the game start screen, the progress ring and hammer
spinner provide loading feedback. See the full configuration reference for situations, settings, and layer options.

## Documentation

- [Online editor](https://odin-sons.github.io/LoadingScreen/)
- [Configuration reference](https://github.com/odin-sons/LoadingScreen/blob/main/docs/configuration.md)
- [Migrating from Intermission](https://github.com/odin-sons/LoadingScreen/blob/main/docs/migrating-from-intermission.md)
- [Changelog](https://github.com/odin-sons/LoadingScreen/blob/main/CHANGELOG.md)
- [Contributing](https://github.com/odin-sons/LoadingScreen/blob/main/CONTRIBUTING.md)
- [Development milestones](https://github.com/odin-sons/LoadingScreen/milestones)

## License and attribution

This project is a modified fork of **Intermission 1.10.0** by redseiko
(<https://github.com/redseiko/ComfyMods/tree/main/Intermission>, upstream commit `cc8647bec282d50db0f62bb46f797d1c4bbc97d4`).
Only the Intermission plugin was taken from the ComfyMods repository; it is self-contained.

- Licensed under **GPL-3.0** (see [`LICENSE`](https://github.com/odin-sons/LoadingScreen/blob/main/LICENSE)), the same license as the upstream repository.
- Original work: Copyright (C) redseiko / ComfyMods. All upstream copyright notices stay in place.
- Changes are tracked in this repository's history and in [`CHANGELOG.md`](https://github.com/odin-sons/LoadingScreen/blob/main/CHANGELOG.md).
- The optional [Conditional Config Sync adapter](https://github.com/shudnal/ConditionalConfigSync/tree/430a064d5c815c8b90066442ba1b3a40647efa02/ConditionalConfigSync.API)
  is included as source under its [upstream license](https://github.com/odin-sons/LoadingScreen/blob/main/src/Vendor/ConditionalConfigSync/LICENSE).
  [ServerSync v1.20](https://github.com/blaxxun-boop/ServerSync/releases/tag/v1.20)
  is merged into the plugin DLL; its license is in [`lib/ServerSync/LICENSE.txt`](https://github.com/odin-sons/LoadingScreen/blob/main/lib/ServerSync/LICENSE.txt).
- Planned reuse of code from [cjayride/CustomLoadingScreens](https://github.com/cjayride/CustomLoadingScreens_Fork),
  itself a fork of [aedenthorn/CustomLoadingScreens](https://github.com/aedenthorn/ValheimMods/tree/master/CustomLoadingScreens).
  Both repositories state that their content is released into the public domain, which is compatible with GPL-3.0.
  Any code taken from them will be marked in the source and credited here.
