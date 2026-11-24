namespace LoadingScreen;

using System.Collections.Generic;
using global::LoadingScreen.Model;
using UnityEngine;

static class ProgressReader {
  static readonly List<ZDO> _nearObjects = [];
  static double _sleepStart = double.NaN;
  static double _sleepMorning;
  const float AreaReadInterval = 0.25f;
  static float _areaReadAt = float.NegativeInfinity;
  static SituationKind _areaKind;
  static object _areaKeys;
  static ProgressValue _area;

  public static void ResetSleep() => _sleepStart = double.NaN;

  public static ProgressValue Read(LoadingStage stage, Situation situation) {
    if (situation.Kind != SituationKind.Sleep) _sleepStart = double.NaN;
    if (stage == LoadingStage.Scene) return SceneLoaderScreen.CurrentProgress;
    if (stage == LoadingStage.Menu) return Connection();

    if (situation.Kind == SituationKind.Sleep) return Sleep();
    if (situation.Kind is SituationKind.Teleport or SituationKind.Death) return ThrottledArea(situation);
    return Connection();
  }

  static ProgressValue Connection() {
    if (!ZNet.instance) return new(null);
    if (ZNet.instance.InPasswordDialog()) return ProgressMath.Connection(ProgressStage.Password);
    if (ZNet.instance.InConnectingScreen()) return ProgressMath.Connection(ProgressStage.Connecting);
    if (ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connecting
        || (ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected && !Player.m_localPlayer)) {
      return ProgressMath.Connection(ProgressStage.ReceivingWorld);
    }
    return new(null);
  }

  static ProgressValue ThrottledArea(Situation situation) {
    float now = Time.realtimeSinceStartup;
    if (situation.Kind == _areaKind && ReferenceEquals(situation.Keys, _areaKeys) && now - _areaReadAt < AreaReadInterval)
      return _area;
    _area = Area(situation);
    _areaKind = situation.Kind;
    _areaKeys = situation.Keys;
    _areaReadAt = now;
    return _area;
  }

  static ProgressValue Area(Situation situation) {
    if (situation.Kind == SituationKind.Death && Game.instance && !Game.instance.m_requestRespawn)
      return ProgressMath.Area(false, 0, 0);
    if (!ZNet.instance || !ZoneSystem.instance || !ZNetScene.instance || ZDOMan.instance == null) return new(null);
    Vector3 target = situation.Kind == SituationKind.Teleport && Player.m_localPlayer
        ? Player.m_localPlayer.m_teleportTargetPos : ZNet.instance.GetReferencePosition();
    Vector2s zone = ZoneSystem.GetZone(target);
    bool zoneReady = ZoneSystem.instance.IsZoneLoaded(zone);
    if (!zoneReady) return ProgressMath.Area(false, 0, 0);
    _nearObjects.Clear();
    ZDOMan.instance.FindSectorObjects(zone, new SimulationDistance(1, 0), _nearObjects);
    int required = 0;
    int instantiated = 0;
    foreach (ZDO zdo in _nearObjects) {
      if (!ZNetScene.instance.IsPrefabZDOValid(zdo)) continue;
      required++;
      if (ZNetScene.instance.FindInstance(zdo)) instantiated++;
    }
    return ProgressMath.Area(true, instantiated, required);
  }

  static ProgressValue Sleep() {
    if (!ZNet.instance || !EnvMan.instance) return new(null);
    double current = ZNet.instance.GetTimeSeconds();
    if (double.IsNaN(_sleepStart)) {
      _sleepStart = current;
      long dayLength = EnvMan.instance.m_dayLengthSec;
      int day = EnvMan.instance.GetDay(current - dayLength * 0.15);
      _sleepMorning = EnvMan.instance.GetMorningStartSec(day + 1);
    }
    return ProgressMath.Sleep(_sleepStart, current, _sleepMorning);
  }
}
