namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public enum LayerKind {
  Image,
  Color,
  Text
}

public enum TextAlign {
  Left,
  Center,
  Right
}

public sealed class TextStyle {
  public float? FontSize { get; set; }
  public ColorValue? Color { get; set; }
  public TextAlign? Align { get; set; }
  public ColorValue? Background { get; set; }
}

public sealed class Layer {
  public string Id { get; set; }
  public LayerKind Kind { get; set; }
  public bool FromEntry { get; set; }
  public bool GameSeparator { get; set; }
  public bool HasTint { get; set; }
  public List<string> Images { get; } = [];
  public ColorValue Color { get; set; }
  public TextStyle Text { get; set; }
  public Placement Placement { get; set; } = new();
}

public sealed class Layout {
  public string Name { get; set; }
  public string Sound { get; set; }
  public bool ShowSleepUi { get; set; } = true;
  public VignetteSettings Vignette { get; set; }
  public MaskSettings Mask { get; set; }
  public List<Layer> Layers { get; } = [];
  public Placement Placement { get; set; } = new();

  public bool ShowsEntryImage => Layers.Any(layer => layer.FromEntry);

  public bool ShowsText => Layers.Any(layer => layer.Kind == LayerKind.Text);

  public bool ShowsSeparator => Layers.Any(layer => layer.GameSeparator);
}

public sealed class Entry {
  public string Image { get; set; }
  public string Sound { get; set; }
  public string Voice { get; set; }
  public string Text { get; set; }
  public string LayoutName { get; set; }
  public Placement Place { get; set; } = new();
  public List<Condition> Conditions { get; } = [];
}

public enum PickOrder {
  Random,
  Sequential
}

public sealed class EntryList {
  public string Name { get; set; }
  public string Sound { get; set; }
  public string LayoutName { get; set; }
  public PickOrder? Order { get; set; }
  public List<Entry> Entries { get; } = [];
}

public sealed class ZoomDefaults {
  public bool Enabled { get; set; } = true;
  public float Amount { get; set; } = 1.05f;
  public float Seconds { get; set; } = 15f;
}

public sealed class TipDefaults {
  public float X { get; set; }
  public float Bottom { get; set; } = 90f;
  public int Size { get; set; } = 24;
  public ColorValue Color { get; set; } = new(255, 255, 255);
}

public sealed class SeparatorDefaults {
  public bool Show { get; set; } = true;
  public float X { get; set; }
  public float Bottom { get; set; } = 150f;
  public bool OnLayouts { get; set; } = true;
}

public sealed class SpinnerDefaults {
  public bool Center { get; set; } = true;
  public float X { get; set; }
  public float Bottom { get; set; } = 200f;
}

public sealed class ScreenDefaults : Placement {
  public ColorValue Background { get; set; } = new(0, 0, 0);
  public ColorValue Tint { get; set; } = new(255, 255, 255);
  public VignetteSettings Vignette { get; set; }
  public MaskSettings Mask { get; set; }
  public ZoomDefaults Zoom { get; } = new();
  public TipDefaults Tip { get; } = new();
  public SeparatorDefaults Separator { get; } = new();
  public SpinnerDefaults Spinner { get; } = new();
}

public sealed class ScreenConfig {
  public ScreenDefaults Defaults { get; set; } = new();
  public PickOrder Order { get; set; } = PickOrder.Random;
  public Dictionary<string, Layout> Layouts { get; } = new(StringComparer.OrdinalIgnoreCase);
  public EntryList General { get; set; } = new() { Name = "general" };
  public EntryList Sleep { get; set; } = new() { Name = "sleep" };
  public Dictionary<string, EntryList> Teleport { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, EntryList> Death { get; } = new(StringComparer.OrdinalIgnoreCase);

  public IEnumerable<EntryList> AllLists => new[] { General, Sleep }.Concat(Teleport.Values).Concat(Death.Values);

  public int EntryCount => AllLists.Sum(list => list.Entries.Count);

  public bool IsEmpty => EntryCount == 0;
}

public static class KnownNames {
  public static readonly IReadOnlyList<string> Biomes = [
    "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean"
  ];

  public static readonly IReadOnlyList<string> ImageExtensions = [".png", ".jpg", ".jpeg"];
  public static readonly IReadOnlyList<string> AudioExtensions = [".ogg", ".wav"];
}
