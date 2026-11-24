namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public enum LayerKind {
  Image,
  Color,
  Text,
  Spinner,
  Progress
}

public enum ProgressStyle {
  Bar,
  Ring,
  Percent
}

public enum SpinnerStyle {
  Hammer,
  Ring,
  Dots,
  Image
}

public enum TextAlign {
  Left,
  Center,
  Right
}

public enum TextSource {
  Caption,
  Prompt
}

public sealed class TextStyle {
  [SchemaField("size", "Font size in points.", Minimum = 0, ExclusiveMinimum = true)]
  public float? FontSize { get; set; }
  [SchemaField("color", "Text color or auto.", Form = "auto-color")]
  public ColorValue? Color { get; set; }
  public bool AutoColor { get; set; }
  [SchemaField("align", "Horizontal text alignment.", EnumType = typeof(TextAlign))]
  public TextAlign? Align { get; set; }
  [SchemaField("background", "Text background color or auto.", Form = "auto-color")]
  public ColorValue? Background { get; set; }
  public bool AutoBackground { get; set; }
}

public sealed class Layer {
  [SchemaField("id", "Optional layer identifier.")]
  public string Id { get; set; }
  public LayerKind Kind { get; set; }
  public bool FromEntry { get; set; }
  public bool GameSeparator { get; set; }
  public bool HasTint { get; set; }
  [SchemaField("spinner", "Built-in spinner style or relative image path.", Form = "spinner", LayerKind = true, LayerKindOrder = 2)]
  public SpinnerStyle SpinnerStyle { get; set; }
  [SchemaField("progress", "Progress indicator style.", EnumType = typeof(ProgressStyle), LayerKind = true, LayerKindOrder = 3)]
  public ProgressStyle ProgressStyle { get; set; }
  [SchemaField("size", "Progress size or text font size.", Minimum = 0, ExclusiveMinimum = true)]
  public float? ProgressSize { get; set; }
  [SchemaField("animation", "Animation preset or phase mapping.", Form = "animation")]
  public AnimationSpec Animation { get; set; }
  [SchemaField("image", "Image path, entry, separator, or list of paths.", Form = "layer-image", LayerKind = true, LayerKindOrder = 0)]
  public List<string> Images { get; } = [];
  [SchemaField("color", "Layer color or image tint.", Form = "auto-color", LayerKind = true, LayerKindOrder = 4)]
  public ColorValue Color { get; set; }
  [SchemaField("text", "Caption or prompt text source.", Form = "text-source", LayerKind = true, LayerKindOrder = 1)]
  public TextStyle Text { get; set; }
  public TextSource TextSource { get; set; }
  [SchemaField("place", "Layer placement.")]
  public Placement Placement { get; set; } = new();
}

public sealed class Layout {
  public string Name { get; set; }
  [SchemaField("sound", "Relative audio path.")]
  public string Sound { get; set; }
  [SchemaField("showSleepUi", "Show the original sleep UI.")]
  public bool ShowSleepUi { get; set; } = true;
  [SchemaField("vignette", "Vignette effect.")]
  public VignetteSettings Vignette { get; set; }
  [SchemaField("mask", "Color mask effect.")]
  public MaskSettings Mask { get; set; }
  [SchemaField("layers", "Ordered screen layers.")]
  public List<Layer> Layers { get; } = [];
  [SchemaField("place", "Layout placement.")]
  public Placement Placement { get; set; } = new();

  public bool ShowsEntryImage => Layers.Any(layer => layer.FromEntry);

  public bool ShowsText => Layers.Any(layer => layer.Kind == LayerKind.Text);

  public bool ShowsSeparator => Layers.Any(layer => layer.GameSeparator);

  public bool ShowsSpinner => Layers.Any(layer => layer.Kind == LayerKind.Spinner);
}

public sealed class Entry {
  [SchemaField("image", "Relative image path.")]
  public string Image { get; set; }
  [SchemaField("sound", "Relative audio path.")]
  public string Sound { get; set; }
  [SchemaField("voice", "Relative voice path.")]
  public string Voice { get; set; }
  [SchemaField("text", "Text or language-to-text mapping.", Form = "translated-text")]
  public string Text { get; set; }
  public Dictionary<string, string> Translations { get; } = new(StringComparer.OrdinalIgnoreCase);
  [SchemaField("layout", "Layout name.")]
  public string LayoutName { get; set; }
  [SchemaField("place", "Entry placement.")]
  public Placement Place { get; set; } = new();
  [SchemaField("when", "Conditions that must all hold.", Form = "conditions")]
  public List<Condition> Conditions { get; } = [];

  public string GetText(string code) {
    if (code != null && Translations.TryGetValue(code, out string localized)) return localized;
    if (Translations.TryGetValue("en", out string english)) return english;
    return Text ?? Translations.Values.FirstOrDefault();
  }
}

public enum PickOrder {
  Random,
  Sequential
}

