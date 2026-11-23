# LoadingScreen

A Valheim loading-screen mod: custom images and tips on the loading screen, chosen by **situation** instead of at random.

This project is a modified fork of **Intermission 1.10.0** by redseiko
(<https://github.com/redseiko/ComfyMods/tree/main/Intermission>, upstream commit `cc8647bec282d50db0f62bb46f797d1c4bbc97d4`).
Only the Intermission plugin was taken from the ComfyMods repository; it is self-contained.

## License and attribution

- Licensed under **GPL-3.0** (see [`LICENSE`](LICENSE)), the same license as the upstream repository.
- Original work: Copyright (C) redseiko / ComfyMods. All upstream copyright notices stay in place.
- Changes are tracked in this repository's history and in [`CHANGELOG.md`](CHANGELOG.md).
- The optional [Conditional Config Sync adapter](https://github.com/shudnal/ConditionalConfigSync/tree/430a064d5c815c8b90066442ba1b3a40647efa02/ConditionalConfigSync.API)
  is included as source under its [upstream license](src/Vendor/ConditionalConfigSync/LICENSE).
  [ServerSync v1.20](https://github.com/blaxxun-boop/ServerSync/releases/tag/v1.20)
  is merged into the plugin DLL; its license is in [`lib/ServerSync/LICENSE.txt`](lib/ServerSync/LICENSE.txt).
- Planned reuse of code from [cjayride/CustomLoadingScreens](https://github.com/cjayride/CustomLoadingScreens_Fork),
  itself a fork of [aedenthorn/CustomLoadingScreens](https://github.com/aedenthorn/ValheimMods/tree/master/CustomLoadingScreens).
  Both repositories state that their content is released into the public domain, which is compatible with GPL-3.0.
  Any code taken from them will be marked in the source and credited here.

## Status

Version 0.2.0. All planned features below are implemented and covered by unit tests where the code does not depend
on Unity, but **the mod has not been tested in the game yet**. See the
[issues](https://github.com/odin-sons/LoadingScreen/issues) for the test checklist and the remaining work.

## Features

- **Situational screens.** A portal teleport shows an image chosen for the destination biome; a respawn first tries
  the cause of death, then the biome where the player died; everything else uses the general list. Within a list,
  one entry is picked at random without immediate repeats.
- **Layered layouts.** A screen is a stack of layers (solid colors, fixed images, random image pools, spinners, progress, the chosen
  entry's image, the caption), each sized and positioned on its own, with gradient edges and enter, idle and exit animations.
  Two presets are included in the example config.
- **Captions.** An entry can carry plain or translated text; without it, a tip from the selected language's tip file is shown on screens without a layout or through a layout's `text: caption` layer.
- **Loading sounds.** Loop music for a layout, list or entry; play an entry's voiced tip once when its text appears.
- **Conditions.** An entry can be limited to players who, for example, have never held a Wisplight or have not
  defeated a boss yet, so a tip only reaches the players who need it.
- **Server images.** A server that runs the mod can share its config and images; clients download the images in the
  background and use the server's screens while connected.
- **Reload in game** with the `loadingscreen reload` console command.
- Everything Intermission does still works and stays the fallback.

## Installation

Requires `denikson-BepInExPack_Valheim` and `ValheimModding-YamlDotNet`. Put `LoadingScreen.dll` into
`BepInEx/plugins`. Coming from Intermission? See [Migrating from Intermission](docs/migrating-from-intermission.md).

LoadingScreen does not load while Intermission or CustomLoadingScreens is installed: they patch the same loading
screens and break each other. BepInEx then writes `Could not load [LoadingScreen ...] because it is incompatible
with: ...` to `BepInEx/LogOutput.log`. Remove the other mod to use LoadingScreen. ComfyLoadingScreens installs
Intermission as its dependency, so it counts as Intermission.

On first start the mod creates `BepInEx/config/LoadingScreen/loadingscreen.yaml` with a commented example. While
everything in it stays commented out, the mod behaves like Intermission.

## Configuration

By default, these files live in `BepInEx/config/LoadingScreen/`:

| File | Purpose |
|---|---|
| `loadingscreen.yaml` | When to show, layouts, placement and appearance (below) |
| `translations.yaml` | Optional overrides for the mod's own UI text |
| `tips.txt` | One tip per line; fallback when a language-specific file is absent |
| `tips.<code>.txt` | Optional translated tips, for example `tips.ru.txt` |
| `*.png`, `*.jpg` at the top level | Plain images, used when no situational entry applies |
| `.ogg`, `.wav` in the config folder | Optional music and voiced tips referenced from `loadingscreen.yaml` |

Set `[1 - General] Image folder` in `BepInEx/config/Odin_Sons.LoadingScreen.cfg` to use another folder. The
default is `config/LoadingScreen`. Relative paths start at the BepInEx folder, so `plugins/MyPack/screens` means
`BepInEx/plugins/MyPack/screens`; absolute paths also work. Restart the game after changing this setting. The chosen
folder holds `loadingscreen.yaml`, tip files, images and audio clips.

Behavior switches are in `BepInEx/config/Odin_Sons.LoadingScreen.cfg`; appearance is in the YAML `defaults` section.

`[5 - Sound] Play loading screen sounds` turns configured music and voices on or off (on by default). `Music volume
percent` and `Voice volume percent` each range from 0 to 100 and default to 100. Music also follows Valheim's master
and music sliders; voices follow its master and effects sliders. Music follows the loading screen's fade and stops
as soon as the screen closes. `Mute game sounds on loading screens` (`None` by default) fades out the game's
`Effects` (such as the portal hum), `Music`, or both (`Effects, Music`) while a loading screen is visible, and restores them when it closes. Audio paths are relative to the config folder, with `.ogg` and `.wav` supported. Each
clip is limited to 8 MiB.

The game's Audio settings show `Loading screen music` and `Loading screen voice` sliders below the game's own controls.
They edit `Music volume percent` and `Voice volume percent`: the sliders start from the cfg values, `OK` saves them to
the cfg, and `Back` discards the change. Changes made in the cfg while the menu is open show on the sliders.

With other audio mods:

- [NotSoLoud](https://thunderstore.io/c/valheim/p/OdinPlus/NotSoLoud/) routes audio sources without a mixer group into
  the game's effects group and caps their volume. The mod keeps its music and voice sources out of that group and
  sets their volume every frame, so they are neither scaled twice by the effects slider nor faded by
  `Mute game sounds on loading screens`.
- [ValheimAudioControl](https://thunderstore.io/c/valheim/p/XineladaLendaria/ValheimAudioControl/) rebuilds the Audio
  tab, so the two sliders are not added while it is installed; use the cfg or a configuration manager instead.
  ValheimAudioControl keeps the game's effects volume at 100%, so voices then follow the master volume only. The mod
  sets its music and voice volume every frame, so ValheimAudioControl's category and per-object sliders are not
  meant for loading screen audio; leave any entry for it at 100% or below.

`[1 - General] Text language` defaults to `Game`, following Valheim's selected language. Set an ISO language code,
such as `ru` or `pt-BR`, to force a language for tips and entry text. Unknown codes use the game language. The mod
looks for `tips.<code>.txt` first, then `tips.txt`. Each file has one tip per line; a missing plain file is created
empty. Language changes take effect on the next tip or screen selection. The game's `Chinese` and `Chinese_Trad`
use `zh-CN` and `zh-TW`; `Portuguese_European` and `Portuguese_Brazilian` use `pt` and `pt-BR`.

The mod's own UI text follows Valheim's selected language, independently of `Text language`. Built-in text covers all
36 languages in the game's localization list; an unknown language falls back to English. To replace individual strings,
create `translations.yaml` beside `loadingscreen.yaml`. Use a game language name or the corresponding language code:

```yaml
Russian:
  continuePrompt: "Нажмите любую клавишу для продолжения"
  secondsUnit: "с"
German:
  gameStartProgress: "<b>{percent}%</b>\n<size=-4>({seconds} {unit})</size>"
```

The keys are `continuePrompt`, `gameStartProgress`, `secondsUnit`, `connectingStage`, `passwordStage`,
`receivingWorldStage`, `configProblems`, `musicVolumeSlider`, and `voiceVolumeSlider`. The progress template accepts `{percent}`,
`{seconds}`, and `{unit}`; percent and elapsed seconds are integers. The game-start percentage now follows the
real scene load operation. Missing or invalid entries keep their built-in
translations. `loadingscreen reload` re-reads the file. Translations for less common languages welcome review from
native speakers.

Set `[3 - Timing] Change image every N seconds` to a positive number to change images while the HUD or game start
loading screen stays visible. The default `0` turns the slideshow off. When a situational entry is showing, each
change picks another usable entry from that same list and follows its `order` setting. The entry animation starts again
on each change.

Set `[3 - Timing] Change tip every N seconds` to a positive number to show another line from `tips.txt` while a HUD
or main-menu loading screen stays visible. The default `0` turns tip rotation off. An entry's own text stays fixed;
entries without text rotate tips when `[2 - When to show] Random tip when an entry has no text` is on. Tip rotation also
works with a layout's `text: caption` layer. The game's arrow keys still advance tips manually. With more than one
tip, the next pick differs from the previous one.

Set `[2 - When to show] Loading screen while sleeping` to `true` to show an image and tip during sleep (default: off).
The game's sleep text and spinner stay visible above plain images. A sleep-specific layout hides them and draws
only its layers. Image slideshows and tip rotation also apply while sleeping.

Set `[3 - Timing] Hold loading screen until a key is pressed` to `true` to keep the HUD loading screen and tip
visible after loading finishes (default: off). A separate "Press any key to continue" line appears; any key or mouse
click dismisses the key hold. `[3 - Timing] Invulnerable while held` defaults to `true` and protects the local player
from damage while a HUD screen is held. If turned off, the held screen closes immediately when PvP is enabled or when
a nearby hostile monster notices the player. Deer, tamed creatures, and non-enemies do not trigger this close.
This setting only matters while a hold is active.

Set `[3 - Timing] Wait for the voice to finish` to `true` to keep the HUD screen
visible while its entry voice is loading or playing after loading finishes (default: off). It releases when the voice
ends or fails to load; no voice or disabled sounds do not cause a hold. A key press does not skip the voice, and when
both settings are on, the screen closes after both the voice and key hold are done. Set `Wait for the exit animation
to finish` to play the layer exit phases before closing (default: off); otherwise the screen closes immediately and
the exit phases are skipped. This also applies when sleep or the game start scene loader ends. `[3 - Timing] Maximum hold time in
seconds` closes any hold automatically after 30 seconds by default (allowed: 1 to 300). Holds do not run on
dedicated servers or during cinematics. On screens without a layout, style the key prompt with
`defaults.continuePrompt` in `loadingscreen.yaml`. A layout needs a `text: prompt` layer to show it.

### Selection

| Loading screen | First choice | Then |
|---|---|---|
| Portal teleport | `when.biome.<destination biome>` | base biome list for EWD custom biomes, then `when.general` and plain images |
| Respawn | `when.enemy.<attacker prefab>`, then `when.death.<dominant damage type>`, then `when.death.<hit type>` | `when.biome.<death biome>`, base biome list for EWD custom biomes, `when.general`, then plain images |
| Sleep (when enabled) | `when.sleep` | `when.general`, then the plain images |
| Anything else, main menu, game start | `when.general` | the plain images |

- Biomes: `Meadows`, `BlackForest`, `Swamp`, `Mountain`, `Plains`, `Mistlands`, `AshLands`, `DeepNorth`, `Ocean`.
  Teleports use the destination position; deaths use the player's position when death occurs. Teleport screens need
  `[2 - When to show] Loading screen on portal teleport` (on by default); without it the game shows its own portal animation.
- Expand World Data custom biomes use the `biome:` identifier from `expand_biomes.yaml` in
  `when.biome` and `knownBiome`, not the display name. Names are case-insensitive. When a custom biome
  has no usable entry, its base vanilla biome list is tried before `general`. Unknown biome names are
  logged once after EWD biome data becomes available following world load; without EWD, they are logged when the
  config is loaded.
- Causes are the game's own hit types: `Undefined`, `EnemyHit`, `PlayerHit`, `Fall`, `Drowning`, `Burning`,
  `Freezing`, `Poisoned`, `Water`, `Smoke`, `EdgeOfWorld`, `Impact`, `Cart`, `Tree`, `Self`, `Structural`,
  `Turret`, `Boat`, `Stalagtite`, `Catapult`, `CinderFire`, `AshlandsOcean`, `AshlandsLava`, `Incinerator`,
  `DrawBridge`.
- For `EnemyHit`, `PlayerHit`, `Turret`, `Catapult` and `Undefined`, the dominant damage type of the killing hit is
  tried first: `Blunt`, `Slash`, `Pierce`, `Chop`, `Pickaxe`, `Fire`, `Frost`, `Lightning`, `Poison`, `Spirit`.
  Example: a frost drake kill uses `death.Frost` if it exists, then `death.EnemyHit`, then the death biome and `general`.
- A kill by a creature tries `when.enemy.<prefab name>` before the damage and hit type lists, for example `enemy.Troll`
  or `enemy.Hatchling` (a drake; `Dragon` is Moder). Prefab names are free-form, including modded creatures. Each list is tried in turn until one has a
  usable entry, so `death.EnemyHit` still catches every enemy kill whose damage type has no list.
- Within a list an entry is picked at random, never the same one twice in a row. `order: sequential` at the top of
  the file, or on one list next to `entries`, shows the usable entries in config order instead, wrapping around.
- YAML anchors and aliases are not supported, and nesting is limited to 32 levels.
- Names are case-insensitive. Missing files and invalid values are reported in `BepInEx/LogOutput.log` with line
  numbers; unknown biome names are checked at runtime. The rest of the config still loads.

### Entries

```yaml
when:
  general:                     # a list of entries, or { layout, entries }
    layout: logo-over-backdrops
    entries:
      - image: general/valheim.jpg
        text: { en: Skol!, ru: Скол! }
        sound: music/forest.ogg
        voice: voice/welcome.ogg
      - general/another.jpg    # an image without text
  sleep:
    - image: sleep/night.jpg
      text: Rest until morning.
  biome:
    Mistlands:
      - image: mistlands/fog.jpg
        text: Light your wisp.
        layout: backdrop-art   # overrides the list's layout
        place: { position: { anchor: right, x: -5% } }   # placement for this image only
  death:
    Fall: [ death/fall.jpg ]
  enemy:
    Troll: [ death/troll.jpg ]
```

Image paths are relative to the config folder; `.png`, `.jpg` and `.jpeg` are supported, up to 8192 px per side
and 16.7 megapixels (for example 3840x2160 or 5120x2880; larger files are skipped with a log message). An entry without a layout
uses a single layer that shows its image with the `defaults` placement (`contain`, centered, unless changed).

An entry may have only a `text`; it is shown over a random plain image from the config folder, or over the game's
own image if there is none.

Add `sound: music/forest.ogg` to a layout, entry list or entry. The entry's sound takes priority over the list's,
then the layout's; it loops while that screen is visible. Add `voice: voice/welcome.wav` to an entry with `text` to
play it once when the entry's text appears. A voice is not played for a random tip substituted for missing entry text.

Entry `text` can be a plain string or a mapping of ISO language codes to strings, such as
`text: { en: Welcome, ru: Добро пожаловать }`. For a missing translation, `en` is used, then the first available
translation. A plain string is shown in every language. This also applies to text-only entries and layout captions.

### Conditions

`when` limits an entry to players for whom every condition holds:

```yaml
when:
  biome:
    Mistlands:
      - text: The mist is thick. Craft a Wisplight to see further.
        when:
          notKnownItem: Demister        # has never picked up a Wisplight
          notGlobalKey: defeated_queen  # the Queen is still alive in this world
      - mistlands/fog.jpg
```

| Condition | Holds when | Value |
|---|---|---|
| `knownItem` | the player has ever picked up the item | item prefab name (`Demister`) or `$item_` token |
| `hasItem` | the item is in the inventory now | same as above |
| `knownRecipe` | the player knows the recipe for the item or build piece | same as above, or a piece token (`$ship_karve`) |
| `knownBiome` | the player has discovered the biome | vanilla name or EWD `biome:` identifier |
| `globalKey` | the world has the global key | key name (`defeated_bonemass`, `defeated_queen`, ...) |
| `playerKey` | the player has the unique key | key name |

Prefix a condition with `not` to invert it (`notKnownItem`, `notGlobalKey`, ...). A value can be one name or a list;
every name must match. Player conditions need a loaded character, so entries with them are skipped on the main menu
and on the first loading screen of a session. An entry with an unknown condition is ignored with a log message.
Steam or Xbox achievements are not available to mods on every platform; use the global key or item the achievement
is about instead.

#### Condition recipes

Boss progress. The global key is set for the whole world when the boss dies:

| Boss | Summoned or reached with | Drops | Global key |
|---|---|---|---|
| Eikthyr | `TrophyDeer` | `HardAntler`, `TrophyEikthyr` | `defeated_eikthyr` |
| The Elder | `AncientSeed` | `CryptKey`, `TrophyTheElder` | `defeated_gdking` |
| Bonemass | `WitheredBone` | `Wishbone`, `TrophyBonemass` | `defeated_bonemass` |
| Moder | `DragonEgg` | `DragonTear`, `TrophyDragonQueen` | `defeated_dragon` |
| Yagluth | `GoblinTotem` | `YagluthDrop`, `TrophyGoblinKing` | `defeated_goblinking` |
| The Queen | `DvergrKey` (from `DvergrKeyFragment`) | `QueenDrop`, `TrophySeekerQueen` | `defeated_queen` |
| Fader | `Bell` (from `BellFragment`) | `FaderDrop`, `TrophyFader` | `defeated_fader` |

Other world keys: `KilledTroll`, `killed_surtling`, `KilledBat`.

Materials that mark progress through the biomes:

| Biome | Item prefab names |
|---|---|
| Meadows | `Wood`, `Stone`, `Flint`, `Resin`, `LeatherScraps`, `DeerHide`, `Feathers` |
| Black Forest | `CopperOre`, `Copper`, `TinOre`, `Tin`, `Bronze`, `FineWood`, `RoundLog`, `SurtlingCore`, `TrollHide`, `Thistle` |
| Swamp | `IronScrap`, `Iron`, `Guck`, `Bloodbag`, `Ooze`, `Chain`, `Root`, `ElderBark`, `Entrails` |
| Mountain | `SilverOre`, `Silver`, `WolfPelt`, `WolfFang`, `FreezeGland`, `Obsidian`, `Thunderstone` |
| Plains | `BlackMetalScrap`, `BlackMetal`, `Barley`, `BarleyFlour`, `Flax`, `LoxPelt`, `Needle`, `Tar` |
| Mistlands | `BlackMarble`, `Sap`, `Eitr`, `BlackCore`, `Softtissue`, `Carapace`, `Mandible`, `Wisp`, `YggdrasilWood`, `RoyalJelly`, `MushroomJotunPuffs`, `MushroomMagecap`, `JuteRed`, `ScaleHide`, `MechanicalSpring` |
| Ashlands | `FlametalOreNew`, `FlametalNew`, `Blackwood`, `AskHide`, `CharredBone`, `Grausten`, `MoltenCore`, `Bilebag`, `CelestialFeather`, `MorgenHeart`, `CeramicPlate` |
| Deep North | `GoldOre`, `Gold`, `Frostwood`, `FrostCore`, `Ice`, `FrozenFuel`, `MooseHide` |
| Ocean | `SerpentScale`, `Chitin`, `SerpentMeat` |

Key tools and gear: `Hammer`, `Hoe`, `Cultivator`, `PickaxeAntler`, `PickaxeBronze`, `PickaxeIron`,
`PickaxeBlackMetal`, `Demister` (Wisplight), `Lantern`, `MeadFrostResist`, `MeadPoisonResist`, `CapeWolf`,
`CapeLox`, `FishingRod`, `Scythe`. Any other item works by its prefab name; the
[Jotunn item list](https://valheim-modding.github.io/Jotunn/data/objects/item-list.html) has all of them.

Haldor, the Black Forest trader:

| Goods | Prefab or key | Sold after |
|---|---|---|
| Megingjord | `BeltStrength` | always |
| Dverger circlet | `HelmetDverger` | always |
| Barrel hoops | `BarrelRings` | always |
| Fishing rod, bait | `FishingRod`, `FishingBait` | always |
| Thunder stone | `Thunderstone` | `defeated_gdking` |
| Ymir flesh | `YmirRemains` | `defeated_gdking` |
| Egg | `ChickenEgg` | `defeated_goblinking` |
| Wider Pockets, +1 inventory row | player key `invrows 5` | `defeated_dragon` |
| Deeper Pockets, +1 inventory row | player key `invrows 6` | `defeated_queen` |

The pockets are not items: the game stores the number of inventory rows as the player key `invrows N`, starting at
`invrows 4`. `playerKey: invrows 4` therefore means the player has not bought Wider Pockets yet.

The Bog Witch, the Swamp trader:

| Goods | Prefab | Sold after |
|---|---|---|
| Candle wick | `CandleWick` | always |
| Cured squirrel hamstring | `CuredSquirrelHamstring` | always |
| Fresh seaweed | `FreshSeaweed` | always |
| Ivy seeds | `VineGreenSeeds` | always |
| Love potion | `MeadTrollPheromones` | always |
| Powdered dragon eggshells | `PowderedDragonEgg` | always |
| Pungent pebbles | `PungentPebbles` | always |
| Serving tray | `Feaster` | always |
| Woodland herb blend | `SpiceForests` | `defeated_gdking` |
| Fragrant bundle | `FragrantBundle` | `defeated_dragon` |
| Mountain peak pepper powder | `SpiceMountains` | `defeated_dragon` |
| Toadstool | `MushroomBzerker` | `defeated_dragon` |
| Scythe handle | `ScytheHandle` | `defeated_dragon` |
| Grasslands herbalist harvest | `SpicePlains` | `defeated_goblinking` |
| Herbs of the hidden hills | `SpiceMistlands` | `defeated_queen` |
| Fiery spice powder | `SpiceAshlands` | `defeated_fader` |
| Seafarer's herbs | `SpiceOceans` | a killed serpent |

Build pieces are known by their token, so use `knownRecipe` with it. Ships: `$ship_raft`, `$ship_karve`,
`$ship_longship`, `$ship_longship_ashlands` (Drakkar). Stations: `$piece_workbench`, `$piece_forge`, `$piece_smelter`,
`$piece_charcoalkiln`, `$piece_stonecutter`, `$piece_fermenter`, `$piece_cauldron`, `$piece_artisanstation`,
`$piece_spinningwheel`, `$piece_windmill`, `$piece_blastfurnace`, `$piece_blackforge`, `$piece_eitrrefinery`,
`$piece_sapcollector`, `$piece_preptable`, `$piece_meadcauldron`, `$piece_portal`.

A tip for the next boss, shown on any loading screen while that boss is the next one:

```yaml
when:
  general:
    - text: Two deer trophies on the mystical altar call Eikthyr, the first of the Forsaken.
      when: { notGlobalKey: defeated_eikthyr }
    - text: Ancient seeds from the greydwarf nests summon the Elder in the Black Forest.
      when: { globalKey: defeated_eikthyr, notGlobalKey: defeated_gdking }
    - text: The Swamp key from the Elder opens the sunken crypts.
      when: { globalKey: defeated_gdking, notKnownItem: CryptKey }
    - text: Withered bones from the sunken crypts summon Bonemass.
      when: { globalKey: defeated_gdking, notGlobalKey: defeated_bonemass }
    - text: Dragon eggs from the mountain peaks summon Moder.
      when: { globalKey: defeated_bonemass, notGlobalKey: defeated_dragon }
    - text: Fuling totems from the Plains villages summon Yagluth.
      when: { globalKey: defeated_dragon, notGlobalKey: defeated_goblinking }
    - text: Sealbreaker fragments from the infested mines open the Queen's lair.
      when: { globalKey: defeated_goblinking, notGlobalKey: defeated_queen }
    - text: Bell fragments in the Ashlands summon Fader.
      when: { globalKey: defeated_queen, notGlobalKey: defeated_fader }
```

What to do after each boss, until the player has done it:

```yaml
when:
  general:
    - text: Eikthyr's hard antlers make a pickaxe. Copper and tin wait in the Black Forest.
      when: { globalKey: defeated_eikthyr, notKnownItem: PickaxeAntler }
    - text: Copper and tin smelt into bronze. A smelter needs surtling cores from the burial chambers.
      when: { knownItem: PickaxeAntler, notKnownItem: Bronze }
    - text: Wood, leather scraps and resin make a raft. Slow, but it floats.
      when: { notKnownRecipe: $ship_raft }
    - text: Bronze nails and fine wood build a karve, a real ship for the open sea.
      when: { knownItem: Bronze, notKnownRecipe: $ship_karve }
    - text: Iron scrap lies in the sunken crypts. Bring a bronze pickaxe and the Elder's key.
      when: { globalKey: defeated_gdking, notKnownItem: IronScrap }
    - text: Iron nails and ancient bark build a longship that carries much more cargo.
      when: { knownItem: Iron, notKnownRecipe: $ship_longship }
    - text: An iron pickaxe breaks silver ore. The Wishbone shows where it is buried.
      when: { globalKey: defeated_bonemass, notKnownItem: PickaxeIron }
    - text: Haldor now sells Wider Pockets, one more row in your inventory.
      when: { globalKey: defeated_dragon, playerKey: invrows 4 }
    - text: The Bog Witch now sells a scythe handle. A scythe cuts crops in a wide arc.
      when: { globalKey: defeated_dragon, notKnownItem: Scythe }
    - text: Black metal makes the strongest pickaxe yet.
      when: { knownItem: BlackMetal, notKnownItem: PickaxeBlackMetal }
    - text: Haldor now sells Deeper Pockets, one more row in your inventory.
      when: { globalKey: defeated_queen, playerKey: invrows 5 }
    - text: Take a Drakkar into the Ashlands seas.
      when: { globalKey: defeated_queen, notKnownRecipe: $ship_longship_ashlands }
```

Biome tips for every biome, shown until the player has found the item the tip is about:

```yaml
when:
  biome:
    Meadows:
      - text: Wood and stone make a hammer. Build a workbench under a roof.
        when: { notKnownRecipe: $piece_workbench }
      - text: Flint lies along the shores. It makes better axes and spears.
        when: { notKnownItem: Flint }
    BlackForest:
      - text: Eikthyr's antlers make a pickaxe; copper and tin are waiting.
        when: { notKnownItem: PickaxeAntler }
      - text: Burial chambers hold surtling cores, the heart of a smelter.
        when: { notKnownItem: SurtlingCore }
      - text: Greydwarf nests drop ancient seeds that summon the Elder.
        when: { notGlobalKey: defeated_gdking, notKnownItem: AncientSeed }
      - text: Trolls drop hides for light, quiet armor.
        when: { notKnownItem: TrollHide }
    Swamp:
      - text: Bring poison resistance mead before the leeches find you.
        when: { notKnownItem: MeadPoisonResist }
      - text: The sunken crypts hide iron scrap behind the Elder's key.
        when: { globalKey: defeated_gdking, notKnownItem: IronScrap }
      - text: Ancient trees give ancient bark, but only to a bronze axe or better.
        when: { notKnownItem: ElderBark }
      - text: Guck hangs from the tall swamp trees.
        when: { notKnownItem: Guck }
    Mountain:
      - text: Without frost resistance the mountain freezes you to death.
        when: { notKnownItem: [MeadFrostResist, CapeWolf] }
      - text: The Wishbone hums near buried silver.
        when: { globalKey: defeated_bonemass, notKnownItem: SilverOre }
      - text: Wolves drop pelts for warm armor.
        when: { notKnownItem: WolfPelt }
    Plains:
      - text: Fulings in the Plains villages carry black metal scrap.
        when: { notKnownItem: BlackMetalScrap }
      - text: Barley and flax grow wild in the fuling villages and only in the Plains.
        when: { notKnownItem: [Barley, Flax] }
      - text: Tar pits give tar for black metal work and roofs.
        when: { notKnownItem: Tar }
    Ocean:
      - text: Serpent scales make one of the best shields of the early seas.
        when: { notKnownItem: SerpentScale }
      - text: Leviathans sink when struck. Mine chitin from their backs while you can.
        when: { notKnownItem: Chitin }
    Mistlands:
      - text: The mist is thick. Craft a Wisplight to see further.
        when: { notKnownItem: Demister }
      - text: A sap extractor on the giant roots collects sap.
        when: { notKnownItem: Sap }
      - text: Seekers drop carapace for strong armor.
        when: { notKnownItem: Carapace }
    AshLands:
      - text: Flametal ore is found in the lava fields.
        when: { notKnownItem: FlametalOreNew }
      - text: Fire resistance barley wine keeps the heat bearable.
        when: { notKnownItem: BarleyWine }
    DeepNorth:
      - text: The north freezes everything. Bring frost resistance.
        when: { notKnownItem: [MeadFrostResist, CapeWolf, CapeDeepNorth] }
      - text: Moose hide makes a warm cape for the north.
        when: { notKnownItem: MooseHide }
      - text: Bloodgold is the metal of the north.
        when: { notKnownItem: Gold }
  enemy:
    Hatchling:
      - text: Drakes spit frost from above. Frost resistance and a bow help.
    Troll:
      - text: Trolls hit hard but slowly. Dodge, then strike from behind.
    Deathsquito:
      - text: Deathsquitos die to one arrow. Wear armor in the Plains.
  death:
    Freezing:
      - text: Frost resistance mead or a wolf cape keeps the cold away.
        when: { notKnownItem: [MeadFrostResist, CapeWolf] }
      - text: A campfire and a roof warm you up and remove the Wet effect.
    Frost:
      - text: Drakes and frost spells hit hard. Frost resistance mead cuts the damage.
        when: { notKnownItem: MeadFrostResist }
    Poisoned:
      - text: Poison resistance mead makes the poison much weaker.
        when: { notKnownItem: MeadPoisonResist }
      - text: Healing mead outlasts poison. Keep a few in the hotbar.
        when: { notKnownItem: MeadHealthMinor }
    Poison:
      - text: Blobs and leeches poison you. Fight them from range or drink poison resistance mead.
    Burning:
      - text: Fire resistance barley wine keeps you from burning.
        when: { notKnownItem: BarleyWine }
      - text: Jump into water to put the fire out.
    Fire:
      - text: Surtlings and fire spells burn. Keep a fire resistance brew ready.
    Fall:
      - text: A feather cape lets you glide down from any height.
        when: { notKnownItem: CapeFeather }
      - text: Long falls hurt. Climb down slowly on steep cliffs.
    Drowning:
      - text: Swimming drains stamina. Stamina mead and more food keep you afloat longer.
        when: { notKnownItem: [MeadStaminaMinor, MeadStaminaMedium] }
      - text: Drop heavy cargo before a long swim.
    Smoke:
      - text: Smoke needs a way out. Leave a gap in the roof or build a chimney.
    Tree:
      - text: Falling trees and logs crush anyone below. Step aside after the last chop.
    Structural:
      - text: Unsupported buildings collapse. Check the support color before you go inside.
    AshlandsOcean:
      - text: The Ashlands sea boils. Stay on the boat.
    AshlandsLava:
      - text: Lava burns through any armor. Watch your step on glowing rock.
    Blunt:
      - text: Skeletons and trolls deal blunt damage. Block or parry with a shield.
        when: { notKnownItem: [ShieldWood, ShieldBronzeBuckler] }
    Spirit:
      - text: Ghosts and wraiths fear silver. A silver sword or Frostner works well.
        when: { notKnownItem: [SwordSilver, MaceSilver] }
    EnemyHit:
      - text: Eat three different foods before a fight. More health and stamina win battles.
      - text: Rest by a fire under a roof to get the Rested bonus before heading out.
      - text: A well-timed parry staggers most enemies.
```

`notKnownItem: [A, B]` means the player knows neither item. Items count as known once picked up or crafted.

### Layouts and layers

```yaml
layouts:
  logo-over-backdrops:
    vignette: { strength: 0.7 } # override one default effect value on this layout
    layers:                    # drawn in order, the first is at the back
      - color: "#000000"       # solid fill
      - id: backdrop
        image: entry          # the chosen entry's image
        animation: { enter: fade-in, idle: zoom-in, exit: fly-out-left }
        place:
          fit: cover
          fade: { sides: [bottom], color: "#000000", size: 25% }
      - image: common/logo.png # one fixed image; "image: [a.png, b.png]" picks one per screen
        place: { fit: { width: 30% }, position: { anchor: top, y: 8% } }
      - progress: ring       # shows loading progress on the game start screen
        size: 96
        place: { position: { anchor: bottom, y: -20% } }
      - spinner: hammer      # also accepts ring, dots, or a relative image path
        place: { fit: { width: 48px }, position: { anchor: bottom, y: -20% } }
      - progress: bar         # also accepts ring or percent
        color: "#FFFFFF"
        size: 16
        place: { fit: { width: 40% }, position: { anchor: bottom, y: -12% } }
      - text: caption          # the entry text or a tip
        size: 28
        color: "#FFFFFF"
        align: center
        background: "#00000099"
        place: { fit: { width: 50% }, position: { anchor: bottom, y: -6% } }
      - text: prompt           # shown only while waiting for a key
        size: 20
        place: { fit: { width: 50% }, position: { anchor: bottom, y: -2% } }
      - image: separator        # the game's ornament line
        color: "#FFFFFFCC"     # optional tint
        place: { fit: { width: 35% }, position: { anchor: bottom, y: -14% } }
```

Placement options go under `place` in `defaults`, layouts, layers and entries. The most specific
value wins: entry, then layer, then layout, then defaults. For example, `defaults.place: { fit: contain }`.

Layer `image` accepts one path, a list of paths (one picked per screen), `entry` (the selected entry's image), or
`separator` (the game's ornament line). The exact extensionless values `entry` and `separator` are keywords;
use `entry.png` or `separator.png` for files with those names. A layer has one content key: `image`, `text`,
`color`, `spinner`, or `progress`. On an `image: separator` layer, `color` is an optional tint.

A `spinner` layer works in layouts selected for game start, portal, death, and sleep screens. Use `spinner: hammer`
for Valheim's loading indicator sprite, `spinner: ring` or `spinner: dots` for generated graphics, or
`spinner: common/indicator.png` for a custom image. Custom images use the same path rules and server transfer as
image layers. Spinner layers use `place` like images and keep their native size unless `fit` is set. They use
`spin` as their idle animation by default; `animation: pulse` or an `animation` phase mapping replaces it.
On game start layouts, add `progress: ring` and `spinner: hammer` layers if you want a progress ring and Valheim's hammer.
Valheim's own indicator stays hidden while a layout is shown.

Use `progress: bar`, `progress: ring`, or `progress: percent` in any layout. It uses `place` like an image;
`color` sets its color and `size` sets bar height, ring diameter, or percentage font size. Defaults are a 240 px
bar, 96 px ring, and 28 point percentage. `place.fit` can set a different width or height. Game start follows the
real scene load operation. Portal and death wait for the target zone, then count instantiated nearby objects by
the same rules as Valheim's area readiness check. The zone-ready transition moves to 50%; the remaining half
tracks the object share. At 100%, the layer softly pulses while Valheim holds a minimum teleport or respawn delay.
Sleep tracks game time toward the next morning. While joining a server, any progress style displays the localized
connecting, password, or receiving-world stage instead of a percentage. If no loading signal is available, the
progress layer is hidden.

| Option | Values |
|---|---|
| `fit` | `cover` (fill, crop), `contain` (fit inside), `stretch` (fill, ignore aspect), `none` (native size), or `{ width, height }` in `%` of the screen or `px`. With one axis the other keeps the aspect ratio; with both the image fits inside that box. |
| `position` | `{ anchor, x, y }`. Anchors: `center`, `left`, `right`, `top`, `bottom`, `top-left`, `top-right`, `bottom-left`, `bottom-right`. `x` moves right, `y` moves down, in `%` of the screen or `px`. A plain anchor name also works: `position: top`. |
| `opacity` | `0` to `1` |
| `fade` | `{ sides, color, size, curve }`: a gradient from the color to transparent drawn over the image edges. `sides`: any of `left`, `right`, `top`, `bottom`, or `all`. `size`: `%` of the image size or `px` (default 15%). `curve`: `smooth` (default) or `linear`. |

Set `animation` on any layer or in `defaults`. A short value such as `animation: zoom-in` sets only the idle
phase. The full form is `animation: { enter: fly-in-left, idle: breathe, exit: fly-out-left, speed: 1.5 }`.
`origin` sets the point the layer scales and rotates around, like CSS `transform-origin`: `center` (default),
`left`, `right`, `top`, `bottom` or a corner such as `top-left`, so `animation: { idle: zoom-in, origin: top }`
zooms a logo away from the top edge. Layer values override the corresponding `defaults.animation` phase;
omitted phases use `none`. The default entry image and plain images use `zoom-in` when no animation is configured,
matching the previous 1.05 scale over 15 seconds; plain images follow the idle phase and speed of `defaults.animation`.
Enter plays once, idle starts after all layer enter phases finish, and exit plays once when the screen closes.
A continued menu, scene loader or HUD handover resumes idle at the last drawn time without replaying enter.
Animation time uses real time, so a blocked world load advances the phase clock.

| Built-in preset | Motion |
|---|---|
| `none` | No transform or opacity change. |
| `zoom-in` | Scale 1 to 1.05 over 15 seconds with smoothstep easing. |
| `zoom-out` | Scale 1.05 to 1 over 15 seconds. |
| `pan` | Horizontal pan from -3% to 3%, then back. |
| `ken-burns` | Gentle scale and diagonal pan, then back. |
| `fade-in` | Opacity 0 to 1. |
| `pulse` | Scale 1 to 1.06, then back. |
| `breathe` | Slow scale 1 to 1.025, then back. |
| `spin` | Continuous full rotation. |
| `sway` | Rotation from -4 to 4 degrees, then back. |
| `drift` | Gentle diagonal motion, then back. |
| `fly-in-left`, `fly-in-right`, `fly-in-top`, `fly-in-bottom` | Start beyond that screen edge, pass the destination slightly, then settle with `back-out`. |
| `fly-out-left`, `fly-out-right`, `fly-out-top`, `fly-out-bottom` | End beyond that screen edge. |

Create `animations.yaml` beside `loadingscreen.yaml` to add or replace presets:

```yaml
presets:
  gentle-reveal:
    duration: 1.2
    easing: cubic-bezier(0.22, 1, 0.36, 1)
    overshoot: 1.7
    repeat: once
    speed: 1
    keyframes:
      - { at: 0, scale: 0.9, rotation: -3, x: -20px, y: 5%, opacity: 0 }
      - { at: 1, scale: 1, rotation: 0, x: 0px, y: 0px, opacity: 1 }
```

`duration` is seconds and `speed` multiplies playback speed, including the layer's `speed` when both are set.
`easing` accepts `linear`, `ease-in`, `ease-out`, `ease-in-out`, `back-out`, `elastic-out`,
`bounce-out`, or `cubic-bezier(x1, y1, x2, y2)`; the cubic Bézier x controls must be from 0 to 1.
`overshoot` controls `back-out` (default 1.70158). `repeat` is `once`, `loop`, or `pingpong`;
`pingpong` repeats a forward and backward cycle. Keyframes require increasing `at` positions from 0 to 1.
`scale` defaults to 1, `rotation` degrees and `x`/`y` offsets default to 0, and `opacity` defaults to 1.
Positive `x` moves right; positive `y` moves down. Offsets accept `px` or percent of the screen size.
Custom names override built-ins. Unknown names warn once and use `none`. Reloading the screens also reloads
`animations.yaml`; servers share it with their screen config.

The YAML `defaults` section also sets appearance for the normal screen, layout entry images and game start screen.
All keys are optional. Missing or invalid values use the built-in values shown here. Colors accept `#RRGGBB` or
`#RRGGBBAA`; `x` is in pixels from the horizontal center and `bottom` is in pixels above the bottom edge.

```yaml
defaults:
  place: { fit: contain, position: { anchor: center } }
  background: "#000000"            # color behind the loading image
  tint: "#FFFFFF"                  # color multiplied over entry and plain images
  vignette: { color: "#000000", size: 25%, strength: 0.5 }
  mask: { color: "#000000", opacity: 0.25 }
  animation: { enter: fade-in, idle: breathe, exit: fly-out-left }
  tip: { x: 0, bottom: 90, size: 24, color: "#FFFFFF" }
  continuePrompt: { x: 0, bottom: 30, size: 20, color: "#FFFFFF" }
  separator: { show: true, x: 0, bottom: 150 }
  spinner: { center: true, x: 0, bottom: 200 }
```

`tip.size` accepts `0` to `64`.
`vignette` darkens the outer screen area with a radial gradient. `size` is the outer `0%` to `100%` of the
distance from the center to the screen edge; `strength` is `0` to `1`. `mask` tints the whole screen with
`opacity` from `0` to `1`. Both effects are off when omitted. Once enabled, missing values use the shown
built-ins; a layout can override individual values from `defaults`. Set `strength: 0` or `opacity: 0` on a
layout to disable an inherited effect there. Color alpha also affects the final opacity. Effects cover
layered screens and plain-image fallbacks.

`continuePrompt` positions and styles the line shown during a key hold on screens without a layout; its `size`
accepts `0` to `64`. `defaults.spinner` styles Valheim's game start indicator on screens without a layout. It
does not add layers. Layouts show only their declared layers, regardless of `defaults` tip, separator, spinner,
and continue prompt settings. An older `separator.onLayouts` value is accepted but has no effect.

A text layer accepts `place` with `position`, `opacity`, and `fit: { width }` (the text box width, default 80% of the screen), plus
`size`, `color`, `align` (`left`, `center`, `right`), `background` (a color box behind the text). Without a text
layer, a layout shows no caption. `text: caption` shows the entry text or a tip. `text: prompt` shows the localized
continue prompt only while the screen waits for a key; it accepts the same style and animation keys.

Set a text layer's `color: auto` to choose light or dark text from the average brightness of the image behind its
text box. `background: auto` chooses a light or dark box at 60% opacity; `color: auto` also accounts for that box.
The image is sampled once when loaded, and the choice updates when the text box's placement changes. On screens
without a sampled custom image, the YAML default background is used.

`[6 - Accessibility] Readable text` is a personal setting, off by default. When enabled, it preserves the selected
text hue while adjusting its lightness toward WCAG AA 4.5:1 contrast against the effective background; if needed,
it strengthens the text box and renders the text opaque. `[6 - Accessibility] Minimum text size` is also personal. Its default `0` disables the
minimum; positive values apply to text layers, tips, captions, and progress labels.

Layouts hide the game's sleep text and spinner. Add `text: caption` or other layers if the sleep layout needs
them. Screens without a layout keep the game's sleep UI. An older `showSleepUi` value is accepted but has no effect.

A `image: separator` layer draws the game's ornament line as an image layer. It takes the placement options and an
optional `color` tint; without placement it keeps its native size near the bottom. Layouts never show the built-in
separator. To use your own line, add it as an `image` layer.

### Settings

Behavior switches in `Odin_Sons.LoadingScreen.cfg`; each has a description in the Configuration Manager tooltip.

| Section | Settings |
|---|---|
| `1 - General` | Lock Configuration, Mod enabled, Image folder, Text language, Log level, Share screens with players |
| `2 - When to show` | Loading screen on portal teleport, Loading screen while sleeping, Loading screen right after death, Random tip when an entry has no text |
| `3 - Timing` | Change image every N seconds, Change tip every N seconds, Hold loading screen until a key is pressed, Invulnerable while held, Wait for the voice to finish, Wait for the exit animation to finish, Maximum hold time in seconds |
| `4 - Game start screen` | Use loading images, Show progress |
| `5 - Sound` | Play loading screen sounds, Music volume percent, Voice volume percent, Mute game sounds on loading screens |
| `6 - Accessibility` | Readable text, Minimum text size |
| `7 - Experimental` | Load world in background |

`[7 - Experimental] Load world in background` is a personal setting, off by default. When enabled, starting or
joining a world loads the world scene in the background instead of freezing the game, so the loading screen keeps
animating; menu buttons and keys are ignored until the world opens. If the background load cannot start, the game's
normal load runs and a warning is logged. With `Log level` at `Info`, each world load logs
`Loaded the world scene.` on `LoadingScreen` with `mode` (`sync` or `async`), `totalSeconds`, and for background
loads `readSeconds` (until the scene data is read) and `activationSeconds` (until the world objects are created).

### Config synchronization

Install LoadingScreen on the server and players who should receive its settings. With
[Conditional Config Sync 1.0.10+](https://github.com/shudnal/ConditionalConfigSync/blob/master/docs/conditional-conditional-config-sync.md)
active, LoadingScreen uses its optional adapter. Otherwise the ServerSync code merged into LoadingScreen handles
config synchronization. The server and player must use the same path; install CCS on both or neither. If there is
no matching server plugin or sync path, settings stay local.

The following cfg settings are server controlled by default with ServerSync, or `Conditional` with CCS so a server
admin can choose ownership in `ConditionalConfigSync.SyncPolicy.cfg`:
`Mod enabled`, `Share screens with players`, all settings in `2 - When to show`, and all settings in `3 - Timing`
except `Hold loading screen until a key is pressed`. `Lock Configuration` controls the lock itself.
The remaining settings are client controlled: `Image folder`, `Text language`, `Log level`, the key hold, both game start settings,
sound settings, and `Load world in background`. Each cfg description ends with its sync status. The server's
`loadingscreen.yaml` and `animations.yaml` carry appearance defaults and presets through
the existing screen transfer; they are not cfg settings.

Set `[1 - General] Lock Configuration` to `true` on the server (the default) to restrict changes
to server admins. Set it to `false` to allow unlocked synchronized settings to be changed by clients.
CCS admins can also override `Conditional` ownership in
`BepInEx/config/shudnal.ConditionalConfigSync/ConditionalConfigSync.SyncPolicy.cfg`.
Received cfg values, including `Mod enabled`, take effect without restarting the game.
Settings marked as requiring a restart remain local.

`[1 - General] Log level` defaults to `Warning`. Choose `Error`, `Warning`, `Info`, or `Debug` to control
LoadingScreen's messages. `Debug` includes screen selection reasons, audio volumes, file transfers, and image load timings.
The configuration summary after each load is written at BepInEx `Message` level; it is shown from `Warning` when it
reports problems and from `Info` otherwise. Messages come from one BepInEx source per area, so lines read
`[Warning:LoadingScreen.Audio]`: `LoadingScreen.Config` (YAML, translations, images and tips),
`LoadingScreen.Audio`, `LoadingScreen.Server` (setting sync and server screen sharing), `LoadingScreen.Render`
(screen selection and display), and `LoadingScreen` for startup. `Log level` applies to all of them.

### Console

`loadingscreen status` shows what is loaded; `loadingscreen reload` re-reads `loadingscreen.yaml`, the images and
the selected tip file. A broken YAML file keeps the previously loaded config.

When a load or reload logs warnings or errors for `loadingscreen.yaml`, `animations.yaml` or `translations.yaml`, the
console shows one line with the number of problems per file, for example
`LoadingScreen found problems in loadingscreen.yaml: 2, translations.yaml: 1. Details are in BepInEx/LogOutput.log.`
The main menu shows the same line once in a dismissible popup when the console is closed. Its text uses the
`configProblems` translation key, where `{files}` is the per-file list. Problems hidden by `Log level` are not counted.

## Server images

A dedicated server, or a player hosting a world, that runs this mod shares its `loadingscreen.yaml`, images and audio
it uses. A client with the mod then:

1. receives the config and a manifest (path, size, SHA-256 of every file) when its character spawns;
2. uses the server's screens while connected, and its own local config again after disconnecting;
3. downloads the files in the background, one 32 KiB piece at a time over the game connection, in this order:
   layer images, general, death, biome, then audio;
4. stores them in `BepInEx/cache/LoadingScreen/` named by hash, so a file is downloaded only once, also across
   servers. The folder can be deleted at any time.

An image or audio clip that is not downloaded yet is skipped, so a download never delays a loading screen. PNG files are
gzip-compressed for the transfer; JPEG files are sent as they are.

The transfer runs over the direct connection between the client and the server, so other players cannot inject
data. Everything from the server is treated as untrusted:

- limits: 16 MiB per image, 8 MiB per audio clip, 512 MiB and 2000 files per server, 256 KiB of YAML, 1 GiB of cache on disk (the oldest
  files of other servers are removed first);
- every file is checked against its size and SHA-256 hash and must have a supported image or audio header; paths must
  stay inside the folder; decompression is bounded;
- server captions are cut to 500 characters and rich-text tags are removed;
- the server answers each client at a limited rate, and a client gives up on a piece after a few timeouts.

The game start screen can only use local files, because no server is connected yet.

## Image formats

**Now: PNG and JPEG only.** Unity decodes them itself (`ImageConversion.LoadImage`), so the main package stays a
single plugin DLL with no native code.

**Later (not scheduled): WebP, AVIF and JPEG XL in a separate optional download.** These formats need native decoders.
All three are royalty-free, with decoders under permissive licenses compatible with GPL-3.0:

| Format | Decoder | License | Typical size vs. JPEG |
|---|---|---|---|
| WebP | libwebp | BSD-3-Clause plus Google's royalty-free patent grant | about 25-35 % smaller |
| AVIF | libavif + dav1d (AV1) | BSD-2-Clause; AV1 is royalty-free under the AOMedia patent license | about 50 % smaller |
| JPEG XL | libjxl | BSD-3-Clause plus a royalty-free patent grant | about 50 % smaller; can recompress existing JPEGs losslessly (about 20 % smaller) |

A patent pool (Sisvel) claims patents on AV1 outside the AOMedia license; no fees are known to be charged for
software decoders, but this is a reading of public information, not legal advice.

Packaging rules for the decoder download:

- A separate package (an "extra formats" kit) that holds the decoder libraries as plain files, one build per platform
  (Windows x64, Linux x64; macOS optional), plus their license and patent files. The main mod detects it and
  enables the formats it finds; without it, those files are skipped with a warning.
- Never embed native libraries in the plugin DLL and extract them at run time. Hexium's terms ban "concealed
  payloads", Thunderstore's global rules ask packages not to hinder auditing, and Nexus Mods and Hexium scan every
  upload with VirusTotal, where self-extracting loaders are likely to be flagged.
- Hexium's packaging guide says "never copy another author's DLLs into your package". It is unclear whether that
  covers general libraries such as libwebp; ask Hexium moderators before the first upload of the kit.
- Native decoders parse untrusted files: pin versions, update them on security advisories, cap file size and
  resolution before decoding, and decode server-provided images (see [Server images](#server-images)) only with an explicit opt-in.

Not decided yet: JPEG encoder guidance (mozjpeg, jpegli) and recommended resolution and quality settings.


## Original Intermission features (kept)

- Show custom images and tips on the loading screen.
- Plain `.png` or `.jpg` files in the configured image folder and one loading tip per line in `tips.txt`.
- The loading screen instead of the portal animation and right after death.
- Game start screen, image tint and zoom, background color, separator line and tip text style settings.

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

Unit tests for the Unity-free code (config parser, selection, placement math) need only the .NET SDK:

```sh
dotnet test tests/LoadingScreen.Tests
```
