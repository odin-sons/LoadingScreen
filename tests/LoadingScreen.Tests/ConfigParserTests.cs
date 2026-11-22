namespace LoadingScreen.Tests;

using System.Collections.Generic;
using System.Linq;

using LoadingScreen.Model;

using Xunit;

public class ConfigParserTests {
  static readonly HashSet<string> _files = [
    "general/a.jpg", "general/b.png", "sleep/night.jpg", "mistlands/fog.jpg", "death/fall.jpg", "common/logo.png", "common/bg.jpeg",
    "music/forest.ogg", "music/list.wav", "music/entry.ogg", "voice/tip.wav", "entry.png", "separator.png",
    "common/indicator.png"
  ];

  static ConfigLoadResult Parse(string yaml, ICollection<string> causes = null) {
    return new ConfigParser(path => _files.Contains(path), causes).Parse(yaml);
  }

  [Fact]
  public void TextLayerAcceptsAutomaticTextAndBackgroundColors() {
    const string yaml = """
        layouts:
          accessible:
            layers:
              - image: entry
              - text: caption
                color: auto
                background: auto
        """;

    ConfigLoadResult result = Parse(yaml);
    Assert.Empty(result.Warnings);
    TextStyle style = result.Config.Layouts["accessible"].Layers[1].Text;
    Assert.True(style.AutoColor);
    Assert.True(style.AutoBackground);
    Assert.Null(style.Color);
    Assert.Null(style.Background);
  }

  [Fact]
  public void OldSituationsKeyIsNotAccepted() {
    ConfigLoadResult result = Parse("situations: { general: [ general/a.jpg ] }");

    Assert.True(result.Config.IsEmpty);
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown top-level key 'situations'"));
  }

