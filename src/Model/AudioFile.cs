namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public static class AudioFile {
  public const int MaxBytes = 8 * 1024 * 1024;

  public static IEnumerable<string> Paths(ScreenConfig config) {
    return config.Layouts.Values.Select(layout => layout.Sound)
        .Concat(config.AllLists.Select(list => list.Sound))
        .Concat(config.AllLists.SelectMany(list => list.Entries).SelectMany(entry => new[] { entry.Sound, entry.Voice }))
        .Where(path => path != null)
        .Distinct(StringComparer.OrdinalIgnoreCase);
  }

  public static bool IsValid(string path, byte[] data) {
    if (data == null || data.Length < 12 || data.Length > MaxBytes) return false;
    if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) {
      return data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S';
    }
    if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
      return data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
          && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E';
    }
    return false;
  }
}
