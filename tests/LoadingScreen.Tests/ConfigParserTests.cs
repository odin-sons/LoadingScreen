namespace LoadingScreen.Tests;

using System.Collections.Generic;
using System.Linq;

using LoadingScreen.Screens;

using Xunit;

public class ConfigParserTests {
  static readonly HashSet<string> _files = [
    "general/a.jpg", "general/b.png", "mistlands/fog.jpg", "death/fall.jpg", "common/logo.png", "common/bg.jpeg"
  ];

  static ConfigLoadResult Parse(string yaml, ICollection<string> causes = null) {
    return new ConfigParser(path => _files.Contains(path), causes).Parse(yaml);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   \n# only a comment\n")]
  public void EmptyConfigIsEmptyWithoutWarnings(string yaml) {
    ConfigLoadResult result = Parse(yaml);

    Assert.True(result.Config.IsEmpty);
    Assert.Empty(result.Warnings);
  }

  [Fact]
  public void InvalidYamlIsReportedNotThrown() {
    ConfigLoadResult result = Parse("situations: [unclosed");

    Assert.True(result.Config.IsEmpty);
    Assert.Single(result.Warnings);
    Assert.Contains("invalid YAML", result.Warnings[0]);
  }

  [Fact]
  public void ParsesFullExample() {
    const string yaml = """
        defaults:
          fit: cover
          position: { anchor: center }

        layouts:
          logo-over-backdrops:
            layers:
              - color: "#000000"
              - id: backdrop
                source: entry
                fade: { sides: [bottom, left], color: "#00000080", size: 25%, curve: smooth }
              - image: common/logo.png
                fit: { width: 30% }
                position: { anchor: top, y: 8% }
                opacity: 0.9
                animate: false

        situations:
          general:
            layout: logo-over-backdrops
            entries:
              - image: general/a.jpg
                text: Skol!
              - general/b.png
          teleport:
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
    Assert.False(logo.Placement.Animate);

    Assert.Equal("logo-over-backdrops", config.General.LayoutName);
    Assert.Equal(2, config.General.Entries.Count);
    Assert.Equal("Skol!", config.General.Entries[0].Text);
    Assert.Equal("general/b.png", config.General.Entries[1].Image);

    Entry fog = config.Teleport["Mistlands"].Entries.Single();
    Assert.Equal(Anchor.Right, fog.Place.Anchor);
    Assert.Equal(new Length(-5, LengthUnit.Percent), fog.Place.X);
    Assert.True(config.Teleport.ContainsKey("MISTLANDS"));
    Assert.Equal("Mistlands", config.Teleport.Keys.Single());

    Assert.Single(config.Death["fall"].Entries);
    Assert.Equal(4, config.EntryCount);
  }

  [Fact]
  public void SkipsMissingUnsafeAndUnsupportedImages() {
    const string yaml = """
        situations:
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
        situations:
          teleport:
            Narnia: [ general/a.jpg ]
          death:
            Boredom: [ general/a.jpg ]
            Fall: [ death/fall.jpg ]
          weather: []
        """;

    ConfigLoadResult result = Parse(yaml, ["Fall", "Drowning"]);

    Assert.Empty(result.Config.Teleport);
    Assert.Equal(["Fall"], result.Config.Death.Keys);
    Assert.Contains(result.Warnings, warning => warning.Contains("'colour'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("'Narnia'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("'Boredom'"));
    Assert.Contains(result.Warnings, warning => warning.Contains("'weather'"));
    Assert.All(result.Warnings, warning => Assert.StartsWith("line ", warning));
  }

  [Fact]
  public void AcceptsAnyCauseWhenCauseListIsUnknown() {
    ConfigLoadResult result = Parse("situations: { death: { Whatever: [ death/fall.jpg ] } }");

    Assert.Empty(result.Warnings);
    Assert.True(result.Config.Death.ContainsKey("whatever"));
  }

  [Fact]
  public void UnknownLayoutFallsBackToDefault() {
    const string yaml = """
        situations:
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
                source: entry
              - id: nothing
          ok:
            layers:
              - image: common/bg.jpeg
                fit: { width: -10px, height: 50% }
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
    Assert.Contains(result.Warnings, warning => warning.Contains("no 'source: entry'"));
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
    Assert.Equal(5, result.Config.EntryCount);
  }
}

public class TextLayerTests {
  [Fact]
  public void ParsesTextLayer() {
    const string yaml = """
        layouts:
          captioned:
            layers:
              - source: entry
              - text: caption
                size: 32
                color: "#FFFFFF"
                align: left
                background: "#00000080"
                fit: { width: 40% }
                position: { anchor: bottom-left, x: 5%, y: -5% }
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
              - source: entry
              - text: weather
              - text: caption
                image: a.png
              - text: caption
                fit: cover
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
    string yaml = "situations: " + new string('[', 5000) + new string(']', 5000);
    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.False(result.IsValid);
    Assert.Contains(result.Warnings, warning => warning.Contains("nested deeper"));
  }

  [Fact]
  public void RejectsAliases() {
    const string yaml = """
        situations:
          general: &list [ a.jpg ]
          death: { Fall: *list }
        """;

    ConfigLoadResult result = new ConfigParser(_ => true).Parse(yaml);

    Assert.False(result.IsValid);
    Assert.Contains(result.Warnings, warning => warning.Contains("aliases"));
  }
}
