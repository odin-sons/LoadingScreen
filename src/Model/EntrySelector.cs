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

  Situation(SituationKind kind, IReadOnlyList<string> keys) {
    Kind = kind;
    Keys = keys ?? [];
  }

  public static Situation General => new(SituationKind.General, []);

  public static Situation Sleep => new(SituationKind.Sleep, []);

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
      Selection selection = SelectFromList(list);
      if (selection != null) {
        return selection;
      }
    }

    return null;
  }

  public Selection SelectNext(Selection current, Situation situation) {
    if (current != null && Candidates(situation).Any(list => ReferenceEquals(list, current.List))) {
      return SelectFromList(current.List);
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

  IEnumerable<EntryList> Candidates(Situation situation) {
    if (situation.Kind == SituationKind.Sleep) {
      yield return _config.Sleep;
    }

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
