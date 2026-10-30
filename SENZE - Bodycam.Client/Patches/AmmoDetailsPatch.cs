using System;
using System.Reflection;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch]
internal static class AmmoDetailsPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        var ownerType = AccessTools.TypeByName("EFT.GamePlayerOwner");
        return AccessTools.Method(
            ownerType,
            "PlayerOnOnShowAmmoDetails",
            new[] { typeof(int), typeof(int), typeof(int), typeof(string), typeof(bool) });
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        return PatchContext.BodycamOverlay?.ShouldReplaceMagazineCheckDetails() != true;
    }
}
