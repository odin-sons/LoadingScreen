namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public enum SituationKind {
  General,
  Sleep,
  Teleport,
  Death
}

public readonly struct Situation {
  public SituationKind Kind { get; }
  public IReadOnlyList<string> Keys { get; }
  public string AttackerPrefab { get; }
  public int? BiomeValue { get; }

  Situation(SituationKind kind, IReadOnlyList<string> keys, string attackerPrefab = null, int? biomeValue = null) {
    Kind = kind;
    Keys = keys ?? [];
    AttackerPrefab = attackerPrefab;
    BiomeValue = biomeValue;
  }

  public static Situation General => new(SituationKind.General, []);

  public static Situation Sleep => new(SituationKind.Sleep, []);

  public static Situation Teleport(string biome) => new(SituationKind.Teleport, biome == null ? [] : [biome]);

  public static Situation Teleport(int biome) => new(SituationKind.Teleport, [], biomeValue: biome);

  public static Situation Death(params string[] causes) => new(SituationKind.Death, causes);

  public static Situation Death(IReadOnlyList<string> causes, string attackerPrefab, int? biomeValue = null) =>
      new(SituationKind.Death, causes, attackerPrefab, biomeValue);

  public override string ToString() {
    if (Kind == SituationKind.Death && AttackerPrefab != null) {
      return $"{Kind}({AttackerPrefab}, {string.Join(", ", Keys)})";
    }

    return Keys.Count == 0 ? Kind.ToString() : $"{Kind}({string.Join(", ", Keys)})";
  }
}

public sealed class Selection {
  public Entry Entry { get; set; }
  public EntryList List { get; set; }
  public string LayoutName { get; set; }
}

public sealed class EntrySelector {
  readonly ScreenConfig _config;
  readonly Func<Entry, bool> _isUsable;
  readonly Random _random;
  readonly IBiomeResolver _biomes;
  readonly Action<string> _debug;
  readonly Dictionary<EntryList, Entry> _lastPicked = [];

  public EntrySelector(ScreenConfig config, Func<Entry, bool> isUsable = null, Random random = null,
      IBiomeResolver biomes = null, Action<string> debug = null) {
    _config = config ?? new ScreenConfig();
    _isUsable = isUsable ?? (_ => true);
    _random = random ?? new Random();
    _biomes = biomes;
    _debug = debug;
  }

  public Selection Select(Situation situation) {
    foreach ((EntryList list, string reason) in Candidates(situation)) {
      Selection selection = SelectFromList(list);
      if (selection != null) {
        _debug?.Invoke($"Selected {selection.Entry.Image} from {list.Name}: {reason}; order {list.Order ?? _config.Order}.");
        return selection;
      }
      _debug?.Invoke($"Skipped {list.Name}: no usable entries for {situation}.");
    }

    return null;
  }

  public Selection SelectNext(Selection current, Situation situation) {
    if (current != null && Candidates(situation).Any(candidate => ReferenceEquals(candidate.List, current.List))) {
      Selection next = SelectFromList(current.List);
      if (next != null) _debug?.Invoke($"Selected {next.Entry.Image} from {current.List.Name}: continuing the same list.");
      return next;
    }

    return Select(situation);
  }

  public Selection SelectFromList(EntryList list) {
    if (list == null) {
      return null;
    }

    Entry entry = Pick(list);
    return entry == null ? null : new() { Entry = entry, List = list, LayoutName = entry.LayoutName ?? list.LayoutName };
  }

  public void Reset() {
    _lastPicked.Clear();
  }

  IEnumerable<(EntryList List, string Reason)> Candidates(Situation situation) {
    if (situation.Kind == SituationKind.Sleep) {
      yield return (_config.Sleep, "sleep situation");
    }

    if (situation.Kind == SituationKind.Death && situation.AttackerPrefab != null
        && _config.Enemy.TryGetValue(situation.AttackerPrefab, out EntryList enemy)) {
      yield return (enemy, $"attacker {situation.AttackerPrefab}");
    } else if (situation.Kind == SituationKind.Death && situation.AttackerPrefab != null) {
      _debug?.Invoke($"No enemy list for attacker {situation.AttackerPrefab}.");
    }

    if (situation.Kind == SituationKind.Death) {
      foreach (string key in situation.Keys) {
        if (key == null) continue;
        if (_config.Death.TryGetValue(key, out EntryList list)) yield return (list, $"death cause {key}");
        else _debug?.Invoke($"No death list for cause {key}.");
      }
    }

    if ((situation.Kind is SituationKind.Teleport or SituationKind.Death)
        && situation.BiomeValue.HasValue && _biomes != null) {
      int biome = situation.BiomeValue.Value;
      foreach (EntryList list in MatchingBiomeLists(biome)) yield return (list, $"biome {biome}");
      int baseBiome = _biomes.GetBaseBiome(biome);
      if (baseBiome != biome) {
        foreach (EntryList list in MatchingBiomeLists(baseBiome)) yield return (list, $"base biome {baseBiome}");
      }
    } else if (situation.Kind == SituationKind.Teleport) {
      foreach (string key in situation.Keys) {
        if (key == null) continue;
        if (_config.Biome.TryGetValue(key, out EntryList list)) yield return (list, $"destination biome {key}");
        else _debug?.Invoke($"No biome list for destination {key}.");
      }
    }

    yield return (_config.General, $"general fallback for {situation}");
  }

  IEnumerable<EntryList> MatchingBiomeLists(int biome) {
    foreach (KeyValuePair<string, EntryList> pair in _config.Biome) {
      if (_biomes.TryResolve(pair.Key, out int value) && value == biome) yield return pair.Value;
    }
  }

  Entry Pick(EntryList list) {
    List<Entry> usable = list.Entries.Where(_isUsable).ToList();

    if (usable.Count == 0) {
      return null;
    }

    if ((list.Order ?? _config.Order) == PickOrder.Sequential) {
      return PickNext(list, usable);
    }

    if (usable.Count > 1 && _lastPicked.TryGetValue(list, out Entry last)) {
      usable.Remove(last);
    }

    Entry picked = usable[_random.Next(usable.Count)];
    _lastPicked[list] = picked;

    return picked;
  }

  Entry PickNext(EntryList list, List<Entry> usable) {
    int start = _lastPicked.TryGetValue(list, out Entry last) ? list.Entries.IndexOf(last) + 1 : 0;
    Entry picked = null;

    for (int offset = 0; offset < list.Entries.Count && picked == null; offset++) {
      Entry candidate = list.Entries[(start + offset) % list.Entries.Count];

      if (usable.Contains(candidate)) {
        picked = candidate;
      }
    }

    _lastPicked[list] = picked;
    return picked;
  }
}
