using Drova_Modding_API.Access;
using Drova_Modding_API.Register;
using Drova_Modding_API.UI.Builder;
using Il2CppCustomFramework.Localization;
using UnityEngine.InputSystem;

namespace MageDrova
{
	internal static class MageOptions
	{
		internal const string PenaltyOptionKey = "MageDrovaPenalty";
		internal const string EndMorphAction = "MageDrova_EndMorph";
		private const string LocalizationTable = "MageDrovaOptions";
		private const string BuilderId = "MageDrova";
		private const string TitleKey = "MageDrovaTitle";
		private const string PenaltyDisclaimerKey = "MageDrovaPenaltyDisclaimer";
		private const string PenaltyLabelKey = "MageDrovaPenaltyLabel";
		private const string PenaltyOnKey = "MageDrovaPenaltyOn";
		private const string PenaltyOffKey = "MageDrovaPenaltyOff";
		private const string EndMorphLabelKey = "MageDrovaEndMorphLabel";
		private const bool PenaltyDefault = true;

		internal static void Register()
		{
			OptionMenuAccess.Instance.OnOptionMenuOpen += OnOptionMenuOpen;
			OptionMenuAccess.Instance.OnOptionMenuClose += OnOptionMenuClose;
			InputActionRegister.OnInputActionsLoaded += RegisterEndMorphKey;
			if (InputActionRegister.Instance != null) RegisterEndMorphKey();
		}

		internal static void RegisterLocalization()
		{
			LocalizationAccess.CreateLocalizationEntries([
				new LocalizationAccess.LocalizationEntry(TitleKey, "MageDrova", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(TitleKey, "MageDrova", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyDisclaimerKey, "Halbiert die maximale Lebensenergie und die Lebensenergie pro Stufe.", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyDisclaimerKey, "Halves maximum health and the health gained per level.", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyLabelKey, "Magier-Nachteil", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyLabelKey, "Mage penalty", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyOnKey, "An", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyOnKey, "On", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(PenaltyOffKey, "Aus", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(PenaltyOffKey, "Off", LocalizationDB.ELanguage.en),
				new LocalizationAccess.LocalizationEntry(EndMorphLabelKey, "Verwandlung beenden", LocalizationDB.ELanguage.de),
				new LocalizationAccess.LocalizationEntry(EndMorphLabelKey, "End morph", LocalizationDB.ELanguage.en),
				], LocalizationTable);
		}

		/// <summary>
		/// H is the one letter Drova binds to nothing. The options row finds the binding by its control group.
		/// </summary>
		private static void RegisterEndMorphKey()
		{
			InputActionRegister.Instance.AddActionToGameplayMap(EndMorphAction)?.AddBinding("<Keyboard>/h", null, null, "Keyboard;Mouse");
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
				.CreateInputActionSection([new OptionUIBuilder.InputActionTemplate(LocalizationAccess.GetLocalizedString(LocalizationTable, EndMorphLabelKey), EndMorphAction)])
				.Build();
		}

		private static void OnOptionMenuClose()
		{
			Core.RefreshPenaltyState();
		}
	}
}
