using HarmonyLib;
using Il2CppDrova.Talent;

namespace MageDrova
{
	[HarmonyPatch(typeof(TalentActorModule), nameof(TalentActorModule.LearnTalent))]
	public static class TalentActorModulePatch
	{
		static void Postfix(TalentContainer container)
		{
			if (container.GUID == Core.GuidTalent)
			{
				Core.ActivateRegenMod();
			}
		}
	}
}
