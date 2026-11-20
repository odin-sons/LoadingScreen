namespace LoadingScreen.Screens;

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
      #
      # Placement options (defaults, layouts, layers and an entry's "place"; the most specific one wins):
      #   fit: cover | contain | stretch | none | { width: 40%, height: 300px }
      #        (one axis keeps the aspect ratio; both axes fit the image inside that box)
      #   position: { anchor: center | left | right | top | bottom | top-left | ..., x: 5%, y: -20px }
      #        (x moves right, y moves down; % of the screen size)
      #   opacity: 0..1
      #   fade: { sides: [left, bottom], color: "#000000", size: 15%, curve: smooth | linear }
      #        (gradient from the color into the image; size is % of the image size or px)
      #   animate: true | false   (slow zoom, uses the LoadingImage.ScaleLerp settings)

      # defaults:
      #   fit: contain
      #   position: { anchor: center }

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
      #     layers:
      #       - id: backdrop
      #         source: entry
      #         fit: cover
      #         fade: { sides: [bottom], color: "#000000", size: 25% }
      #       - image: common/logo.png
      #         fit: { width: 30% }
      #         position: { anchor: top, y: 8% }

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
      #   death:
      #     Fall:
      #       - image: death/fall.jpg
      #         text: Mind the cliff.
      #     Frost:
      #       - image: death/frost.jpg
      #         text: Should have worn the troll armor.

      """;
}
