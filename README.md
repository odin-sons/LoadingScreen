# LoadingScreen

A Valheim loading-screen mod: custom images and tips on the loading screen, chosen by **situation** instead of at random.

This project is a modified fork of **Intermission 1.10.0** by redseiko
(<https://github.com/redseiko/ComfyMods/tree/main/Intermission>, upstream commit `cc8647bec282d50db0f62bb46f797d1c4bbc97d4`).
Only the Intermission plugin was taken from the ComfyMods repository; it is self-contained.

## License and attribution

- Licensed under **GPL-3.0** (see [`LICENSE`](LICENSE)), the same license as the upstream repository.
- Original work: Copyright (C) redseiko / ComfyMods. All upstream copyright notices stay in place.
- Changes are tracked in this repository's history and in [`CHANGELOG.md`](CHANGELOG.md).
- Planned reuse of code from [cjayride/CustomLoadingScreens](https://github.com/cjayride/CustomLoadingScreens_Fork),
  itself a fork of [aedenthorn/CustomLoadingScreens](https://github.com/aedenthorn/ValheimMods/tree/master/CustomLoadingScreens).
  Both repositories state that their content is released into the public domain, which is compatible with GPL-3.0.
  Any code taken from them will be marked in the source and credited here.

## Status

Version 0.1.0: Intermission 1.10.0 renamed to `LoadingScreen` (GUID `Odin_Sons.LoadingScreen`), no functional changes
yet. Everything below is the roadmap.

## Planned features

### 1. Situation-based image and text selection

A new structured config file (JSON proposed, to avoid extra dependencies) in `BepInEx/config/LoadingScreen/` links
**images** and **captions** to situations. Each situation holds an array of entries, and each entry is an image with
an optional text:

```json
{
  "teleportBiomes": {
    "Meadows":   [ { "image": "meadows/farm.jpg", "text": "Home sweet home." } ],
    "Mistlands": [ { "image": "mistlands/fog.jpg", "text": "Light your wisp." } ]
  },
  "deathCauses": {
    "Fall":    [ { "image": "death/fall.jpg", "text": "Mind the cliff." } ],
    "Frost":   [ { "image": "death/frost.jpg", "text": "Should have worn the troll armor." } ]
  },
  "general": [ { "image": "general/valheim.jpg", "text": "Skol!" } ]
}
```

#### Selection priority

When a loading screen is about to be shown, the first level that has at least one entry wins; within a level one entry
is picked at random (no immediate repeats).

1. **Teleport destination biome**, only when the loading screen is caused by a portal teleport.
   - Every biome can have its own array of images with texts.
   - The biome is resolved from the destination position, not from the biome the player is leaving.
2. **Cause of death**, only when the loading screen is caused by a respawn.
   - Every possible cause of death can have its own array of images with texts.
   - Causes to cover: damage types (blunt, slash, pierce, chop, pickaxe, fire, frost, lightning, poison, spirit)
     and environmental deaths (fall, drowning, freezing, burning, smoke, and similar); the final list is set during
     implementation from what the game exposes.
3. **General**, used for everything else and as the fallback when the levels above have no matching entries.

If no level has any entry, the mod falls back to the plain image folder, as Intermission does today.

#### Config details

- Image paths are relative to the config folder; missing files are skipped with a warning.
- The text is optional; an entry without text shows the image only (or a random tip, controlled by a setting).
- Biome and cause names are case-insensitive; unknown names are reported in the log at startup.
- A reload command to re-read the config without restarting the game.

### 2. Image formats and size

- Keep `.png` and `.jpg`.
- Document recommended resolution and quality to keep the config folder small.

### 3. Compatibility and packaging

- Done in 0.1.0: the plugin GUID, namespace and assembly are renamed, so the mod can be installed next to the original
  Intermission.
- Publish source and a Thunderstore package under GPL-3.0.

### 4. Ideas from CustomLoadingScreens (evaluate)

- Separate screens for game start, respawn and portal travel.
- Optional unlocking of image pools by progression (defeated bosses).
- Valheim tips shown on portal screens.

## Original Intermission features (kept)

- Show custom images and tips on the loading screen.
- Place `.png` or `.jpg` files in `BepInEx/config/LoadingScreen/` and one loading tip per line in `tips.txt`.
- Settings are in `BepInEx/config/Odin_Sons.LoadingScreen.cfg`:
  - `[Hud.LoadingScreen] showOnTeleport`: show the loading screen instead of the vanilla portal animation.
  - `[Hud.LoadingScreen] showImmediatelyOnDeath`: show the loading screen right when the player dies.

## Building

Requires the .NET SDK and a Valheim install with BepInEx.

```sh
dotnet build LoadingScreen.csproj -c Release
```

MSBuild properties:

- `GamePath`: the Valheim install (the folder with `valheim_Data`). Found through the Steam registry keys or the default
  Steam locations if not set.
- `BepInExPath`: the `BepInEx` folder, default `$(GamePath)/BepInEx`. Set it when BepInEx lives in a mod manager
  profile, for example `-p:BepInExPath="<profile dir>/BepInEx"`.
- `PluginOutputPath`: where the built DLL is copied, default `$(BepInExPath)/plugins`.

Game assemblies are publicized at build time by `BepInEx.AssemblyPublicizer.MSBuild`, so no pre-generated
`publicized_assemblies` folder is needed. The build also writes a Thunderstore package `LoadingScreen_v<version>.zip`
to `bin/Release`.
