using EFT.CameraControl;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.LateUpdate))]
internal static class PlayerCameraLateUpdatePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PlayerCameraController __instance)
    {
        PatchContext.BodyCamera?.Apply(__instance);
        PatchContext.BodycamOverlay?.ApplyCameraEffects(__instance);
    }
}
