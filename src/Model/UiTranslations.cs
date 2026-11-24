namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public sealed class UiTranslations {
  public const string FileName = "translations.yaml";
  public const int MaxBytes = 256 * 1024;
  const string ProgressTemplate = "<b>{percent}%</b>\n<size=-4>({seconds}{unit})</size>";

  static readonly (string Language, string Unit)[] BuiltIn = [
    ("English", "s"),
    ("Swedish", "s"),
    ("French", "s"),
    ("Italian", "s"),
    ("German", "s"),
    ("Spanish", "s"),
    ("Russian", "с"),
    ("Romanian", "s"),
    ("Bulgarian", "с"),
    ("Macedonian", "с"),
    ("Finnish", "s"),
    ("Danish", "s"),
    ("Norwegian", "s"),
    ("Icelandic", "s"),
    ("Turkish", "sn"),
    ("Lithuanian", "s"),
    ("Czech", "s"),
    ("Hungarian", "mp"),
    ("Slovak", "s"),
    ("Polish", "s"),
    ("Dutch", "s"),
    ("Portuguese_European", "s"),
    ("Portuguese_Brazilian", "s"),
    ("Chinese", "秒"),
    ("Chinese_Trad", "秒"),
    ("Japanese", "秒"),
    ("Korean", "초"),
    ("Hindi", "से"),
    ("Thai", "วิ"),
    ("Abenaki", "s"),
    ("Croatian", "s"),
    ("Georgian", "წმ"),
    ("Greek", "δ"),
    ("Serbian", "с"),
    ("Ukrainian", "с"),
    ("Latvian", "s")
  ];

  static readonly string[] KeyNames = [
    "gameStartProgress", "secondsUnit", "connectingStage", "passwordStage", "receivingWorldStage",
    "configProblems", "musicVolumeSlider", "voiceVolumeSlider"
  ];
  static readonly string[] RemovedKeyNames = ["continuePrompt"];
  static readonly Dictionary<string, Dictionary<string, string>> Defaults = BuiltIn.ToDictionary(
      item => LanguageText.Code("Game", item.Language),
      item => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["gameStartProgress"] = ProgressTemplate,
        ["secondsUnit"] = item.Unit
      }, StringComparer.OrdinalIgnoreCase);

  static UiTranslations() {
    Defaults["en"]["connectingStage"] = "Connecting";
    Defaults["en"]["passwordStage"] = "Password";
    Defaults["en"]["receivingWorldStage"] = "Receiving the world";
    Defaults["en"]["configProblems"] = "LoadingScreen found problems in {files}. Details are in BepInEx/LogOutput.log.";
    Defaults["en"]["musicVolumeSlider"] = "Loading screen music";
    Defaults["en"]["voiceVolumeSlider"] = "Loading screen voice";
  }

  readonly Dictionary<string, Dictionary<string, string>> _overrides = new(StringComparer.OrdinalIgnoreCase);

  public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly(BuiltIn.Select(item => item.Language).ToArray());
  public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(KeyNames);
  public List<string> Warnings { get; } = [];

  public string Get(string language, string key) {
    if (!KeyNames.Contains(key, StringComparer.OrdinalIgnoreCase)) return null;
    string code = LanguageText.TryCode(language, out string selected) && Defaults.ContainsKey(selected)
        ? selected : "en";
    if (_overrides.TryGetValue(code, out Dictionary<string, string> translated)
        && translated.TryGetValue(key, out string value)) return value;
    return Defaults[code].TryGetValue(key, out string builtIn) ? builtIn : Defaults["en"][key];
  }

  public string FormatProgress(string language, int percent, int seconds) =>
      Get(language, "gameStartProgress")
          .Replace("{percent}", percent.ToString(CultureInfo.InvariantCulture))
          .Replace("{seconds}", Math.Max(0, seconds).ToString(CultureInfo.InvariantCulture))
          .Replace("{unit}", Get(language, "secondsUnit"));

  public string FormatConfigProblems(string language, string files) =>
      Get(language, "configProblems").Replace("{files}", files);

  public static UiTranslations Parse(string yaml) {
    UiTranslations translations = new();
    if (string.IsNullOrWhiteSpace(yaml)) return translations;
    if (yaml.Length > MaxBytes || System.Text.Encoding.UTF8.GetByteCount(yaml) > MaxBytes) {
      translations.Warnings.Add(LogContext.Message("Translation overrides ignored.", ("file", FileName),
          ("bytes", System.Text.Encoding.UTF8.GetByteCount(yaml)), ("maxBytes", MaxBytes)));
      return translations;
    }

    YamlStream stream = new();
    try {
      string structureProblem = YamlGuard.Check(yaml);
      if (structureProblem != null) {
        translations.Warnings.Add(LogContext.Message("Translation overrides ignored.", ("file", FileName),
            ("reason", structureProblem)));
        return translations;
      }
      stream.Load(new StringReader(yaml));
    } catch (YamlException exception) {
      translations.Warnings.Add(LogContext.Message("Could not parse translation overrides.", ("file", FileName),
          ("line", exception.Start.Line), ("error", exception.Message)));
      return translations;
    }

    if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) {
      translations.Warnings.Add(LogContext.Message("Translation overrides ignored.", ("file", FileName),
          ("expected", "mapping of language names")));
      return translations;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> languageEntry in root.Children) {
      string language = (languageEntry.Key as YamlScalarNode)?.Value;
      if (!LanguageText.TryCode(language, out string code) || !Defaults.ContainsKey(code)) {
        translations.Warnings.Add(LogContext.Message("Unknown translation language ignored.", ("file", FileName),
            ("line", languageEntry.Key.Start.Line), ("language", language)));
        continue;
      }
      if (languageEntry.Value is not YamlMappingNode entries) {
        translations.Warnings.Add(LogContext.Message("Translation language ignored.", ("file", FileName),
            ("line", languageEntry.Value.Start.Line), ("language", language), ("expected", "mapping of keys to text")));
        continue;
      }
      if (!translations._overrides.TryGetValue(code, out Dictionary<string, string> values)) {
        values = new(StringComparer.OrdinalIgnoreCase);
        translations._overrides[code] = values;
      }
      foreach (KeyValuePair<YamlNode, YamlNode> entry in entries.Children) {
        string key = (entry.Key as YamlScalarNode)?.Value;
        if (RemovedKeyNames.Contains(key, StringComparer.OrdinalIgnoreCase)) {
          translations.Warnings.Add(LogContext.Message("Removed translation key ignored.", ("file", FileName),
              ("line", entry.Key.Start.Line), ("language", language), ("key", key)));
        } else if (!KeyNames.Contains(key, StringComparer.OrdinalIgnoreCase)) {
          translations.Warnings.Add(LogContext.Message("Unknown translation key ignored.", ("file", FileName),
              ("line", entry.Key.Start.Line), ("language", language), ("key", key),
              ("accepted", string.Join(",", KeyNames))));
        } else if (entry.Value is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value)) {
          translations.Warnings.Add(LogContext.Message("Translation text ignored.", ("file", FileName),
              ("line", entry.Value.Start.Line), ("language", language), ("key", key), ("expected", "non-empty text")));
        } else {
          values[key] = scalar.Value;
        }
      }
    }
    return translations;
  }
}
