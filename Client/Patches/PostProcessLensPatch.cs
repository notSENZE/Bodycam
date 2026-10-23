using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace SenzeBodycam.Patches;

[HarmonyPatch(
    typeof(PostProcessLayer),
    nameof(PostProcessLayer.UpdateVolumeSystem),
    typeof(Camera),
    typeof(CommandBuffer))]
internal static class PostProcessLensPatch
{
    [HarmonyPostfix]
    private static void Postfix(PostProcessLayer __instance, Camera __0)
    {
        PatchContext.BodyCamera?.ApplyLensDistortion(__instance, __0);
    }
}
