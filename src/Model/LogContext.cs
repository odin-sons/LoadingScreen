namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class LogContext {
  public static string Message(string sentence, params (string Key, object Value)[] details) =>
      sentence + (details.Length == 0 ? string.Empty : " " + string.Join(" ", details.Select(
          detail => $"{detail.Key}={Value(detail.Value)}")));

  public static string Value(object value) {
    string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
    text = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"");
    return text.Length == 0 || text.Any(character => char.IsWhiteSpace(character) || character == '=' || character == '"')
        ? $"\"{text}\"" : text;
  }

  public static string File(string path, string folder = null) {
    if (path == null) return "null";
    if (!Path.IsPathRooted(path)) return path.Replace('\\', '/');
    if (folder != null) {
      string prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
          + Path.DirectorySeparatorChar;
      if (Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return path.Substring(prefix.Length).Replace('\\', '/');
    }
    return Path.GetFileName(path);
  }
}

public sealed class LogOnce {
  readonly HashSet<string> _seen = new(StringComparer.Ordinal);

  public bool First(string key) => _seen.Add(key);
}
