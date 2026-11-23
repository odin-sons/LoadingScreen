namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public static class LanguageText {
  internal static readonly Dictionary<string, string> GameCodes = new(StringComparer.OrdinalIgnoreCase) {
    ["English"] = "en", ["Russian"] = "ru", ["German"] = "de", ["French"] = "fr",
    ["Spanish"] = "es", ["Italian"] = "it", ["Japanese"] = "ja", ["Korean"] = "ko",
    ["Chinese"] = "zh-CN", ["Chinese_Trad"] = "zh-TW", ["Portuguese_European"] = "pt",
    ["Portuguese_Brazilian"] = "pt-BR", ["Czech"] = "cs", ["Danish"] = "da",
    ["Dutch"] = "nl", ["Finnish"] = "fi", ["Greek"] = "el", ["Hungarian"] = "hu",
    ["Norwegian"] = "no", ["Polish"] = "pl", ["Slovak"] = "sk", ["Swedish"] = "sv",
    ["Turkish"] = "tr", ["Ukrainian"] = "uk", ["Romanian"] = "ro", ["Thai"] = "th",
    ["Indonesian"] = "id", ["Vietnamese"] = "vi", ["Bulgarian"] = "bg",
    ["Macedonian"] = "mk", ["Icelandic"] = "is", ["Lithuanian"] = "lt",
    ["Hindi"] = "hi", ["Abenaki"] = "abe", ["Croatian"] = "hr",
    ["Georgian"] = "ka", ["Serbian"] = "sr", ["Latvian"] = "lv", ["Arabic"] = "ar"
  };

  public static string Code(string setting, string gameLanguage) {
    if (!string.IsNullOrWhiteSpace(setting) && !string.Equals(setting.Trim(), "Game", StringComparison.OrdinalIgnoreCase)
        && TryCode(setting, out string forced)) {
      return forced;
    }

    return TryCode(gameLanguage, out string selected) ? selected : "en";
  }

  public static bool TryCode(string value, out string code) {
    code = null;
    if (string.IsNullOrWhiteSpace(value)) return false;
    string name = value.Trim();
    if (GameCodes.TryGetValue(name, out code)) return true;
    code = GameCodes.Values.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
    return code != null;
  }

  public static string TipFileName(string code, Func<string, bool> exists) {
    string localized = $"tips.{code}.txt";
    return exists(localized) ? localized : "tips.txt";
  }
}
