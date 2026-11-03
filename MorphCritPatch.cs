using HarmonyLib;
using Il2CppDrova;
using Il2CppDrova.ActorActions;

namespace MageDrova
{
    /// <summary>
    /// Creature attacks are authored with a crit multiplier of 1, so a morph's Dexterity crits would do nothing.
    /// Rowan's creatures crit like the player's own weapons. Crits only roll for the player, so wild creatures are unchanged.
    /// </summary>
    [HarmonyPatch(typeof(AA_AttackAction_Settings), nameof(AA_AttackAction_Settings.CalcCritMultiplier))]
    public static class MorphCritPatch
    {
        static void Postfix(Entity source, ref float __result)
        {
            if (Morphing.IsOwnBody(source))
            {
                __result = Math.Max(__result, Morphing.CritMultiplier);
            }
        }
    }
}
