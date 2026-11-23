# Migrating from Intermission

LoadingScreen started as a fork of Intermission 1.10.0. It reads the same kind of files, so moving over takes
a few minutes and needs no format changes.

## 1. Move the files

Intermission keeps its files in `BepInEx/config/Intermission/`. LoadingScreen uses
`BepInEx/config/LoadingScreen/` with the same layout:

| Intermission | LoadingScreen |
|---|---|
| `tips.txt` | `tips.txt`, unchanged format: one tip per line |
| `*.png`, `*.jpg` at the top level | `*.png`, `*.jpg` at the top level, used as plain images |

Copy everything from `config/Intermission/` into `config/LoadingScreen/`. To keep a single folder instead, set
`[1 - General] Image folder` to `config/Intermission` in `BepInEx/config/Odin_Sons.LoadingScreen.cfg` and restart
the game.

## 2. Carry over the settings

Intermission's cfg settings are not read. Use this table to set the same values in LoadingScreen. Settings in
`loadingscreen.yaml` go under the `defaults:` key; the file is created on first start with a commented example.

| Intermission setting | LoadingScreen |
|---|---|
| `_Global` `isModEnabled` | `[1 - General] Mod enabled` |
| `SceneLoader` `useLoadingImages` | `[4 - Game start screen] Use loading images` |
| `SceneLoader` `showProgressText` | `[4 - Game start screen] Show progress` |
| `SceneLoader` `centerProgressIndicator` | `spinner: { center: true }` in `defaults` |
| `SceneLoader` `progressIndicatorOffset` | `spinner: { x: 0, bottom: 200 }` in `defaults` |
| `LoadingImage.Image` `baseColor` | `tint: "#FFFFFF"` in `defaults` |
| `LoadingImage.ScaleLerp` `useScaleLerp` | On by default; `animation: none` in `defaults` turns it off |
| `LoadingImage.ScaleLerp` `lerpEndScale`, `lerpDuration` | A custom `zoom-in` preset in `animations.yaml` (see the README) |
| `LoadingTip.Text` `textPosition`, `textFontSize`, `textColor` | `tip: { x: 0, bottom: 90, size: 24, color: "#FFFFFF" }` in `defaults` |
| `LoadingScreen.Background` `backgroundColor` | `background: "#000000"` in `defaults` |
| `LoadingScreen.PanelSeparator` `showPanelSeparator`, `panelSeparatorPosition` | `separator: { show: true, x: 0, bottom: 150 }` in `defaults` |
| `Hud.LoadingScreen` `showImmediatelyOnDeath` | `[2 - When to show] Loading screen right after death` |
| `Hud.LoadingScreen` `showOnTeleport` | `[2 - When to show] Loading screen on portal teleport` |

For example, an Intermission setup with a red tint, a larger tip and no zoom becomes:

```yaml
defaults:
  tint: "#FFC0C0"
  tip: { size: 30 }
  animation: none
```

## 3. Remove Intermission

Both mods change the same loading screens. Remove Intermission after the move so only one of them draws the
screen.
