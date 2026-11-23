namespace LoadingScreen.Model;

public enum LoadingHoldSafetyAction {
  Keep,
  Protect,
  CloseForPvp,
  CloseForMonster
}

public static class LoadingHoldSafety {
  public static LoadingHoldSafetyAction Decide(
      bool holding, bool invulnerableWhileHeld, bool pvpEnabled, bool hostileMonsterNoticed) {
    if (!holding) return LoadingHoldSafetyAction.Keep;
    if (invulnerableWhileHeld) return LoadingHoldSafetyAction.Protect;
    if (pvpEnabled) return LoadingHoldSafetyAction.CloseForPvp;
    if (hostileMonsterNoticed) return LoadingHoldSafetyAction.CloseForMonster;
    return LoadingHoldSafetyAction.Keep;
  }
}
