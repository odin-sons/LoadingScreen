namespace LoadingScreen;

using HarmonyLib;

[HarmonyPatch(typeof(Character))]
static class CharacterPatch {
  static bool CanTakeDamage(Character character) =>
      character != Player.m_localPlayer || !PluginConfig.IsModEnabled.Value
      || !PluginConfig.InvulnerableWhileHeld.Value || !HudPatch.IsHolding;

  [HarmonyPrefix]
  [HarmonyPatch("RPC_Damage")]
  static bool RpcDamagePrefix(Character __instance) => CanTakeDamage(__instance);

  [HarmonyPrefix]
  [HarmonyPatch(nameof(Character.ApplyDamage))]
  static bool ApplyDamagePrefix(Character __instance) => CanTakeDamage(__instance);
}
