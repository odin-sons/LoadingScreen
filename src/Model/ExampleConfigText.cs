namespace LoadingScreen.Model;

public static class ExampleConfigText {
  public const string Text = """
      # LoadingScreen situational config. Everything is commented out, so the mod behaves like Intermission
      # (random images from this folder and tips.txt) until you uncomment and edit the parts you need.
      # Optional tips.<code>.txt (for example tips.ru.txt) is used before tips.txt for that language.
      # Image paths are relative to this folder. Supported: .png, .jpg, .jpeg.
      # Audio paths are also relative to this folder. Supported: .ogg and .wav, up to 8 MiB per clip.
      # A layout, list or entry can set sound: music/forest.ogg; the entry wins over the list, then the layout.
      # An entry can set voice: voice/tip.ogg; it plays once when that entry's own text is shown.
      # Custom animation presets belong in animations.yaml beside this file.
      # Reload both files in game with: loadingscreen reload
      #
      # Selection: sleep uses when.sleep, a portal teleport uses when.biome.<destination biome>,
      # and a respawn tries when.enemy.<attacker>, when.death.<damage type>,
      # when.death.<hit type>,
      # then when.biome.<death biome>; custom EWD biomes try their base biome list before general;
      # if these lists are missing or empty, when.general is used; if that is
      # empty too, the plain images in this folder are shown.
      #
      # Biomes: Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth, Ocean
      # EWD custom biomes use the biome: identifier from expand_biomes.yaml, not the display name.
      # Unknown biome names warn after EWD data loads, or when the config loads if EWD is absent.
      # Causes (how the last hit happened): Undefined, EnemyHit, PlayerHit, Fall, Drowning, Burning, Freezing,
      #   Poisoned, Water, Smoke, EdgeOfWorld, Impact, Cart, Tree, Self, Structural, Turret, Boat, Stalagtite,
      #   Catapult, CinderFire, AshlandsOcean, AshlandsLava, Incinerator, DrawBridge
      # Damage types (checked first for EnemyHit, PlayerHit, Turret, Catapult, Undefined): Blunt, Slash, Pierce,
      #   Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit
      # A kill by a creature tries when.enemy.<prefab name> first (enemy: { Troll: [...] }).
      # Prefab names are free-form, including modded creatures.
      #
      # Order: entries are picked at random; "order: sequential" (top level or on a list) uses config order.
      #
      # Conditions ("when" on an entry, all must hold; prefix with "not" to invert, e.g. notKnownItem):
      #   knownItem, hasItem, knownRecipe (item prefab name like Demister), knownBiome, globalKey, playerKey
      # Entry text can be a plain string or { en: English text, ru: Russian text }.
      # Missing translations use en, then the first available translation.
      #
      # Placement options (under "place" in defaults, layouts, layers and entries; the most specific wins):
      #   fit: cover | contain | stretch | none | { width: 40%, height: 300px }
      #        (one axis keeps the aspect ratio; both axes fit the image inside that box)
      #   position: { anchor: center | left | right | top | bottom | top-left | ..., x: 5%, y: -20px }
      #        (x moves right, y moves down; % of the screen size)
      #   opacity: 0..1
      #   fade: { sides: [left, bottom], color: "#000000", size: 15%, curve: smooth | linear }
      #        (gradient from the color into the image; size is % of the image size or px)
      # Layer animation: zoom-in is idle-only. Use animation: { enter: fly-in-left, idle: breathe,
      #   exit: fly-out-left, speed: 1.5 } for phases. defaults.animation supplies missing phases.
      # With no animation setting, the default entry image zooms from 1 to 1.05 over 15 seconds.
      # Screen effects cover the whole loading screen. Set them in defaults or override individual values
      # on a layout. Omit a block to leave that effect off unless defaults enables it.
      #   vignette: { color: "#000000", size: 25%, strength: 0.5 }
      #        (size is the outer 0%..100% from center to screen edge; strength is 0..1)
      #   mask: { color: "#000000", opacity: 0.25 }  (a full-screen color tint; opacity is 0..1)
      #
      # Layers: "color" (solid fill), "image" (one file, a list of files, "entry", or "separator"),
      # "spinner" (hammer, ring, dots, or a relative image path),
      # "image: entry" (the chosen entry's image), "image: separator" (the game's ornament line,
      # takes placement and a "color" tint, replaces the built-in separator) and "text: caption" (the entry text or a tip).
      # Exact extensionless "entry" and "separator" are keywords; files named entry.png or separator.png work normally.
      # A spinner spins by default; animation: pulse or another preset replaces that motion.
      # Place a spinner like an image. Without place.fit it uses the sprite's native size.
      # A text layer takes place (position, opacity, fit: { width }) and: size, color, align (left | center | right),
      # background (a color box behind the text). color and background also accept auto;
      # auto picks light or dark from the image behind the text box (auto background uses 60% opacity).
      # Without a text layer the usual tip line is used.
      # A sleep layout can set showSleepUi: false to hide the game's sleep text and spinner and use its own layers.

      # defaults:
      #   place: { fit: contain, position: { anchor: center } }
      #   background: "#000000"
      #   tint: "#FFFFFF"
      #   vignette: { color: "#000000", size: 25%, strength: 0.5 }
      #   mask: { color: "#000000", opacity: 0.25 }
      #   animation: { enter: fade-in, idle: breathe, exit: fly-out-left }
      #   tip: { x: 0, bottom: 90, size: 24, color: "#FFFFFF" }
      #   continuePrompt: { x: 0, bottom: 30, size: 20, color: "#FFFFFF" }
      #   separator: { show: true, x: 0, bottom: 150, onLayouts: true }
      #   spinner: { center: true, x: 0, bottom: 200 }  # legacy game-start hammer placement

      # layouts:
      #   backdrop-art:
      #     sound: music/forest.ogg
      #     layers:
      #       - color: "#000000"
      #       - image: common/backdrop.jpg
      #         place: { fit: stretch }
      #       - id: art
      #         image: entry
      #         animation: { enter: fly-in-left, idle: breathe, exit: fly-out-left, speed: 1.5 }
      #         place:
      #           fit: { width: 45%, height: 80% }
      #           position: { anchor: left, x: 5% }
      #           fade: { sides: [left, right], color: "#000000", size: 10% }
      #
      #   logo-over-backdrops:
      #     showSleepUi: false
      #     vignette: { strength: 0.7 }
      #     layers:
      #       - id: backdrop
      #         image: entry
      #         place: { fit: cover, fade: { sides: [bottom], color: "#000000", size: 25% } }
      #       - image: common/logo.png
      #         place: { fit: { width: 30% }, position: { anchor: top, y: 8% } }
      #       - spinner: ring
      #         place: { fit: { width: 48px }, position: { anchor: bottom, y: -20% } }
      #       - text: caption
      #         size: 28
      #         color: auto
      #         background: auto
      #         place: { fit: { width: 50% }, position: { anchor: bottom, y: -6% } }

      # when:
      #   general:
      #     layout: logo-over-backdrops
      #     sound: music/forest.ogg
      #     entries:
      #       - image: general/valheim.jpg
      #         text: { en: Skol!, ru: Скол! }
      #         voice: voice/welcome.ogg
      #       - general/another.jpg
      #   sleep:
      #     layout: logo-over-backdrops
      #     entries:
      #       - image: sleep/night.jpg
      #         text: Rest until morning.
      #   biome:
      #     Mistlands:
      #       - image: mistlands/fog.jpg
      #         text: Light your wisp.
      #         layout: backdrop-art
      #         place: { position: { anchor: right, x: -5% } }
      #       - text: The mist is thick. Craft a Wisplight to see further.
      #         when:
      #           notKnownItem: Demister
      #   death:
      #     Fall:
      #       - image: death/fall.jpg
      #         text: Mind the cliff.
      #     Frost:
      #       - image: death/frost.jpg
      #         text: Should have worn the troll armor.
      #   enemy:
      #     Troll:
      #       - text: Dodge the swing, then strike from behind.

      """;
}
