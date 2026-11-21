namespace LoadingScreen.Model;

public static class ExampleConfigText {
  public const string Text = """
      # LoadingScreen situational config. Everything is commented out, so the mod behaves like Intermission
      # (random images from this folder and tips.txt) until you uncomment and edit the parts you need.
      # Image paths are relative to this folder. Supported: .png, .jpg, .jpeg.
      # Reload in game with the console command: loadingscreen reload
      #
      # Selection: a portal teleport uses situations.teleport.<destination biome>, a respawn uses
      # situations.death.<cause>; if that list is missing or empty, situations.general is used; if that is
      # empty too, the plain images in this folder are shown.
      #
      # Biomes: Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth, Ocean
      # Causes (how the last hit happened): Undefined, EnemyHit, PlayerHit, Fall, Drowning, Burning, Freezing,
      #   Poisoned, Water, Smoke, EdgeOfWorld, Impact, Cart, Tree, Self, Structural, Turret, Boat, Stalagtite,
      #   Catapult, CinderFire, AshlandsOcean, AshlandsLava, Incinerator, DrawBridge
      # Damage types (checked first for EnemyHit, PlayerHit, Turret, Catapult, Undefined): Blunt, Slash, Pierce,
      #   Chop, Pickaxe, Fire, Frost, Lightning, Poison, Spirit
      # A kill by a creature tries death.enemy.<prefab name> first (death: { enemy: { Troll: [...] } }).
      #
      # Order: entries are picked at random; "order: sequential" (top level or on a list) uses config order.
      #
      # Conditions ("when" on an entry, all must hold; prefix with "not" to invert, e.g. notKnownItem):
      #   knownItem, hasItem, knownRecipe (item prefab name like Demister), knownBiome, globalKey, playerKey
      #
      # Placement options (defaults, layouts, layers and an entry's "place"; the most specific one wins):
      #   fit: cover | contain | stretch | none | { width: 40%, height: 300px }
      #        (one axis keeps the aspect ratio; both axes fit the image inside that box)
      #   position: { anchor: center | left | right | top | bottom | top-left | ..., x: 5%, y: -20px }
      #        (x moves right, y moves down; % of the screen size)
      #   opacity: 0..1
      #   fade: { sides: [left, bottom], color: "#000000", size: 15%, curve: smooth | linear }
      #        (gradient from the color into the image; size is % of the image size or px)
      #   animate: true | false   (slow zoom, defaults to defaults.zoom.enabled)
      # Screen effects cover the whole loading screen. Set them in defaults or override individual values
      # on a layout. Omit a block to leave that effect off unless defaults enables it.
      #   vignette: { color: "#000000", size: 25%, strength: 0.5 }
      #        (size is the outer 0%..100% from center to screen edge; strength is 0..1)
      #   mask: { color: "#000000", opacity: 0.25 }  (a full-screen color tint; opacity is 0..1)
      #
      # Layers: "color" (solid fill), "image" (one file), "images" (a random file per screen),
      # "source: entry" (the image of the chosen entry), "separator: game" (the game's ornament line,
      # takes placement and a "color" tint, replaces the built-in separator) and "text: caption" (the entry text or a tip).
      # A text layer takes position, opacity, "fit: { width }" and: size, color, align (left | center | right),
      # background (a color box behind the text). Without a text layer the usual tip line is used.

      # defaults:
      #   fit: contain
      #   position: { anchor: center }
      #   background: "#000000"
      #   tint: "#FFFFFF"
      #   vignette: { color: "#000000", size: 25%, strength: 0.5 }
      #   mask: { color: "#000000", opacity: 0.25 }
      #   zoom: { enabled: true, amount: 1.05, seconds: 15 }
      #   tip: { x: 0, bottom: 90, size: 24, color: "#FFFFFF" }
      #   separator: { show: true, x: 0, bottom: 150, onLayouts: true }
      #   spinner: { center: true, x: 0, bottom: 200 }

      # layouts:
      #   backdrop-art:
      #     layers:
      #       - color: "#000000"
      #       - image: common/backdrop.jpg
      #         fit: stretch
      #       - id: art
      #         source: entry
      #         fit: { width: 45%, height: 80% }
      #         position: { anchor: left, x: 5% }
      #         fade: { sides: [left, right], color: "#000000", size: 10% }
      #
      #   logo-over-backdrops:
      #     vignette: { strength: 0.7 }
      #     layers:
      #       - id: backdrop
      #         source: entry
      #         fit: cover
      #         fade: { sides: [bottom], color: "#000000", size: 25% }
      #       - image: common/logo.png
      #         fit: { width: 30% }
      #         position: { anchor: top, y: 8% }
      #       - text: caption
      #         size: 28
      #         background: "#00000099"
      #         fit: { width: 50% }
      #         position: { anchor: bottom, y: -6% }

      # situations:
      #   general:
      #     layout: logo-over-backdrops
      #     entries:
      #       - image: general/valheim.jpg
      #         text: Skol!
      #       - general/another.jpg
      #   teleport:
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

      """;
}
