using EFT;
using EFT.Ballistics;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.ApplyShot))]
internal static class PlayerApplyShotPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Player __instance,
        DamageInfo damageInfo,
        ShotId shotId,
        PlayerHitInfo __result)
    {
        if (__instance == null
            || !__instance.IsYourPlayer
            || __result == null
            || (damageInfo.DamageType & EDamageType.Bullet) == 0)
        {
            return;
        }

        PatchContext.BodycamOverlay?.AddProjectileDamage(damageInfo, shotId);
    }
}
