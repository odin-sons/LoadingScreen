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

### 1. Situation-based selection

A YAML config file `BepInEx/config/LoadingScreen/loadingscreen.yaml` links **entries** (images with optional captions)
to situations. YAML is parsed with YamlDotNet from the `ValheimModding-YamlDotNet` package, which becomes a
dependency.

#### Selection priority

When a loading screen is about to be shown, the first level that has at least one entry wins; within a level one entry
is picked at random (no immediate repeats).

1. **Teleport destination biome**, only when the loading screen is caused by a portal teleport.
   - Every biome can have its own array of entries.
   - The biome is resolved from the destination position, not from the biome the player is leaving.
2. **Cause of death**, only when the loading screen is caused by a respawn.
   - Every possible cause of death can have its own array of entries.
   - Causes to cover: damage types (blunt, slash, pierce, chop, pickaxe, fire, frost, lightning, poison, spirit)
     and environmental deaths (fall, drowning, freezing, burning, smoke, and similar); the final list is set during
     implementation from what the game exposes.
3. **General**, used for everything else and as the fallback when the levels above have no matching entries.

If no level has any entry, the mod falls back to the plain image folder, as Intermission does today.

### 2. Layered layouts

A loading screen is a stack of **layers**, drawn in list order (first is at the back). There is no fixed "background"
and "foreground": a layout can have as many layers as needed, and each layer is sized and positioned on its own.

Layer kinds:

- **image**: a fixed image, a pool of images (one picked at random per screen), or `source: entry`, which takes the
  image from the entry chosen by the situation selector.
- **color**: a solid fill (the backdrop under images that do not cover the screen).
- **text** (later): the caption or tip, with its own position, so the text is not tied to the bottom bar.

Placement options for image layers (the names are not final):

| Option | Values |
|---|---|
| `fit` | `cover` (fill, crop), `contain` (fit inside, letterbox), `stretch` (fill, ignore aspect), `none` (native size), or an explicit `width` and/or `height` in `%` of the screen or `px`; a missing axis keeps the aspect ratio |
| `position` | an anchor (`center`, `left`, `top-right`, ...) plus `x` / `y` offsets in `%` or `px` |
| `opacity` | 0 to 1 |
| `fade` | gradient edges drawn over the image: `sides` (any of `left`, `right`, `top`, `bottom`), `color`, `size` in `%` or `px`, and a curve; the gradient goes from the color to transparent towards the image center |
| `animate` | the scale animation Intermission already has, per layer |

Placement is resolved from the most specific level to the least specific one:
**entry image -> layer -> layout -> global `defaults`**. An entry can override the placement of its own image, for
example to show one picture on the left and the next one on the right.

#### Presets

Layouts are named, and entries or whole situations pick a layout by name. Two presets ship with the mod:

- `backdrop-art`: one fixed stretched backdrop image, then the entry image in a fixed box that entries move left or
  right.
- `logo-over-backdrops`: the entry image as a stretched backdrop (many different ones), then one fixed content image
  on top, such as a server logo.

#### Config sketch

```yaml
defaults:
  fit: cover
  position: { anchor: center }

layouts:
  backdrop-art:
    layers:
      - color: "#000000"
      - image: common/parchment.webp
        fit: stretch
      - id: art
        source: entry
        fit: { width: 45%, height: 80% }
        position: { anchor: left, x: 5% }
        fade: { sides: [left, right], color: "#000000", size: 10% }

  logo-over-backdrops:
    layers:
      - id: backdrop
        source: entry
        fit: cover
        fade: { sides: [bottom], color: "#000000", size: 25% }
      - image: common/server-logo.png
        fit: { width: 30% }
        position: { anchor: top, y: 8% }

situations:
  general:
    layout: logo-over-backdrops
    entries:
      - image: general/valheim.avif
        text: Skol!
  teleport:
    Mistlands:
      - image: mistlands/fog.webp
        text: Light your wisp.
        layout: backdrop-art
        place: { position: { anchor: right, x: -5% } }
  death:
    Fall:
      - image: death/fall.jxl
        text: Mind the cliff.
```

#### Config rules

- Image paths are relative to the config folder; missing files are skipped with a warning.
- The text is optional; an entry without text shows the image only (or a random tip, controlled by a setting).
- Biome, cause, layout and layer names are case-insensitive; unknown names are reported in the log at startup.
- A reload command re-reads the config without restarting the game.

