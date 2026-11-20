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

Version 0.2.0. All planned features below are implemented and covered by unit tests where the code does not depend
on Unity, but **the mod has not been tested in the game yet**. See the
[issues](https://github.com/odin-sons/LoadingScreen/issues) for the test checklist and the remaining work.

## Features

- **Situational screens.** A portal teleport shows an image chosen for the destination biome; a respawn shows an
  image chosen for the cause of death; everything else uses the general list. Within a list, one entry is picked at
  random without immediate repeats.
- **Layered layouts.** A screen is a stack of layers (solid colors, fixed images, random image pools, the chosen
  entry's image, the caption), each sized and positioned on its own, with gradient edges and an optional slow zoom.
  Two presets are included in the example config.
- **Captions.** An entry can carry its own text; without it, a tip from `tips.txt` is shown.
- **Conditions.** An entry can be limited to players who, for example, have never held a Wisplight or have not
  defeated a boss yet, so a tip only reaches the players who need it.
- **Server images.** A server that runs the mod can share its config and images; clients download the images in the
  background and use the server's screens while connected.
- **Reload in game** with the `loadingscreen reload` console command.
- Everything Intermission does still works and stays the fallback.

## Installation

Requires `denikson-BepInExPack_Valheim` and `ValheimModding-YamlDotNet`. Put `LoadingScreen.dll` into
`BepInEx/plugins`. It can be installed next to the original Intermission.

On first start the mod creates `BepInEx/config/LoadingScreen/loadingscreen.yaml` with a commented example. While
everything in it stays commented out, the mod behaves like Intermission.

## Configuration

Files in `BepInEx/config/LoadingScreen/`:

| File | Purpose |
|---|---|
| `loadingscreen.yaml` | Situations, layouts and placement (below) |
| `tips.txt` | One tip per line |
| `*.png`, `*.jpg` at the top level | Plain images, used when no situational entry applies |

Settings are in `BepInEx/config/Odin_Sons.LoadingScreen.cfg`.

### Selection

| Loading screen | First choice | Then |
|---|---|---|
| Portal teleport | `situations.teleport.<destination biome>` | `situations.general`, then the plain images |
| Respawn | `situations.death.<cause>` | `situations.general`, then the plain images |
| Anything else, main menu, game start | `situations.general` | the plain images |

- Biomes: `Meadows`, `BlackForest`, `Swamp`, `Mountain`, `Plains`, `Mistlands`, `AshLands`, `DeepNorth`, `Ocean`.
  The biome is resolved from the destination position. Teleport screens need `[Hud.LoadingScreen] showOnTeleport`
  (on by default); without it the game shows its own portal animation.
- Causes are the game's own hit types: `Undefined`, `EnemyHit`, `PlayerHit`, `Fall`, `Drowning`, `Burning`,
  `Freezing`, `Poisoned`, `Water`, `Smoke`, `EdgeOfWorld`, `Impact`, `Cart`, `Tree`, `Self`, `Structural`,
  `Turret`, `Boat`, `Stalagtite`, `Catapult`, `CinderFire`, `AshlandsOcean`, `AshlandsLava`, `Incinerator`,
  `DrawBridge`.
- For `EnemyHit`, `PlayerHit`, `Turret`, `Catapult` and `Undefined`, the dominant damage type of the killing hit is
  tried first: `Blunt`, `Slash`, `Pierce`, `Chop`, `Pickaxe`, `Fire`, `Frost`, `Lightning`, `Poison`, `Spirit`.
  Example: a frost drake kill uses `death.Frost` if it exists, then `death.EnemyHit`, then `general`.
- YAML anchors and aliases are not supported, and nesting is limited to 32 levels.
- Names are case-insensitive. Unknown names, missing files and invalid values are reported in
  `BepInEx/LogOutput.log` with line numbers; the rest of the config still loads.

### Entries

```yaml
situations:
  general:                     # a list of entries, or { layout, entries }
    layout: logo-over-backdrops
    entries:
      - image: general/valheim.jpg
        text: Skol!
      - general/another.jpg    # an image without text
  teleport:
    Mistlands:
      - image: mistlands/fog.jpg
        text: Light your wisp.
        layout: backdrop-art   # overrides the list's layout
        place: { position: { anchor: right, x: -5% } }   # placement for this image only
  death:
    Fall: [ death/fall.jpg ]
```

Image paths are relative to the config folder; `.png`, `.jpg` and `.jpeg` are supported, up to 8192 px per side
and 16.7 megapixels (for example 3840x2160 or 5120x2880; larger files are skipped with a log message). An entry without a layout
uses a single layer that shows its image with the `defaults` placement (`contain`, centered, unless changed).

An entry may have only a `text`; it is shown over a random plain image from the config folder, or over the game's
own image if there is none.

### Conditions

`when` limits an entry to players for whom every condition holds:

```yaml
situations:
  teleport:
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
| `knownBiome` | the player has discovered the biome | biome name from the list above |
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
situations:
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
situations:
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
situations:
  teleport:
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
    layers:                    # drawn in order, the first is at the back
      - color: "#000000"       # solid fill
      - id: backdrop
        source: entry          # the chosen entry's image
        fit: cover
        fade: { sides: [bottom], color: "#000000", size: 25% }
      - image: common/logo.png # one fixed image; "images: [a.png, b.png]" picks one per screen
        fit: { width: 30% }
        position: { anchor: top, y: 8% }
      - text: caption          # the entry text or a tip
        size: 28
        color: "#FFFFFF"
        align: center
        background: "#00000099"
        fit: { width: 50% }
        position: { anchor: bottom, y: -6% }
```

Placement options, accepted in `defaults`, on a layout, on a layer and in an entry's `place`. The most specific
value wins: entry `place`, then layer, then layout, then `defaults`.

| Option | Values |
|---|---|
| `fit` | `cover` (fill, crop), `contain` (fit inside), `stretch` (fill, ignore aspect), `none` (native size), or `{ width, height }` in `%` of the screen or `px`. With one axis the other keeps the aspect ratio; with both the image fits inside that box. |
| `position` | `{ anchor, x, y }`. Anchors: `center`, `left`, `right`, `top`, `bottom`, `top-left`, `top-right`, `bottom-left`, `bottom-right`. `x` moves right, `y` moves down, in `%` of the screen or `px`. A plain anchor name also works: `position: top`. |
| `opacity` | `0` to `1` |
| `fade` | `{ sides, color, size, curve }`: a gradient from the color to transparent drawn over the image edges. `sides`: any of `left`, `right`, `top`, `bottom`, or `all`. `size`: `%` of the image size or `px` (default 15%). `curve`: `smooth` (default) or `linear`. |
| `animate` | `true` or `false`: the slow zoom from `[LoadingImage.ScaleLerp]`; defaults to that section's `useScaleLerp`. |

A text layer accepts `position`, `opacity`, `fit: { width }` (the text box width, default 80% of the screen) and
`size`, `color`, `align` (`left`, `center`, `right`), `background` (a color box behind the text). Without a text
layer, the caption goes to the usual tip line.

### Settings

New settings in `Odin_Sons.LoadingScreen.cfg`, next to the Intermission ones:

| Section | Setting | Default | Meaning |
|---|---|---|---|
| `Situations` | `useTipWhenNoText` | `true` | An entry without text shows a random tip; otherwise no text |
| `ServerImages` | `serveToClients` | `true` | Server: share `loadingscreen.yaml` and its images with clients |
| `ServerImages` | `downloadFromServer` | `true` | Client: use the server's screens and download their images |

### Console

`loadingscreen status` shows what is loaded; `loadingscreen reload` re-reads `loadingscreen.yaml`, the images and
`tips.txt`. A broken YAML file keeps the previously loaded config.

## Server images

A dedicated server, or a player hosting a world, that runs this mod shares its `loadingscreen.yaml` and the images
it uses. A client with the mod then:

1. receives the config and a manifest (path, size, SHA-256 of every image) when its character spawns;
2. uses the server's screens while connected, and its own local config again after disconnecting;
3. downloads the images in the background, one 32 KiB piece at a time over the game connection, in this order:
   layer images, general, death, teleport;
4. stores them in `BepInEx/cache/LoadingScreen/` named by hash, so an image is downloaded only once, also across
   servers. The folder can be deleted at any time.

An image that is not downloaded yet is skipped, so a download never delays a loading screen. PNG files are
gzip-compressed for the transfer; JPEG files are sent as they are.

The transfer runs over the direct connection between the client and the server, so other players cannot inject
data. Everything from the server is treated as untrusted:

- limits: 16 MiB per image, 512 MiB and 2000 images per server, 256 KiB of YAML, 1 GiB of cache on disk (the oldest
  files of other servers are removed first);
- every file is checked against its size and SHA-256 hash and must be a PNG or JPEG within the image size limit
  below; paths must stay inside the folder; decompression is bounded;
- server captions are cut to 500 characters and rich-text tags are removed;
- the server answers each client at a limited rate, and a client gives up on a piece after a few timeouts.

The game start screen can only use local images, because no server is connected yet.

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
- Plain `.png` or `.jpg` files in `BepInEx/config/LoadingScreen/` and one loading tip per line in `tips.txt`.
- `[Hud.LoadingScreen] showOnTeleport`: show the loading screen instead of the vanilla portal animation.
- `[Hud.LoadingScreen] showImmediatelyOnDeath`: show the loading screen right when the player dies.
- `[SceneLoader]`, `[LoadingImage.*]`, `[LoadingScreen.*]`, `[LoadingTip.*]`: game start screen, image tint and
  zoom, background color, separator and tip text style.

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
