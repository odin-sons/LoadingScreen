namespace LoadingScreen.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public sealed class UiTranslations {
  const string ProgressTemplate = "<b>{percent}%</b>\n<size=-4>({seconds}{unit})</size>";

  static readonly (string Language, string Prompt, string Unit)[] BuiltIn = [
    ("English", "Press any key to continue", "s"),
    ("Swedish", "Tryck på valfri tangent för att fortsätta", "s"),
    ("French", "Appuyez sur une touche pour continuer", "s"),
    ("Italian", "Premi un tasto qualsiasi per continuare", "s"),
    ("German", "Drücke eine beliebige Taste, um fortzufahren", "s"),
    ("Spanish", "Pulsa cualquier tecla para continuar", "s"),
    ("Russian", "Нажмите любую клавишу, чтобы продолжить", "с"),
    ("Romanian", "Apasă orice tastă pentru a continua", "s"),
    ("Bulgarian", "Натиснете произволен клавиш, за да продължите", "с"),
    ("Macedonian", "Притиснете кое било копче за да продолжите", "с"),
    ("Finnish", "Jatka painamalla mitä tahansa näppäintä", "s"),
    ("Danish", "Tryk på en vilkårlig tast for at fortsætte", "s"),
    ("Norwegian", "Trykk på en vilkårlig tast for å fortsette", "s"),
    ("Icelandic", "Ýttu á einhvern takka til að halda áfram", "s"),
    ("Turkish", "Devam etmek için herhangi bir tuşa bas", "sn"),
    ("Lithuanian", "Paspauskite bet kurį klavišą, kad tęstumėte", "s"),
    ("Czech", "Pokračujte stisknutím libovolné klávesy", "s"),
    ("Hungarian", "Nyomj meg egy gombot a folytatáshoz", "mp"),
    ("Slovak", "Pokračujte stlačením ľubovoľného klávesu", "s"),
    ("Polish", "Naciśnij dowolny klawisz, aby kontynuować", "s"),
    ("Dutch", "Druk op een toets om door te gaan", "s"),
    ("Portuguese_European", "Prima qualquer tecla para continuar", "s"),
    ("Portuguese_Brazilian", "Pressione qualquer tecla para continuar", "s"),
    ("Chinese", "按任意键继续", "秒"),
    ("Chinese_Trad", "按任意鍵繼續", "秒"),
    ("Japanese", "いずれかのキーを押して続行", "秒"),
    ("Korean", "아무 키나 눌러 계속", "초"),
    ("Hindi", "जारी रखने के लिए कोई भी कुंजी दबाएँ", "से"),
    ("Thai", "กดปุ่มใดก็ได้เพื่อดำเนินการต่อ", "วิ"),
    ("Abenaki", "Ôlmi", "s"),
    ("Croatian", "Pritisnite bilo koju tipku za nastavak", "s"),
    ("Georgian", "გასაგრძელებლად დააჭირეთ ნებისმიერ ღილაკს", "წმ"),
    ("Greek", "Πάτησε οποιοδήποτε πλήκτρο για συνέχεια", "δ"),
    ("Serbian", "Притисните било који тастер за наставак", "с"),
    ("Ukrainian", "Натисніть будь-яку клавішу, щоб продовжити", "с"),
    ("Latvian", "Nospiediet jebkuru taustiņu, lai turpinātu", "s")
  ];

  static readonly string[] KeyNames = [
    "continuePrompt", "gameStartProgress", "secondsUnit", "connectingStage", "passwordStage", "receivingWorldStage"
  ];
  static readonly Dictionary<string, Dictionary<string, string>> Defaults = BuiltIn.ToDictionary(
      item => LanguageText.Code("Game", item.Language),
      item => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["continuePrompt"] = item.Prompt,
        ["gameStartProgress"] = ProgressTemplate,
        ["secondsUnit"] = item.Unit
      }, StringComparer.OrdinalIgnoreCase);

  static UiTranslations() {
    Defaults["en"]["connectingStage"] = "Connecting";
    Defaults["en"]["passwordStage"] = "Password";
    Defaults["en"]["receivingWorldStage"] = "Receiving the world";
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

  public static UiTranslations Parse(string yaml) {
    UiTranslations translations = new();
    if (string.IsNullOrWhiteSpace(yaml)) return translations;

    YamlStream stream = new();
    try {
      stream.Load(new StringReader(yaml));
    } catch (YamlException exception) {
      translations.Warnings.Add($"Invalid YAML: {exception.Message}");
      return translations;
    }

    if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) {
      translations.Warnings.Add("Expected a language mapping.");
      return translations;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> languageEntry in root.Children) {
      string language = (languageEntry.Key as YamlScalarNode)?.Value;
      if (!LanguageText.TryCode(language, out string code) || !Defaults.ContainsKey(code)) {
        translations.Warnings.Add($"Unknown language '{language}'.");
        continue;
      }
      if (languageEntry.Value is not YamlMappingNode entries) {
        translations.Warnings.Add($"Expected a string mapping for '{language}'.");
        continue;
      }
      if (!translations._overrides.TryGetValue(code, out Dictionary<string, string> values)) {
        values = new(StringComparer.OrdinalIgnoreCase);
        translations._overrides[code] = values;
      }
      foreach (KeyValuePair<YamlNode, YamlNode> entry in entries.Children) {
        string key = (entry.Key as YamlScalarNode)?.Value;
        if (!KeyNames.Contains(key, StringComparer.OrdinalIgnoreCase)) {
          translations.Warnings.Add($"Unknown key '{key}' for '{language}'.");
        } else if (entry.Value is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value)) {
          translations.Warnings.Add($"Expected non-empty text for '{language}.{key}'.");
        } else {
          values[key] = scalar.Value;
        }
      }
    }
    return translations;
  }
}
