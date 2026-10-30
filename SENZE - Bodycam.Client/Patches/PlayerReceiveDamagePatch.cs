using EFT;
using HarmonyLib;

namespace SenzeBodycam.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.ReceiveDamage))]
internal static class PlayerReceiveDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance, float damage, EDamageType type, float absorbed)
    {
        if (__instance == null || !__instance.IsYourPlayer || damage + absorbed <= 0f)
        {
            return;
        }

        if ((type & EDamageType.Bullet) != 0 || !IsCombatDamage(type))
        {
            return;
        }

        PatchContext.BodycamOverlay?.AddDamage(damage, absorbed);
    }

    private static bool IsCombatDamage(EDamageType type)
    {
        const EDamageType combatDamage = EDamageType.Explosion
            | EDamageType.GrenadeFragment
            | EDamageType.Impact
            | EDamageType.Bullet
            | EDamageType.Melee
            | EDamageType.Landmine
            | EDamageType.Sniper
            | EDamageType.Blunt
            | EDamageType.Btr
            | EDamageType.Artillery
            | EDamageType.HotGases
            | EDamageType.ThermobaricExplosion;

        return (type & combatDamage) != 0;
    }
}
