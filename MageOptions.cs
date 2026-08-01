using Drova_Modding_API.Access;
using Drova_Modding_API.UI.Builder;
using Il2CppCustomFramework.Localization;

namespace MageDrova
{
	internal static class MageOptions
	{
		internal const string PenaltyOptionKey = "MageDrovaPenalty";
		private const string LocalizationTable = "MageDrovaOptions";
		private const string BuilderId = "MageDrova";
		private const string TitleKey = "MageDrovaTitle";
		private const string PenaltyDisclaimerKey = "MageDrovaPenaltyDisclaimer";
		private const string PenaltyLabelKey = "MageDrovaPenaltyLabel";
		private const string PenaltyOnKey = "MageDrovaPenaltyOn";
		private const string PenaltyOffKey = "MageDrovaPenaltyOff";
		private const bool PenaltyDefault = true;

		internal static void Register()
		{
			OptionMenuAccess.Instance.OnOptionMenuOpen += OnOptionMenuOpen;
			OptionMenuAccess.Instance.OnOptionMenuClose += OnOptionMenuClose;
		}

		internal static void RegisterLocalization()
		{
			LocalizationAccess.CreateLocalizationEntries([
				new LocalizationAccess.LocalizationEntry(TitleKey, "MageDrova", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(TitleKey, "MageDrova", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(TitleKey, "MageDrova", LocalizationDB.ELanguage.fr),
				new LocalizationAccess.LocalizationEntry(PenaltyDisclaimerKey, "Halbiert die maximale Lebensenergie und die Lebensenergie pro Stufe.", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyDisclaimerKey, "Halves maximum health and the health gained per level.", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyDisclaimerKey, "Reduit de moitie la sante maximale et la sante gagnee par niveau.", LocalizationDB.ELanguage.fr),
				new LocalizationAccess.LocalizationEntry(PenaltyLabelKey, "Magier-Nachteil", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyLabelKey, "Mage penalty", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyLabelKey, "Malus de mage", LocalizationDB.ELanguage.fr),
				new LocalizationAccess.LocalizationEntry(PenaltyOnKey, "An", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyOnKey, "On", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyOnKey, "Active", LocalizationDB.ELanguage.fr),
				new LocalizationAccess.LocalizationEntry(PenaltyOffKey, "Aus", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyOffKey, "Off", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyOffKey, "Desactive", LocalizationDB.ELanguage.fr),
				], LocalizationTable);
		}

		internal static bool IsPenaltyEnabled()
		{
			if (ConfigAccessor.TryGetConfigValue(PenaltyOptionKey, out bool enabled)) return enabled;
			return PenaltyDefault;
		}

		private static void OnOptionMenuOpen()
		{
			OptionUIBuilder builder = OptionMenuAccess.Instance.GetBuilder(BuilderId);
			if (builder == null) return;

			builder.CreateTitle(LocalizationAccess.GetLocalizedString(LocalizationTable, TitleKey))
				.CreateDisclaimer(LocalizationAccess.GetLocalizedString(LocalizationTable, PenaltyDisclaimerKey))
				.CreateSwitch(
					LocalizationAccess.GetLocalizedString(LocalizationTable, PenaltyLabelKey),
					LocalizationAccess.GetLocalizedString(LocalizationTable, PenaltyOnKey),
					LocalizationAccess.GetLocalizedString(LocalizationTable, PenaltyOffKey),
					PenaltyOptionKey,
					PenaltyDefault)
				.Build();
		}

		private static void OnOptionMenuClose()
		{
			Core.RefreshPenaltyState();
		}
	}
}
