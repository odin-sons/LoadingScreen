namespace LoadingScreen.Model;

using System.IO;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;

public static class YamlGuard {
  public const int MaxNestingDepth = 32;

  public static string Check(string yaml) {
    Parser parser = new(new StringReader(yaml));
    int depth = 0;

    while (parser.MoveNext()) {
      switch (parser.Current) {
        case AnchorAlias:
          return "YAML aliases (*name) are not supported";

        case MappingStart or SequenceStart:
          if (++depth > MaxNestingDepth) return $"YAML is nested deeper than {MaxNestingDepth} levels";
          break;

        case MappingEnd or SequenceEnd:
          depth--;
          break;
      }
    }

    return null;
  }
}
