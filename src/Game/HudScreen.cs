namespace LoadingScreen;

using global::LoadingScreen.Model;

using UnityEngine;

using static PluginConfig;

internal static class HudScreen {
  internal static void OnAwake(Hud hud) {
    _holdState.Reset();
    if (IsModEnabled.Value) LoadingManager.SetupHud(hud);
  }

  static bool _loadingScreenState;
  static bool _wasSleeping;
  static bool _sleepExitHolding;
  static Situation _currentSituation;
  static readonly LoadingHoldState _holdState = new();
  static float _nextThreatScanAt;
  static GameObject _indicatorOwner;
  static bool _indicatorWasActive;

  internal static bool IsHolding => _holdState.Holding;

  internal static bool OnBeforeUpdate(Hud __instance, Player player) {
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
        || ZInput.GetMouseButtonDown(2) || GamepadMoved();
    float now = Time.realtimeSinceStartup;
    bool holding = _holdState.Update(
        _loadingScreenState, ready, allowed, voiceActive, dismissed, now, MaximumHoldSeconds.Value, exitActive);
    bool invulnerable = InvulnerableWhileHeld.Value;
    bool pvp = holding && !invulnerable && player.IsPVPEnabled();
    bool monster = holding && !invulnerable && !pvp && now >= _nextThreatScanAt
        && HasHostileMonsterNoticed(player);
    LoadingHoldSafetyAction safety = LoadingHoldSafety.Decide(holding, invulnerable, pvp, monster);
    if (safety is LoadingHoldSafetyAction.CloseForPvp or LoadingHoldSafetyAction.CloseForMonster) {
      _holdState.Reset();
      holding = false;
      Log.Render.Info(LogContext.Message("Closed the held loading screen.",
          ("reason", safety == LoadingHoldSafetyAction.CloseForPvp ? "pvp" : "monster")));
    }
    _sleepExitHolding = _wasSleeping && !stillSleeping && holding;

    if (holding) {
      __instance.m_loadingScreen.gameObject.SetActive(true);
      __instance.m_loadingScreen.alpha = 1f;
      __instance.m_loadingProgress.SetActive(true);
    }

    if (_wasSleeping && !stillSleeping && !holding) {
      __instance.m_loadingProgress.SetActive(false);
    }

    return !holding;
  }

  static bool _gamepadWasMoved;

  static bool GamepadMoved() {
    bool moved = ZInput.GetJoyLeftStick().sqrMagnitude > 0.25f || ZInput.GetJoyRightStick().sqrMagnitude > 0.25f
        || ZInput.GetJoyLTrigger() > 0.5f || ZInput.GetJoyRTrigger() > 0.5f;
    bool pressed = moved && !_gamepadWasMoved;
    _gamepadWasMoved = moved;
    return pressed;
  }

  static bool HasHostileMonsterNoticed(Player player) {
    _nextThreatScanAt = Time.realtimeSinceStartup + 0.5f;
    foreach (BaseAI ai in BaseAI.GetAllInstances()) {
      if (ai is not MonsterAI monster || !monster) continue;
      if ((monster.transform.position - player.transform.position).sqrMagnitude > 2500f) continue;
      Character creature = monster.GetComponent<Character>();
      if (!creature || creature.IsDead() || creature.IsTamed() || !monster.IsEnemy(player)) continue;
      if (monster.GetTargetCreature() == player || monster.CanSenseTarget(player)) return true;
    }

    return false;
  }

  internal static void OnAfterUpdate(Hud __instance) {
    UpdateScreen(__instance);
    SyncIndicator(__instance);
  }

  static void SyncIndicator(Hud hud) {
    if (!hud.m_loadingIndicator) return;
    GameObject indicator = hud.m_loadingIndicator.gameObject;
    if (_indicatorOwner != indicator) {
      _indicatorOwner = indicator;
      _indicatorWasActive = indicator.activeSelf;
    }
    bool active = _indicatorWasActive
        && !(IsModEnabled.Value && LoadingManager.HudHost && LoadingManager.HudHost.IsShowingLayout);
    if (indicator.activeSelf != active) indicator.SetActive(active);
  }

  static void UpdateScreen(Hud __instance) {
    if (!IsModEnabled.Value || _sleepExitHolding) return;
    bool sleeping = ShowLoadingScreenWhileSleeping.Value && Player.m_localPlayer && Player.m_localPlayer.IsSleeping();
    if (sleeping && !_wasSleeping) ProgressReader.ResetSleep();
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
      LoadingManager.HudHost.Slideshow.Reset(continueSession ? LoadingManager.Session.StartedAt : now, now);

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

    LoadingManager.HudHost.Slideshow.Tick(now, LoadingStage.Hud, _currentSituation,
        LoadingManager.HudHost, __instance.m_loadingTip, __instance.m_loadingImage,
        rotateImages: true, rotateTips: true,
        onFallback: () => __instance.ScaleLerpLoadingImage(__instance.m_loadingImage));

    if (sleeping) {
      __instance.m_sleepingProgress.SetActive(LoadingManager.HudHost.ShowSleepUi);
    }
  }

  internal static bool ShowTeleportAnimation(bool showTeleportAnimation, Hud hud) {
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

  internal static bool ShowLoadingProgress(bool _, Player player) {
    if (!IsModEnabled.Value) return false;
    if (CinematicsManager.IsPlaying()) {
      return false;
    }

    if (player && player.IsDead()) {
      return ShowLoadingScreenImmediatelyOnDeath.Value;
    }

    return true;
  }

  internal static string LoadingTipSetText(string text) {
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

    if (LoadingManager.HudHost) LoadingManager.HudHost.Slideshow.TipChanged(Time.realtimeSinceStartup);
    return text;
  }
}