public sealed class EntryList {
  public string Name { get; set; }
  [SchemaField("sound", "Relative audio path.")]
  public string Sound { get; set; }
  [SchemaField("layout", "Layout name.")]
  public string LayoutName { get; set; }
  [SchemaField("order", "Entry selection order.", EnumType = typeof(PickOrder))]
  public PickOrder? Order { get; set; }
  [SchemaField("entries", "Entries in this list.")]
  public List<Entry> Entries { get; } = [];
}

public sealed class TipDefaults {
  [SchemaField("x", "Horizontal offset.")]
  public float X { get; set; }
  [SchemaField("bottom", "Distance from bottom.")]
  public float Bottom { get; set; } = 90f;
  [SchemaField("size", "Text size.", Minimum = 0, Maximum = 64)]
  public int Size { get; set; } = 24;
  [SchemaField("color", "Text color.")]
  public ColorValue Color { get; set; } = new(255, 255, 255);
}

public sealed class ContinuePromptDefaults {
  [SchemaField("x", "Horizontal offset.")]
  public float X { get; set; }
  [SchemaField("bottom", "Distance from bottom.")]
  public float Bottom { get; set; } = 30f;
  [SchemaField("size", "Text size.", Minimum = 0, Maximum = 64)]
  public int Size { get; set; } = 20;
  [SchemaField("color", "Text color.")]
  public ColorValue Color { get; set; } = new(255, 255, 255);
}

public sealed class SeparatorDefaults {
  [SchemaField("show", "Show the separator.")]
  public bool Show { get; set; } = true;
  [SchemaField("x", "Horizontal offset.")]
  public float X { get; set; }
  [SchemaField("bottom", "Distance from bottom.")]
  public float Bottom { get; set; } = 150f;
  [SchemaField("onLayouts", "Show on custom layouts.")]
  public bool OnLayouts { get; set; } = true;
}

public sealed class SpinnerDefaults {
  [SchemaField("center", "Center the spinner.")]
  public bool Center { get; set; } = true;
  [SchemaField("x", "Horizontal offset.")]
  public float X { get; set; }
  [SchemaField("bottom", "Distance from bottom.")]
  public float Bottom { get; set; } = 200f;
}

[SchemaField("place", "Default placement.", ValueType = typeof(Placement))]
[SchemaField("zoom", "Legacy zoom settings.", Form = "legacy-zoom")]
public sealed class ScreenDefaults : Placement {
  [SchemaField("background", "Background color.")]
  public ColorValue Background { get; set; } = new(0, 0, 0);
  [SchemaField("tint", "Image tint.")]
  public ColorValue Tint { get; set; } = new(255, 255, 255);
  [SchemaField("vignette", "Default vignette.")]
  public VignetteSettings Vignette { get; set; }
  [SchemaField("mask", "Default color mask.")]
  public MaskSettings Mask { get; set; }
  [SchemaField("animation", "Default animation.", Form = "animation")]
  public AnimationSpec Animation { get; set; }
  public AnimationCatalog Animations { get; set; } = new();
  [SchemaField("tip", "Tip appearance.")]
  public TipDefaults Tip { get; } = new();
  [SchemaField("continuePrompt", "Continue prompt appearance.")]
  public ContinuePromptDefaults ContinuePrompt { get; } = new();
  [SchemaField("separator", "Separator appearance.")]
  public SeparatorDefaults Separator { get; } = new();
  [SchemaField("spinner", "Spinner appearance.")]
  public SpinnerDefaults Spinner { get; } = new();
}

[SchemaField("when", "Situation-based entry lists.", Form = "when")]
public sealed class ScreenConfig {
  [SchemaField("defaults", "Default appearance.")]
  public ScreenDefaults Defaults { get; set; } = new();
  [SchemaField("order", "Global entry selection order.", EnumType = typeof(PickOrder))]
  public PickOrder Order { get; set; } = PickOrder.Random;
  [SchemaField("layouts", "Named layouts.", Form = "layout-map")]
  public Dictionary<string, Layout> Layouts { get; } = new(StringComparer.OrdinalIgnoreCase);
  public EntryList General { get; set; } = new() { Name = "general" };
  public EntryList Sleep { get; set; } = new() { Name = "sleep" };
  public Dictionary<string, EntryList> Biome { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, EntryList> Death { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, EntryList> Enemy { get; } = new(StringComparer.OrdinalIgnoreCase);

  public IEnumerable<EntryList> AllLists => new[] { General, Sleep }.Concat(Biome.Values).Concat(Death.Values).Concat(Enemy.Values);

  public int EntryCount => AllLists.Sum(list => list.Entries.Count);

  public bool IsEmpty => EntryCount == 0;
}

public static class KnownNames {
  public static readonly IReadOnlyList<string> Biomes = [
    "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean"
  ];

  public static readonly IReadOnlyList<string> GlobalKeys = [
    "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking",
    "defeated_queen", "defeated_fader", "KilledTroll", "killed_surtling", "KilledBat"
  ];

  public static readonly IReadOnlyList<string> ImageExtensions = [".png", ".jpg", ".jpeg"];
  public static readonly IReadOnlyList<string> AudioExtensions = [".ogg", ".wav"];
}
