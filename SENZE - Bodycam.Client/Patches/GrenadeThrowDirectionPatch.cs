using EFT;
using HarmonyLib;
using UnityEngine;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(Player.BaseGrenadeHandsController), nameof(Player.BaseGrenadeHandsController.ThrowGrenadeInner))]
internal static class GrenadeThrowDirectionPatch
{
    [HarmonyPrefix]
    private static void Prefix(Player.BaseGrenadeHandsController __instance, ref Vector3 direction)
    {
        var player = __instance?._player;
        if (PatchContext.BodyCamera != null
            && PatchContext.BodyCamera.TryAdjustGrenadeDirection(
                player,
                direction,
                out var adjustedDirection))
        {
            direction = adjustedDirection;
        }
    }
}
