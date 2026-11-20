namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public enum SituationKind {
  General,
  Teleport,
  Death
}

public readonly struct Situation {
  public SituationKind Kind { get; }
  public IReadOnlyList<string> Keys { get; }

  Situation(SituationKind kind, IReadOnlyList<string> keys) {
    Kind = kind;
    Keys = keys ?? [];
  }

  public static Situation General => new(SituationKind.General, []);

  public static Situation Teleport(string biome) => new(SituationKind.Teleport, biome == null ? [] : [biome]);

  public static Situation Death(params string[] causes) => new(SituationKind.Death, causes);

  public override string ToString() {
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
  readonly Dictionary<EntryList, Entry> _lastPicked = [];

  public EntrySelector(ScreenConfig config, Func<Entry, bool> isUsable = null, Random random = null) {
    _config = config ?? new ScreenConfig();
    _isUsable = isUsable ?? (_ => true);
    _random = random ?? new Random();
  }

  public Selection Select(Situation situation) {
    foreach (EntryList list in Candidates(situation)) {
      Entry entry = Pick(list);

      if (entry != null) {
        return new() { Entry = entry, List = list, LayoutName = entry.LayoutName ?? list.LayoutName };
      }
    }

    return null;
  }

  public void Reset() {
    _lastPicked.Clear();
  }

  IEnumerable<EntryList> Candidates(Situation situation) {
    Dictionary<string, EntryList> specific = situation.Kind switch {
      SituationKind.Teleport => _config.Teleport,
      SituationKind.Death => _config.Death,
      _ => null,
    };

    if (specific != null) {
      foreach (string key in situation.Keys) {
        if (key != null && specific.TryGetValue(key, out EntryList list)) {
          yield return list;
        }
      }
    }

    yield return _config.General;
  }

  Entry Pick(EntryList list) {
    List<Entry> usable = list.Entries.Where(_isUsable).ToList();

    if (usable.Count == 0) {
      return null;
    }

    if (usable.Count > 1 && _lastPicked.TryGetValue(list, out Entry last)) {
      usable.Remove(last);
    }

    Entry picked = usable[_random.Next(usable.Count)];
    _lastPicked[list] = picked;

    return picked;
  }
}
