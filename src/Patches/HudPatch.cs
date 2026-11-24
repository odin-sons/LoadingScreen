namespace LoadingScreen;

using System.Collections.Generic;
using System.Reflection.Emit;

using global::LoadingScreen.Extensions;

using HarmonyLib;

using UnityEngine;

[HarmonyPatch(typeof(Hud))]
static class HudPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(Hud.Awake))]
  static void AwakePostfix(Hud __instance) => HudScreen.OnAwake(__instance);

  [HarmonyPrefix]
  [HarmonyPatch(nameof(Hud.UpdateBlackScreen))]
  static bool UpdateBlackScreenPrefix(Hud __instance, Player player) => HudScreen.OnBeforeUpdate(__instance, player);

  [HarmonyPostfix]
  [HarmonyPatch(nameof(Hud.UpdateBlackScreen))]
  static void UpdateBlackScreenPostfix(Hud __instance) => HudScreen.OnAfterUpdate(__instance);

  [HarmonyTranspiler]
  [HarmonyPatch(nameof(Hud.UpdateBlackScreen))]
  static IEnumerable<CodeInstruction> UpdateBlackScreenTranspiler(
      IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
    return new CodeMatcher(instructions, generator)
        .Start()
        .MatchStartForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Player), nameof(Player.ShowTeleportAnimation))),
            new CodeMatch(OpCodes.Brfalse),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Hud), nameof(Hud.m_loadingProgress))))
        .ThrowIfInvalid($"Could not patch Hud.UpdateBlackScreen()! (show-teleport-animation)")
        .Advance(offset: 1)
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(
                OpCodes.Call, AccessTools.Method(typeof(HudPatch), nameof(ShowTeleportAnimationDelegate))))
        .MatchStartForward(
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Hud), nameof(Hud.UpdateProgressIndicator))),
            new CodeMatch(OpCodes.Ret),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Hud), nameof(Hud.m_loadingProgress))),
            new CodeMatch(OpCodes.Ldc_I4_0),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive))))
        .ThrowIfInvalid($"Could not patch Hud.UpdateBlackScreen()! (set-loading-progress-active)")
        .Advance(offset: 5)
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(
                OpCodes.Call, AccessTools.Method(typeof(HudPatch), nameof(ShowLoadingProgressDelegate))))
        .InstructionEnumeration();
  }

  static bool ShowTeleportAnimationDelegate(bool value, Hud hud) => HudScreen.ShowTeleportAnimation(value, hud);

  static bool ShowLoadingProgressDelegate(bool value, Player player) => HudScreen.ShowLoadingProgress(value, player);

  [HarmonyTranspiler]
  [HarmonyPatch(nameof(Hud.UpdateShownTip))]
  static IEnumerable<CodeInstruction> UpdateShownTipTranspiler(
      IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
    return new CodeMatcher(instructions, generator)
        .Start()
        .MatchStartForward(
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Hud), nameof(Hud.m_currentLoadingTipIndex))),
            new CodeMatch(OpCodes.Callvirt),
            new CodeMatch(OpCodes.Stloc_1))
        .ThrowIfInvalid($"Could not patch Hud.UpdateShownTip()! (loading-tip-set-text)")
        .Advance(offset: 2)
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HudPatch), nameof(LoadingTipSetTextDelegate))))
        .MatchStartForward(
            new CodeMatch(OpCodes.Ldstr, "tip:"),
            new CodeMatch(OpCodes.Ldloc_1),
            new CodeMatch(
                OpCodes.Call,
                AccessTools.Method(typeof(string), nameof(string.Concat), [typeof(string), typeof(string)])),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ZLog), nameof(ZLog.Log))))
        .ThrowIfInvalid($"Could not patch Hud.UpdateShownTip()! (loading-tip-ignore-zlog)")
        .CreateLabelOffset(offset: 4, out Label logLabel)
        .ExtractLabels(out List<Label> ldstrLabels)
        .Insert(new CodeInstruction(OpCodes.Br, logLabel))
        .AddLabels(ldstrLabels)
        .Advance(offset: 1)
        .InstructionEnumeration();
  }

  static string LoadingTipSetTextDelegate(string text) => HudScreen.LoadingTipSetText(text);
}