### 3. Image formats

Unity can decode only PNG and JPEG (`ImageConversion.LoadImage`). Newer formats need a decoder shipped with the mod.
All three candidates are royalty-free, with reference decoders under permissive licenses compatible with GPL-3.0:

| Format | Decoder | License | Typical size vs. JPEG |
|---|---|---|---|
| WebP | libwebp | BSD-3-Clause plus Google's royalty-free patent grant | about 25-35 % smaller |
| AVIF | libavif + dav1d (AV1) | BSD-2-Clause; AV1 is royalty-free under the AOMedia patent license | about 50 % smaller |
| JPEG XL | libjxl | BSD-3-Clause plus a royalty-free patent grant | about 50 % smaller; can recompress existing JPEGs losslessly (about 20 % smaller) |

A patent pool (Sisvel) claims patents on AV1 outside the AOMedia license; no fees are known to be charged for
software decoders, but this is a reading of public information, not legal advice.

Plan:

- Load decoders as native libraries through P/Invoke, one build per platform (Windows x64, Linux x64; macOS
  optional). A format whose decoder is missing is skipped with a warning; PNG and JPEG always work.
- Start with WebP (smallest decoder, widest tool support), then AVIF and JPEG XL.
- Decode on a worker thread and upload with `Texture2D.LoadRawTextureData`, so loading a large image does not stall
  the frame that shows the loading screen.
- Unload textures that are no longer shown to keep VRAM low.
- Document recommended resolution (1920x1080 or 2560x1440) and quality settings, and give example commands for the
  standard encoders (`cwebp`, `avifenc`, `cjxl`).

### 4. Smaller packages: images served on demand

Goal: the Thunderstore package or modpack ships the mod and a small config only; the images live on the server and
reach a client just before they are needed.

- The dedicated server runs the same plugin and holds the image folder. On connect it sends a manifest: path, size,
  format and SHA-256 hash of every image, plus the YAML config.
- The client keeps a disk cache keyed by hash (`BepInEx/cache/LoadingScreen/`), so an image is downloaded once and
  reused across sessions and across servers that share it.
- Prefetch, in the background and rate limited so it does not affect gameplay:
  - right after login: the `general` and `death` entries (small sets, needed without warning);
  - teleport entries for a portal's destination biome when the player comes near that portal (the target position
    is known from the connected portal), and the remaining teleport entries slowly over time.
- An image that is not in the cache yet is never waited for: the selector skips it and falls back to the next
  level, so a slow download never delays the loading screen. The game start screen can only use cached and local
  images, because no server connection exists at that point.
- Transport, to be decided: chunked transfer over the game's own RPC (no extra hosting), or plain HTTP(S) from a base
  URL set in the server config (a CDN or a release page; less load on the game server).
- Compression: images in WebP, AVIF, JPEG XL or JPEG are already compressed, so zlib saves almost nothing on them.
  zlib is used for the manifest, the config and PNG files. The real size win comes from the formats in section 3
  and from a resolution cap.
- Safety: the data comes from a server, so paths are validated (no `..`, no absolute paths), file sizes and counts
  are capped, hashes are verified, and decode errors are caught and logged.
- Without the server part (a server without the plugin, or single player) the mod works as before from local files.

### 5. Compatibility and packaging

- Done in 0.1.0: the plugin GUID, namespace and assembly are renamed, so the mod can be installed next to the original
  Intermission.
- Publish source and a Thunderstore package under GPL-3.0.

### 6. Ideas from CustomLoadingScreens (evaluate)

- Separate screens for game start, respawn and portal travel.
- Optional unlocking of image pools by progression (defeated bosses).
- Valheim tips shown on portal screens.

### Implementation order

1. Data model, YAML loading and validation (situations, entries, layouts, layers, defaults).
2. Situation selector (pure code, unit tested).
3. Situation capture: portal destination biome, cause of death.
4. Layer renderer: placement resolution, fit and position, color layers, gradient edges; the two presets.
5. Wire into the in-game loading screen, the game start screen and the main menu; keep tips and the plain image
   folder as the final fallback.
6. Reload command.
7. WebP decoder, then AVIF and JPEG XL; background decoding.
8. Server-provided images: manifest, cache, prefetch, transport.
9. In-game tests, README and CHANGELOG.

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
