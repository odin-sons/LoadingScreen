namespace LoadingScreen;

using System.Collections.Generic;

using HarmonyLib;

[HarmonyPatch(typeof(Terminal))]
static class TerminalPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(Terminal.InitTerminal))]
  static void InitTerminalPostfix() {
    new Terminal.ConsoleCommand(
        "loadingscreen",
        "[reload|status] - LoadingScreen: re-read loadingscreen.yaml, images and tips, or show what is loaded.",
        args => Run(args),
        optionsFetcher: () => new List<string> { "reload", "status" });
  }

  static void Run(Terminal.ConsoleEventArgs args) {
    string action = args.Length > 1 ? args[1].ToLowerInvariant() : "status";

    if (action == "reload") {
      ScreenConfigLoader.Reload();
    } else if (action != "status") {
      args.Context.AddString("Usage: loadingscreen [reload|status]");
      return;
    }

    args.Context.AddString(ScreenConfigLoader.Summary());
  }
}
