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
  public List<Layer> Layers { get; } = [];
  public Placement Placement { get; set; } = new();

  public bool ShowsEntryImage => Layers.Any(layer => layer.FromEntry);

  public bool ShowsText => Layers.Any(layer => layer.Kind == LayerKind.Text);

  public bool ShowsSeparator => Layers.Any(layer => layer.GameSeparator);
}

public sealed class Entry {
  public string Image { get; set; }
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
  public string LayoutName { get; set; }
  public PickOrder? Order { get; set; }
  public List<Entry> Entries { get; } = [];
}

public sealed class ScreenConfig {
  public Placement Defaults { get; set; } = new();
  public PickOrder Order { get; set; } = PickOrder.Random;
  public Dictionary<string, Layout> Layouts { get; } = new(StringComparer.OrdinalIgnoreCase);
  public EntryList General { get; set; } = new() { Name = "general" };
  public Dictionary<string, EntryList> Teleport { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, EntryList> Death { get; } = new(StringComparer.OrdinalIgnoreCase);

  public IEnumerable<EntryList> AllLists => new[] { General }.Concat(Teleport.Values).Concat(Death.Values);

  public int EntryCount => AllLists.Sum(list => list.Entries.Count);

  public bool IsEmpty => EntryCount == 0;
}

public static class KnownNames {
  public static readonly IReadOnlyList<string> Biomes = [
    "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean"
  ];

  public static readonly IReadOnlyList<string> ImageExtensions = [".png", ".jpg", ".jpeg"];
}
