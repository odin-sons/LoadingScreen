namespace LoadingScreen;

using System.Collections.Generic;
using System.Reflection.Emit;

using global::LoadingScreen.Extensions;

using HarmonyLib;

using global::LoadingScreen.Model;

using UnityEngine;

using static PluginConfig;

[HarmonyPatch(typeof(Hud))]
static class HudPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(Hud.Awake))]
  static void AwakePostfix(Hud __instance) {
    _holdState.Reset();
    if (IsModEnabled.Value) LoadingManager.SetupHud(__instance);
  }

  static bool _loadingScreenState;
  static bool _wasSleeping;
  static bool _sleepExitHolding;
  static float _lastImageTime;
  static float _lastTipTime;
  static Situation _currentSituation;
  static readonly LoadingHoldState _holdState = new();

  [HarmonyPrefix]
  [HarmonyPatch(nameof(Hud.UpdateBlackScreen))]
  static bool UpdateBlackScreenPrefix(Hud __instance, Player player) {
    if (!IsModEnabled.Value) {
      _holdState.Reset();
      _loadingScreenState = false;
      _wasSleeping = false;
      _sleepExitHolding = false;
      return true;
    }

    if (!LoadingManager.HudHost) LoadingManager.SetupHud(__instance);
    bool stillSleeping = ShowLoadingScreenWhileSleeping.Value && Player.m_localPlayer && Player.m_localPlayer.IsSleeping();
    _loadingScreenState = __instance.m_loadingProgress.activeInHierarchy;
    bool ready = player && !player.IsDead() && !player.IsTeleporting() && !player.IsSleeping()
        && Game.instance && !Game.instance.IsShuttingDown();
    bool allowed = !Application.isBatchMode && !CinematicsManager.IsPlaying() && !stillSleeping;
    bool voiceActive = WaitForVoice.Value && LoadingAudio.IsVoicePlayingOrLoading(LoadingManager.HudHost);
    bool exitActive = ready && allowed && WaitForExitAnimation.Value && LoadingManager.HudHost
        && LoadingManager.HudHost.BeginExit(Time.realtimeSinceStartup);
    bool dismissed = Input.anyKeyDown || ZInput.GetMouseButtonDown(0) || ZInput.GetMouseButtonDown(1)
        || ZInput.GetMouseButtonDown(2);
    LoadingHoldDecision hold = _holdState.Update(
        _loadingScreenState, ready, allowed, HoldLoadingScreen.Value, voiceActive, dismissed,
        Time.realtimeSinceStartup, MaximumHoldSeconds.Value, exitActive);
    LoadingManager.ShowContinuePrompt(__instance, hold.ShowContinuePrompt);
    _sleepExitHolding = _wasSleeping && !stillSleeping && hold.Holding;

    if (hold.Holding) {
      __instance.m_loadingScreen.gameObject.SetActive(true);
      __instance.m_loadingScreen.alpha = 1f;
      __instance.m_loadingProgress.SetActive(true);
    }

    if (_wasSleeping && !stillSleeping && !hold.Holding) {
      __instance.m_loadingProgress.SetActive(false);
    }

    return !hold.Holding;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(Hud.UpdateBlackScreen))]
  static void UpdateBlackScreenPostfix(Hud __instance) {
    if (!IsModEnabled.Value || _sleepExitHolding) return;
    bool sleeping = ShowLoadingScreenWhileSleeping.Value && Player.m_localPlayer && Player.m_localPlayer.IsSleeping();
    bool newScreen = !_loadingScreenState || sleeping != _wasSleeping;
    _wasSleeping = sleeping;

    if (sleeping) {
      __instance.m_loadingProgress.SetActive(true);
      if (newScreen) {
        __instance.m_sleepingProgress.transform.SetAsLastSibling();
      }
    }

    if (!__instance.m_loadingProgress.activeInHierarchy) {
      if (LoadingManager.Session.Stage == LoadingStage.Hud) LoadingManager.Session.Reset();
      return;
    }

    float now = Time.realtimeSinceStartup;
    if (newScreen) {
      Selection preselected = null;
      _currentSituation = sleeping ? Situation.Sleep : SituationTracker.Consume(out preselected);
      bool continueSession = LoadingManager.Session.CanContinue(LoadingStage.Hud, _currentSituation);
      _lastImageTime = continueSession ? LoadingManager.Session.StartedAt : now;
      _lastTipTime = now;

      if (!LoadingManager.ShowSituation(
              LoadingManager.HudHost, LoadingStage.Hud, _currentSituation, __instance.m_loadingTip,
              preselected, continueSession)) {
        LoadingManager.ShowFallback(LoadingManager.HudHost, __instance.m_loadingImage, __instance.m_loadingTip);
        __instance.ScaleLerpLoadingImage(__instance.m_loadingImage);
      }

      if (sleeping) {
        __instance.m_sleepingProgress.SetActive(LoadingManager.HudHost.ShowSleepUi);
      }

      return;
    }

    if (sleeping) {
      __instance.m_sleepingProgress.SetActive(LoadingManager.HudHost.ShowSleepUi);
    }

    if (!SlideshowTiming.IsDue(now, _lastImageTime, ChangeImageEverySeconds.Value)) {
      if (TipRotation.IsDue(
              now, _lastTipTime, ChangeTipEverySeconds.Value, LoadingManager.CurrentCaption,
              CustomAssets.LoadingTips.Count)) {
        _lastTipTime = now;
        LoadingManager.RotateTip(LoadingManager.HudHost, __instance.m_loadingTip);
      }

      return;
    }

    _lastImageTime = now;
    _lastTipTime = now;
    Selection current = LoadingManager.HudHost ? LoadingManager.HudHost.CurrentSelection : null;
    Selection next = ScreenConfigLoader.Selector.SelectNext(current, _currentSituation);

    if (next == null
        || !LoadingManager.ShowSituation(
            LoadingManager.HudHost, LoadingStage.Hud, _currentSituation, __instance.m_loadingTip, next)) {
      LoadingManager.ShowFallback(LoadingManager.HudHost, __instance.m_loadingImage, __instance.m_loadingTip);
      __instance.ScaleLerpLoadingImage(__instance.m_loadingImage);
    }

    if (sleeping) {
      __instance.m_sleepingProgress.SetActive(LoadingManager.HudHost.ShowSleepUi);
    }
  }

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

  static bool ShowTeleportAnimationDelegate(bool showTeleportAnimation, Hud hud) {
    if (!IsModEnabled.Value) return showTeleportAnimation;
    if (!showTeleportAnimation) {
      return false;
    }

    if (ShowLoadingScreenOnTeleport.Value) {
      hud.UpdateShownTip(false);
      return false;
    }

    return true;
  }

  static bool ShowLoadingProgressDelegate(bool _, Player player) {
    if (!IsModEnabled.Value) return false;
    if (CinematicsManager.IsPlaying()) {
      return false;
    }

    if (player && player.IsDead()) {
      return ShowLoadingScreenImmediatelyOnDeath.Value;
    }

    return true;
  }

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

  static string LoadingTipSetTextDelegate(string text) {
    if (!IsModEnabled.Value) return text;
    if (LoadingManager.CurrentCaption != null) {
      return LoadingManager.CurrentCaption;
    }

    if (CustomAssets.GetRandomLoadingTip(out string loadingTip)) {
      text = loadingTip;
    }

    if (LoadingManager.HudHost) {
      LoadingManager.HudHost.SetCaption(text);
    }

    _lastTipTime = Time.realtimeSinceStartup;
    return text;
  }
}
