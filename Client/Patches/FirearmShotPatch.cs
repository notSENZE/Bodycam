using EFT;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(Player.FirearmController), nameof(Player.FirearmController.Shot))]
internal static class FirearmShotPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player.FirearmController __instance)
    {
        var player = __instance?._player;
        if (player?.IsYourPlayer == true)
        {
            PatchContext.BodyCamera?.AddShotKick(player);
        }
    }
}
