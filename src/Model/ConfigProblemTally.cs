namespace LoadingScreen.Model;

using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed class ConfigProblemTally {
  static readonly string[] TrackedFiles = ["loadingscreen.yaml", "animations.yaml", UiTranslations.FileName];
  static readonly Regex FileField = new(@"(?:^|\s)file=(""(?:[^""\\]|\\.)*""|\S+)", RegexOptions.CultureInvariant);

  readonly int[] _counts = new int[TrackedFiles.Length];

  public int Total => _counts.Sum();

  public bool Add(string message) {
    if (message == null) return false;
    Match match = FileField.Match(message);
    if (!match.Success) return false;
    string file = match.Groups[1].Value.Trim('"');
    int index = Array.FindIndex(TrackedFiles, tracked => string.Equals(tracked, file, StringComparison.OrdinalIgnoreCase));
    if (index < 0) return false;
    _counts[index]++;
    return true;
  }

  public string Details() => string.Join(", ", TrackedFiles
      .Select((file, index) => (File: file, Count: _counts[index]))
      .Where(item => item.Count > 0)
      .Select(item => $"{item.File}: {item.Count}"));
}