  [Fact]
  public void ParsesMusicAtEachScopeAndEntryVoice() {
    const string yaml = """
        layouts:
          forest:
            sound: music/forest.ogg
            layers: [ { image: entry } ]
        when:
          general:
            layout: forest
            sound: music/list.wav
            entries:
              - image: general/a.jpg
                sound: music/entry.ogg
                voice: voice/tip.wav
                text: Welcome.
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Empty(result.Warnings);
    Assert.Equal("music/forest.ogg", result.Config.Layouts["forest"].Sound);
    Assert.Equal("music/list.wav", result.Config.General.Sound);
    Assert.Equal("music/entry.ogg", result.Config.General.Entries[0].Sound);
    Assert.Equal("voice/tip.wav", result.Config.General.Entries[0].Voice);
  }

  [Fact]
  public void InvalidAudioPathsWarnAndAreIgnoredWithoutLosingEntries() {
    const string yaml = """
        when:
          general:
            sound: ../outside.ogg
            entries:
              - image: general/a.jpg
                sound: music/missing.ogg
                voice: voice/tip.mp3
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Single(result.Config.General.Entries);
    Assert.Null(result.Config.General.Sound);
    Assert.Null(result.Config.General.Entries[0].Sound);
    Assert.Null(result.Config.General.Entries[0].Voice);
    Assert.Equal(3, result.Warnings.Count);
    Assert.All(result.Warnings, warning => Assert.Contains("line=", warning));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   \n# only a comment\n")]
  public void EmptyConfigIsEmptyWithoutWarnings(string yaml) {
    ConfigLoadResult result = Parse(yaml);

    Assert.True(result.Config.IsEmpty);
    Assert.Empty(result.Warnings);
    AssertBuiltInAppearance(result.Config.Defaults);
  }

  [Fact]
  public void ParsesSleepListAndLayoutUiChoice() {
    const string yaml = """
        layouts:
          dream:
            showSleepUi: false
            layers:
              - image: entry
              - text: caption
        when:
          sleep:
            layout: dream
            entries: [ sleep/night.jpg ]
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Empty(result.Warnings);
    Assert.Equal("sleep/night.jpg", result.Config.Sleep.Entries.Single().Image);
    Assert.False(result.Config.Layouts["dream"].ShowSleepUi);
    Assert.Equal(1, result.Config.EntryCount);
  }

  [Fact]
  public void InvalidSleepUiKeepsGameUiVisible() {
    ConfigLoadResult result = Parse("layouts: { dream: { showSleepUi: maybe, layers: [ { image: entry } ] } }");

    Assert.True(result.Config.Layouts["dream"].ShowSleepUi);
    Assert.Contains(result.Warnings, warning => warning.Contains("'showSleepUi' must be true or false"));
  }

  [Fact]
  public void ParsesAppearanceDefaults() {
    const string yaml = """
        defaults:
          background: "#10203040"
          tint: "#ABCDEF"
          animation: { enter: fade-in, idle: breathe, exit: fly-out-left, speed: 1.25 }
          tip: { x: -12, bottom: 75, size: 32, color: "#112233" }
          continuePrompt: { x: 8, bottom: 45, size: 18, color: "#445566" }
          separator: { show: false, x: 14, bottom: 120, onLayouts: false }
          spinner: { center: false, x: -5, bottom: 180 }
        """;

    ConfigLoadResult result = Parse(yaml);
    ScreenDefaults defaults = result.Config.Defaults;

    Assert.Empty(result.Warnings);
    Assert.Equal(new ColorValue(0x10, 0x20, 0x30, 0x40), defaults.Background);
    Assert.Equal(new ColorValue(0xAB, 0xCD, 0xEF), defaults.Tint);
    Assert.Equal("fade-in", defaults.Animation.Enter);
    Assert.Equal("breathe", defaults.Animation.Idle);
    Assert.Equal("fly-out-left", defaults.Animation.Exit);
    Assert.Equal(1.25f, defaults.Animation.Speed);
    Assert.Equal(-12f, defaults.Tip.X);
    Assert.Equal(75f, defaults.Tip.Bottom);
    Assert.Equal(32, defaults.Tip.Size);
    Assert.Equal(new ColorValue(0x11, 0x22, 0x33), defaults.Tip.Color);
    Assert.Equal(8f, defaults.ContinuePrompt.X);
    Assert.Equal(45f, defaults.ContinuePrompt.Bottom);
    Assert.Equal(18, defaults.ContinuePrompt.Size);
    Assert.Equal(new ColorValue(0x44, 0x55, 0x66), defaults.ContinuePrompt.Color);
    Assert.False(defaults.Separator.Show);
    Assert.Equal(14f, defaults.Separator.X);
    Assert.Equal(120f, defaults.Separator.Bottom);
    Assert.False(defaults.Separator.OnLayouts);
    Assert.False(defaults.Spinner.Center);
    Assert.Equal(-5f, defaults.Spinner.X);
    Assert.Equal(180f, defaults.Spinner.Bottom);
  }

  [Fact]
  public void MissingAppearanceKeysUseBuiltIns() {
    ConfigLoadResult result = Parse("defaults:\n  place: { fit: contain }\n  tip: { x: 10 }\n");

    Assert.Empty(result.Warnings);
    Assert.Equal(FitMode.Contain, result.Config.Defaults.Fit.Mode);
    AssertBuiltInAppearance(result.Config.Defaults);
    Assert.Equal(10f, result.Config.Defaults.Tip.X);
  }

  [Fact]
  public void InvalidAppearanceValuesWarnWithLinesAndUseBuiltIns() {
    const string yaml = """
        defaults:
          background: wrong
          tint: "#123"
          animation: { speed: 0, extra: true }
          tip: { x: nope, bottom: .nan, size: 65, color: bad, other: 1 }
          separator: { show: maybe, x: false, bottom: {}, onLayouts: null, unknown: 2 }
          spinner: { center: nope, x: .inf, bottom: [] , unknown: 3 }
        """;

    ConfigLoadResult result = Parse(yaml);

    AssertBuiltInAppearance(result.Config.Defaults);
    Assert.Equal(18, result.Warnings.Count);
    Assert.All(result.Warnings, warning => Assert.Contains("line=", warning));
    Assert.Contains(result.Warnings, warning => warning.Contains("animation: unknown key 'extra'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("tip: unknown key 'other'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("separator: unknown key 'unknown'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("spinner: unknown key 'unknown'"));
  }

  [Fact]
  public void InvalidAppearanceMapsWarnAndUseBuiltIns() {
    const string yaml = """
        defaults:
          animation: []
          tip: null
          separator: []
          spinner: wrong
        """;

    ConfigLoadResult result = Parse(yaml);

    AssertBuiltInAppearance(result.Config.Defaults);
    Assert.Equal(4, result.Warnings.Count);
    Assert.All(result.Warnings, warning => Assert.Contains("line=", warning));
  }

  [Fact]
  public void InvalidContinuePromptValuesUseBuiltIns() {
    ConfigLoadResult result = Parse("defaults:\n  continuePrompt: { x: nope, bottom: .nan, size: 65, color: bad, extra: true }\n");

    Assert.Equal(0f, result.Config.Defaults.ContinuePrompt.X);
    Assert.Equal(30f, result.Config.Defaults.ContinuePrompt.Bottom);
    Assert.Equal(20, result.Config.Defaults.ContinuePrompt.Size);
    Assert.Equal(new ColorValue(255, 255, 255), result.Config.Defaults.ContinuePrompt.Color);
    Assert.Equal(5, result.Warnings.Count);
    Assert.Contains(result.Warnings, warning => warning.Contains("continuePrompt: unknown key 'extra'"));
  }

  static void AssertBuiltInAppearance(ScreenDefaults defaults) {
    Assert.Equal(new ColorValue(0, 0, 0), defaults.Background);
    Assert.Equal(new ColorValue(255, 255, 255), defaults.Tint);
    Assert.Equal(90f, defaults.Tip.Bottom);
    Assert.Equal(24, defaults.Tip.Size);
    Assert.Equal(new ColorValue(255, 255, 255), defaults.Tip.Color);
    Assert.Equal(0f, defaults.ContinuePrompt.X);
    Assert.Equal(30f, defaults.ContinuePrompt.Bottom);
    Assert.Equal(20, defaults.ContinuePrompt.Size);
    Assert.Equal(new ColorValue(255, 255, 255), defaults.ContinuePrompt.Color);
    Assert.True(defaults.Separator.Show);
    Assert.Equal(0f, defaults.Separator.X);
    Assert.Equal(150f, defaults.Separator.Bottom);
    Assert.True(defaults.Separator.OnLayouts);
    Assert.True(defaults.Spinner.Center);
    Assert.Equal(0f, defaults.Spinner.X);
    Assert.Equal(200f, defaults.Spinner.Bottom);
  }

  [Fact]
  public void InvalidYamlIsReportedNotThrown() {
    ConfigLoadResult result = Parse("when: [unclosed");

    Assert.True(result.Config.IsEmpty);
    Assert.Single(result.Warnings);
    Assert.Contains("invalid YAML", result.Warnings[0]);
  }

  [Fact]
  public void ParsesFullExample() {
    const string yaml = """
        defaults:
          place: { fit: cover, position: { anchor: center } }

        layouts:
          logo-over-backdrops:
            layers:
              - color: "#000000"
              - id: backdrop
                image: entry
                place: { fade: { sides: [bottom, left], color: "#00000080", size: 25%, curve: smooth } }
              - image: common/logo.png
                animation: none
                place: { fit: { width: 30% }, position: { anchor: top, y: 8% }, opacity: 0.9 }

        when:
          general:
            layout: logo-over-backdrops
            entries:
              - image: general/a.jpg
                text: Skol!
              - general/b.png
          biome:
            mistlands:
              - image: mistlands/fog.jpg
                place: { position: { anchor: right, x: -5% } }
          death:
            Fall: [ death/fall.jpg ]
        """;

    ConfigLoadResult result = Parse(yaml);
    ScreenConfig config = result.Config;

    Assert.Empty(result.Warnings);
    Assert.Equal(FitMode.Cover, config.Defaults.Fit.Mode);
    Assert.Equal(Anchor.Center, config.Defaults.Anchor);

    Layout layout = config.Layouts["Logo-Over-Backdrops"];
    Assert.Equal(3, layout.Layers.Count);
    Assert.Equal(LayerKind.Color, layout.Layers[0].Kind);
    Assert.True(layout.Layers[1].FromEntry);
    Assert.Equal(Sides.Bottom | Sides.Left, layout.Layers[1].Placement.Fade.Sides);
    Assert.Equal(128, layout.Layers[1].Placement.Fade.Color.Value.A);
    Assert.Equal(FadeCurve.Smooth, layout.Layers[1].Placement.Fade.Curve);
    Assert.Equal(new Length(25, LengthUnit.Percent), layout.Layers[1].Placement.Fade.Size);

    Layer logo = layout.Layers[2];
    Assert.Equal(["common/logo.png"], logo.Images);
    Assert.Equal(FitMode.Box, logo.Placement.Fit.Mode);
    Assert.Equal(30f, logo.Placement.Fit.Width.Value.Value);
    Assert.Null(logo.Placement.Fit.Height);
    Assert.Equal(Anchor.Top, logo.Placement.Anchor);
    Assert.Equal(0.9f, logo.Placement.Opacity);
    Assert.Equal("none", logo.Animation.Idle);

    Assert.Equal("logo-over-backdrops", config.General.LayoutName);
    Assert.Equal(2, config.General.Entries.Count);
    Assert.Equal("Skol!", config.General.Entries[0].Text);
    Assert.Equal("general/b.png", config.General.Entries[1].Image);

    Entry fog = config.Biome["Mistlands"].Entries.Single();
    Assert.Equal(Anchor.Right, fog.Place.Anchor);
    Assert.Equal(new Length(-5, LengthUnit.Percent), fog.Place.X);
    Assert.True(config.Biome.ContainsKey("MISTLANDS"));
    Assert.Equal("Mistlands", config.Biome.Keys.Single());

    Assert.Single(config.Death["fall"].Entries);
    Assert.Equal(4, config.EntryCount);
  }

  [Fact]
  public void SkipsMissingUnsafeAndUnsupportedImages() {
    const string yaml = """
        when:
          general:
            - general/missing.jpg
            - ../outside.jpg
            - /absolute.jpg
            - general/a.gif
            - general\a.jpg
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Equal(["general/a.jpg"], result.Config.General.Entries.Select(entry => entry.Image));
    Assert.Equal(4, result.Warnings.Count);
    Assert.Contains(result.Warnings, warning => warning.Contains("not found"));
    Assert.Contains(result.Warnings, warning => warning.Contains("relative"));
    Assert.Contains(result.Warnings, warning => warning.Contains("unsupported"));
  }

  [Fact]
  public void ReportsUnknownNamesAndKeys() {
    const string yaml = """
        colour: red
        when:
          biome:
            Narnia: [ general/a.jpg ]
          death:
            Boredom: [ general/a.jpg ]
            Fall: [ death/fall.jpg ]
          weather: []
        """;

    ConfigLoadResult result = Parse(yaml, ["Fall", "Drowning"]);

    Assert.True(result.Config.Biome.ContainsKey("Narnia"));
    Assert.Equal(["Fall"], result.Config.Death.Keys);
    Assert.Contains(result.Warnings, warning => warning.Contains("'colour'"));
    Assert.DoesNotContain(result.Warnings, warning => warning.Contains("'Narnia'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("'Boredom'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("'weather'"));
    Assert.All(result.Warnings, warning => Assert.StartsWith("Configuration warning. file=loadingscreen.yaml line=", warning));
  }

  [Fact]
  public void AcceptsAnyCauseWhenCauseListIsUnknown() {
    ConfigLoadResult result = Parse("when: { death: { Whatever: [ death/fall.jpg ] } }");

    Assert.Empty(result.Warnings);
    Assert.True(result.Config.Death.ContainsKey("whatever"));
  }

  [Fact]
  public void BiomeListsParseAndLegacyTeleportIsUnknown() {
    ConfigLoadResult result = Parse("""
        when:
          biome:
            mistlands: [ mistlands/fog.jpg ]
          teleport:
            Meadows: [ general/a.jpg ]
        """);

    Assert.Equal(["Mistlands"], result.Config.Biome.Keys);
    Assert.Equal("biome.Mistlands", result.Config.Biome["Mistlands"].Name);
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown situation 'teleport'"));
    Assert.Single(result.Warnings);
  }

  [Fact]
  public void UnknownLayoutFallsBackToDefault() {
    const string yaml = """
        when:
          general:
            layout: nope
            entries:
              - { image: general/a.jpg, layout: also-nope }
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Null(result.Config.General.LayoutName);
    Assert.Null(result.Config.General.Entries[0].LayoutName);
    Assert.Equal(2, result.Warnings.Count(warning => warning.Contains("unknown layout")));
  }

  [Fact]
  public void InvalidValuesAreReportedAndDropped() {
    const string yaml = """
        defaults:
          place:
            fit: squash
            position: { anchor: middle, x: 10em }
            opacity: 2
            fade: { sides: [inside], color: red }
            animate: maybe
        layouts:
          broken:
            layers:
              - color: blue
              - image: common/logo.png
                text: caption
              - id: nothing
          ok:
            layers:
              - image: common/bg.jpeg
                place: { fit: { width: -10px, height: 50% } }
        """;

    ConfigLoadResult result = Parse(yaml);
    Placement defaults = result.Config.Defaults;

    Assert.True(defaults.IsEmpty);
    Assert.False(result.Config.Layouts.ContainsKey("broken"));

    Layer layer = result.Config.Layouts["ok"].Layers.Single();
    Assert.Null(layer.Placement.Fit.Width);
    Assert.Equal(50f, layer.Placement.Fit.Height.Value.Value);

    Assert.Contains(result.Warnings, warning => warning.Contains("squash"));
    Assert.Contains(result.Warnings, warning => warning.Contains("middle"));
    Assert.Contains(result.Warnings, warning => warning.Contains("10em"));
    Assert.Contains(result.Warnings, warning => warning.Contains("opacity"));
    Assert.Contains(result.Warnings, warning => warning.Contains("inside"));
    Assert.Contains(result.Warnings, warning => warning.Contains("animate"));
    Assert.Contains(result.Warnings, warning => warning.Contains("negative"));
    Assert.Contains(result.Warnings, warning => warning.Contains("no 'image: entry'"));
  }

  [Fact]
  public void MergeTakesMostSpecificValue() {
    Placement entry = new() { X = new Length(5, LengthUnit.Pixels) };
    Placement layer = new() { X = new Length(1, LengthUnit.Pixels), Anchor = Anchor.Left };
    Placement defaults = new() { Fit = new Fit { Mode = FitMode.Contain }, Anchor = Anchor.Center };

    Placement merged = Placement.Merge(entry, layer, null, defaults);

    Assert.Equal(5f, merged.X.Value.Value);
    Assert.Equal(Anchor.Left, merged.Anchor);
    Assert.Equal(FitMode.Contain, merged.Fit.Mode);
  }

  [Theory]
  [InlineData("10", 10f, LengthUnit.Pixels)]
  [InlineData("10px", 10f, LengthUnit.Pixels)]
  [InlineData(" -2.5% ", -2.5f, LengthUnit.Percent)]
  public void ParsesLengths(string text, float value, LengthUnit unit) {
    Assert.True(Length.TryParse(text, out Length length));
    Assert.Equal(value, length.Value);
    Assert.Equal(unit, length.Unit);
  }

  [Theory]
  [InlineData("#112233", 0x11, 0x22, 0x33, 0xFF)]
  [InlineData("11223344", 0x11, 0x22, 0x33, 0x44)]
  public void ParsesColors(string text, int r, int g, int b, int a) {
    Assert.True(ColorValue.TryParse(text, out ColorValue color));
    Assert.Equal(new ColorValue((byte) r, (byte) g, (byte) b, (byte) a), color);
  }

  [Fact]
  public void ParsesEnemyListsAndOrder() {
    const string yaml = """
        order: sequential
        when:
          general:
            order: random
            entries: [ general/a.jpg ]
          enemy:
            Dragon:
              - text: Drakes spit frost.
            Modded_Wisp:
              - text: A modded creature.
          death:
            Fall: [ death/fall.jpg ]
        """;

    ConfigLoadResult result = Parse(yaml, [.. DeathCauses.Names]);

    Assert.Empty(result.Warnings);
    Assert.Equal(PickOrder.Sequential, result.Config.Order);
    Assert.Equal(PickOrder.Random, result.Config.General.Order);
    Assert.Equal("enemy.Dragon", result.Config.Enemy["dragon"].Name);
    Assert.Single(result.Config.Enemy["Modded_Wisp"].Entries);
    Assert.Contains("Fall", result.Config.Death.Keys);
  }

  [Fact]
  public void RejectsUnknownOrder() {
    ConfigLoadResult result = Parse("order: shuffled");

    Assert.Contains(result.Warnings, warning => warning.Contains("use 'random' or 'sequential'"));
    Assert.Equal(PickOrder.Random, result.Config.Order);
  }

  [Fact]
  public void ParsesGameSeparatorLayer() {
    const string yaml = """
        layouts:
          lined:
            layers:
              - image: entry
              - image: separator
                color: "#FFFFFF80"
                place: { fit: { width: 40% }, position: { anchor: bottom, y: -20% } }
        when:
          general:
            layout: lined
            entries: [ general/a.jpg ]
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Empty(result.Warnings);
    Layout layout = result.Config.Layouts["lined"];
    Layer separator = layout.Layers[1];
    Assert.True(layout.ShowsSeparator);
    Assert.True(separator.GameSeparator);
    Assert.Equal(LayerKind.Image, separator.Kind);
    Assert.True(separator.HasTint);
    Assert.Equal(Anchor.Bottom, separator.Placement.Anchor);
  }

  [Theory]
  [InlineData("hammer", SpinnerStyle.Hammer)]
  [InlineData("ring", SpinnerStyle.Ring)]
  [InlineData("dots", SpinnerStyle.Dots)]
  public void ParsesSpinnerStylesWithPlacementAndDefaultSpin(string style, SpinnerStyle expected) {
    string yaml = """
        layouts:
          loading:
            layers:
              - spinner: STYLE
                place: { fit: { width: 48px }, position: { anchor: bottom, y: -12% } }
              - image: entry
        """.Replace("STYLE", style);

    ConfigLoadResult result = Parse(yaml);

    Assert.Empty(result.Warnings);
    Layer spinner = result.Config.Layouts["loading"].Layers[0];
    Assert.Equal(LayerKind.Spinner, spinner.Kind);
    Assert.Equal(expected, spinner.SpinnerStyle);
    Assert.Equal("spin", spinner.Animation.Idle);
    Assert.Equal(Anchor.Bottom, spinner.Placement.Anchor);
    Assert.Equal(48f, spinner.Placement.Fit.Width.Value.Value);
  }

  [Fact]
  public void ParsesCustomSpinnerImageAndAnimationOverride() {
    const string yaml = """
        layouts:
          loading:
            layers:
              - spinner: common/indicator.png
                animation: pulse
              - image: entry
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Empty(result.Warnings);
    Layer spinner = result.Config.Layouts["loading"].Layers[0];
    Assert.Equal(SpinnerStyle.Image, spinner.SpinnerStyle);
    Assert.Equal(["common/indicator.png"], spinner.Images);
    Assert.Equal("pulse", spinner.Animation.Idle);
  }

  [Fact]
  public void RejectsInvalidAndMixedSpinnerContent() {
    const string yaml = """
        layouts:
          loading:
            layers:
              - spinner: unknown
              - spinner: ring
                image: entry
              - spinner: dots
                text: caption
              - spinner: hammer
              - image: entry
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Contains(result.Warnings, warning => warning.Contains("unsupported image type 'unknown'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("use only one of"));
    Assert.All(result.Warnings, warning => Assert.Contains("line=", warning));
    Assert.Equal(2, result.Config.Layouts["loading"].Layers.Count);
  }

  [Fact]
  public void LegacySpinnerDefaultsTranslateToGameStartLayer() {
    ConfigLoadResult result = Parse("defaults: { spinner: { center: true, x: -5, bottom: 180 } }");

    Layer layer = result.Config.Defaults.Spinner.ToLayer();
    Assert.Equal(LayerKind.Spinner, layer.Kind);
    Assert.Equal(SpinnerStyle.Hammer, layer.SpinnerStyle);
    Assert.Equal("spin", layer.Animation.Idle);
    Assert.Equal(Anchor.Bottom, layer.Placement.Anchor);
    Assert.Equal(-5f, layer.Placement.X.Value.Value);
    Assert.Equal(-180f, layer.Placement.Y.Value.Value);
    Assert.Null(Parse("defaults: { spinner: { center: false } }").Config.Defaults.Spinner.ToLayer());
  }

  [Fact]
  public void RejectsMixedLayerContentAndOldKeys() {
    const string yaml = """
        layouts:
          broken:
            layers:
              - separator: fancy
              - image: common/logo.png
                text: caption
              - image: entry
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'separator'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("use only one of"));
    Assert.False(result.Config.Layouts["broken"].ShowsSeparator);
  }

  [Fact]
  public void FormerLayerAndNestedEnemyKeysWarnAsUnknown() {
    const string yaml = """
        layouts:
          old:
            layers:
              - source: entry
              - images: [common/logo.png]
              - image: entry
        when:
          death:
            enemy: { Troll: [death/fall.jpg] }
        """;

    ConfigLoadResult result = Parse(yaml, DeathCauses.Names.ToArray());

    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'source'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'images'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown death name 'enemy'"));
    Assert.Empty(result.Config.Enemy);
  }

  [Fact]
  public void ImageAcceptsKeywordsPathsAndPathLists() {
    const string yaml = """
        layouts:
          mixed:
            layers:
              - image: entry
              - image: separator
                color: "#FFFFFF80"
              - image: [common/logo.png, common/bg.jpeg]
              - image: common/logo.png
              - image: entry.png
              - image: separator.png
        """;

    ConfigLoadResult result = Parse(yaml);
    Layer[] layers = [.. result.Config.Layouts["mixed"].Layers];

    Assert.Empty(result.Warnings);
    Assert.True(layers[0].FromEntry);
    Assert.True(layers[1].GameSeparator);
    Assert.True(layers[1].HasTint);
    Assert.Equal(["common/logo.png", "common/bg.jpeg"], layers[2].Images);
    Assert.Equal(["common/logo.png"], layers[3].Images);
    Assert.Equal(["entry.png"], layers[4].Images);
    Assert.Equal(["separator.png"], layers[5].Images);
  }

  [Fact]
  public void PlaceWorksAtEveryScopeAndFlatKeysAreUnknown() {
    const string yaml = """
        defaults:
          place: { fit: contain }
          fit: cover
        layouts:
          placed:
            place: { position: top }
            opacity: 0.5
            layers:
              - image: entry
                place: { opacity: 0.7 }
                animation: none
        when:
          general:
            layout: placed
            entries:
              - image: general/a.jpg
                place: { animate: true }
        """;

    ConfigLoadResult result = Parse(yaml);

    Assert.Equal(FitMode.Contain, result.Config.Defaults.Fit.Mode);
    Assert.Equal(Anchor.Top, result.Config.Layouts["placed"].Placement.Anchor);
    Assert.Equal(0.7f, result.Config.Layouts["placed"].Layers[0].Placement.Opacity);
    Assert.Equal("none", result.Config.Layouts["placed"].Layers[0].Animation.Idle);
    Assert.Equal(3, result.Warnings.Count);
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'fit'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'opacity'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("unknown key 'animate'"));
  }

  [Fact]
  public void GenericDefaultsKeepValidFieldsAndWarnForInvalidAndUnknownFields() {
    const string yaml = """
        defaults:
          animation: { speed: 0, surprise: 1 }
          tip: { x: 4, size: 65, color: bad, extra: true }
          continuePrompt: { size: 18, bottom: .nan }
          separator: { show: false, onLayouts: wrong }
          spinner: { center: false, x: .inf }
        """;

    ConfigLoadResult result = Parse(yaml);
    ScreenDefaults defaults = result.Config.Defaults;

    Assert.Null(defaults.Animation.Speed);
    Assert.Equal(4f, defaults.Tip.X);
    Assert.Equal(24, defaults.Tip.Size);
    Assert.Equal(18, defaults.ContinuePrompt.Size);
    Assert.Equal(30f, defaults.ContinuePrompt.Bottom);
    Assert.False(defaults.Separator.Show);
    Assert.True(defaults.Separator.OnLayouts);
    Assert.False(defaults.Spinner.Center);
    Assert.Equal(0f, defaults.Spinner.X);
    Assert.Equal(8, result.Warnings.Count);
    Assert.All(result.Warnings, warning => Assert.Contains("line=", warning));
  }
}

public class ExampleConfigTests {
  [Fact]
  public void ExampleIsEmptyAsShipped() {
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(ExampleConfigText.Text);

    Assert.True(result.Config.IsEmpty);
    Assert.Empty(result.Warnings);
  }

  [Fact]
  public void UncommentedExampleParsesWithoutWarnings() {
    string[] paragraphs = ExampleConfigText.Text.Replace("\r\n", "\n").Split("\n\n");
    string yaml = string.Join(
        "\n",
        paragraphs.Skip(1).SelectMany(paragraph => paragraph.Split('\n'))
            .Select(line => line.StartsWith("# ") ? line.Substring(2) : line));

    ConfigLoadResult result = new ConfigParser(_ => true, DeathCauses.Names).Parse(yaml);

    Assert.Empty(result.Warnings);
    Assert.Equal(2, result.Config.Layouts.Count);
    Assert.Equal(8, result.Config.EntryCount);
  }
}

public class TextLayerTests {
  [Fact]
  public void ParsesTextLayer() {
    const string yaml = """
        layouts:
          captioned:
            layers:
              - image: entry
              - text: caption
                size: 32
                color: "#FFFFFF"
                align: left
                background: "#00000080"
                place: { fit: { width: 40% }, position: { anchor: bottom-left, x: 5%, y: -5% } }
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    Layer text = result.Config.Layouts["captioned"].Layers[1];

    Assert.Empty(result.Warnings);
    Assert.Equal(LayerKind.Text, text.Kind);
    Assert.Equal(32f, text.Text.FontSize);
    Assert.Equal(TextAlign.Left, text.Text.Align);
    Assert.Equal(128, text.Text.Background.Value.A);
    Assert.Equal(Anchor.BottomLeft, text.Placement.Anchor);
    Assert.True(result.Config.Layouts["captioned"].ShowsText);
  }

  [Fact]
  public void RejectsInvalidTextLayer() {
    const string yaml = """
        layouts:
          broken:
            layers:
              - image: entry
              - text: weather
              - text: caption
                image: a.png
              - text: caption
                place: { fit: cover }
                size: -3
              - image: a.png
                align: left
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);
    Layout layout = result.Config.Layouts["broken"];

    Assert.Equal(3, layout.Layers.Count);
    Assert.Null(layout.Layers[1].Placement.Fit);
    Assert.Null(layout.Layers[1].Text.FontSize);
    Assert.Contains(result.Warnings, warning => warning.Contains("'caption'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("only one of"));
    Assert.Contains(result.Warnings, warning => warning.Contains("text width"));
    Assert.Contains(result.Warnings, warning => warning.Contains("positive font size"));
    Assert.Contains(result.Warnings, warning => warning.Contains("only to text layers"));
  }
}

public class YamlLimitsTests {
  [Fact]
  public void RejectsDeepNesting() {
    string yaml = "when: " + new string('[', 5000) + new string(']', 5000);
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.False(result.IsValid);
    Assert.Contains(result.Warnings, warning => warning.Contains("nested deeper"));
  }

  [Fact]
  public void RejectsAliases() {
    const string yaml = """
        when:
          general: &list [ a.jpg ]
          death: { Fall: *list }
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.False(result.IsValid);
    Assert.Contains(result.Warnings, warning => warning.Contains("aliases"));
  }

  [Fact]
  public void LegacyAnimateAndZoomKeysMapToIdleAnimation() {
    const string yaml = """
        defaults:
          zoom: { enabled: true, amount: 1.05, seconds: 30 }
        layouts:
          main:
            layers:
              - id: still
                image: entry
                place: { fit: cover, animate: false }
              - id: moving
                image: common/logo.png
                place: { animate: true }
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.Empty(result.Warnings);
    Assert.Equal(0.5f, result.Config.Defaults.Animation.Speed);
    List<Layer> layers = result.Config.Layouts["main"].Layers;
    Assert.Equal("none", layers[0].Animation.Idle);
    Assert.Equal("zoom-in", layers[1].Animation.Idle);
  }

  [Fact]
  public void LegacyZoomDisabledTurnsDefaultIdleOff() {
    const string yaml = """
        defaults:
          zoom: { enabled: false }
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.Empty(result.Warnings);
    Assert.Equal("none", result.Config.Defaults.Animation.Idle);
  }
}
