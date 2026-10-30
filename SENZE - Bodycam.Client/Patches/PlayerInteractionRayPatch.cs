using EFT;
using HarmonyLib;
using UnityEngine;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.InteractionRay), MethodType.Getter)]
internal static class PlayerInteractionRayPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance, ref Ray __result)
    {
        if (PatchContext.BodyCamera != null
            && PatchContext.BodyCamera.TryGetInteractionRay(__instance, out var bodycamRay))
        {
            __result = bodycamRay;
        }
    }
}
