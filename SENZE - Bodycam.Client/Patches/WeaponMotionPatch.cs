using EFT.Animations;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.AvoidObstacles))]
internal static class WeaponMotionPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ProceduralWeaponAnimation __instance)
    {
        PatchContext.BodyCamera?.ApplyWeaponCornerPeek(__instance);
    }
}
