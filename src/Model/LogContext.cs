namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class LogContext {
  public const int MaxSentenceLength = 1000;
  public const int MaxValueLength = 500;

  public static string Message(string sentence, params (string Key, object Value)[] details) =>
      Escape(sentence ?? string.Empty, MaxSentenceLength) + (details.Length == 0 ? string.Empty : " " + string.Join(" ",
          details.Select(detail => $"{detail.Key}={Value(detail.Value)}")));

  public static string Value(object value) {
    string text = Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null", MaxValueLength)
        .Replace("\"", "\\\"");
    return text.Length == 0 || text.Any(character => char.IsWhiteSpace(character) || character == '=' || character == '"')
        ? $"\"{text}\"" : text;
  }

  static string Escape(string text, int maxLength) {
    bool truncated = text.Length > maxLength;
    if (truncated) text = text.Substring(0, maxLength);
    StringBuilder escaped = null;
    for (int index = 0; index < text.Length; index++) {
      char character = text[index];
      string replacement = character switch {
        '\r' => "\\r",
        '\n' => "\\n",
        '\t' => "\\t",
        _ when char.IsControl(character) || character is '\u2028' or '\u2029'
            => "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture),
        _ => null,
      };
      if (replacement == null) {
        escaped?.Append(character);
        continue;
      }
      escaped ??= new StringBuilder(text, 0, index, text.Length + 16);
      escaped.Append(replacement);
    }
    string result = escaped?.ToString() ?? text;
    return truncated ? result + "..." : result;
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
