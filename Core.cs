using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Systems.GlobalVars;
using Drova_Modding_API.Systems.SaveGame;
using Drova_Modding_API.Systems.Spawning;
using Drova_Modding_API.Systems.Talents;
using Il2Cpp;
using Il2CppDrova;
using Il2CppDrova.GlobalVarSystem;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.LoadingScreenHandles;
using Il2CppDrova.Saveables;
using Il2CppDrova.Talent;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MageDrova.Core), "MageDrova", "1.0.0", "TrustNoOneElse", null)]
[assembly: MelonGame("Just2D", "Drova")]
[assembly: MelonAdditionalDependencies("Drova_Modding_API")]

namespace MageDrova
{
	public class Core : MelonMod
	{
		private const string DefinitionId = "Rowan";
		private const string TalentName = "ManaReg";
		private const string GVarListName = "Mage_mod";
		private const string GBoolName = "Choose_Path";
		public const string GuidTalent = "6a83177d-4d25-4756-a168-d5ea864a17b8";
		private bool _isInitialized;
		private static bool _isPunishmentActive;
		private int _lastHeroIndex = -1;
		private static bool _delayedPunishment;

		public override void OnInitializeMelon()
		{
			PlayerAccess.OnPlayerFound += OnPlayerAccessOnOnPlayerFound;
			SaveGameSystem.BeforeSaveGameLoaded += OnBeforeSaveGameLoaded;
		}
		private void OnBeforeSaveGameLoaded(Savegame saveGame)
		{
			if (_lastHeroIndex != saveGame._metaInfo._heroIndex)
			{
				MelonLogger.Msg("Reset punishment state");
				_isPunishmentActive = false;
			}
			_lastHeroIndex = saveGame._metaInfo._heroIndex;
		}
		
		private static bool GetGvar(out GBool choosePath)
		{

			var list = ProviderAccess.GVarDatabase.GetGVarListByName(GVarListName);
			if (list == null)
			{
				MelonLogger.Error("GVarList not found");
				choosePath = null;
				return true;
			}
			var baseVar = list.GetGVarByName(GBoolName);
			if (baseVar == null)
			{
				MelonLogger.Error("Choose_Path not found");
				choosePath = null;
				return true;
			}

			choosePath = baseVar.Cast<GBool>();
			return false;
		}
		
		private static void OnGvarBusSystemOnOnGBoolValueChanged(GvarBusSystem.GvarChangeEvent obj)
		{
			if (obj.Name != GBoolName) return;
			GetGvar(out var choosePath);
			if (choosePath == null) return;
			MelonLogger.Msg($"[OnGvarBusSystemOnOnGBoolValueChanged] Choose_Path value: {choosePath.GetValue()}");
			if (choosePath.GetValue()) ActivatePlayerPunishment();
		}

		private static void OnPlayerAccessOnOnPlayerFound(Actor player)
		{
			MelonLogger.Msg("Player found, checking state");
			if (player._talentModule.HasTalentLearned(GuidTalent))
			{
				ActivateRegenMod();
			}
			if (_delayedPunishment)
			{
				_delayedPunishment = false;
				ActivatePlayerPunishment();
			}
			if(GetGvar(out var choosePath) || choosePath == null) return;
			MelonLogger.Msg($"[OnPlayerAccessOnOnPlayerFound] Choose_Path value: {choosePath.GetValue()}");
			if (!choosePath.GetValue())
			{
				GvarBusSystem.OnGBoolValueChanged -= OnGvarBusSystemOnOnGBoolValueChanged;
				GvarBusSystem.OnGBoolValueChanged += OnGvarBusSystemOnOnGBoolValueChanged;
			}
			else
			{
				_isPunishmentActive = true;
				ActivatePlayerPunishment();
			}
		}

		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			base.OnSceneWasLoaded(buildIndex, sceneName);
			if (_isInitialized) return;
			if (sceneName == SceneNames.MainMenu)
			{
				if (ExternalEntityInfoRegistry.TryGetByDefinitionId(DefinitionId, out var entityInfo))
				{
					var talent = ScriptableObject.CreateInstance<TalentContainer>();
					talent.hideFlags = HideFlags.HideAndDontSave;
					talent.name = TalentName;
					talent._talentCategory = TalentCategory.All;
					talent._teachers ??= new Il2CppSystem.Collections.Generic.List<EntityInfo>();
					talent._teachers.Add(entityInfo);
					talent._respeccable = true;
					talent._icons = new TalentIcons();
					talent._talentCostLearnPoints = 20;
					talent._additionalDesc = new LocalizedString("mage_mod", "mana_reg_description_2");
					talent._additionalHeader = new LocalizedString("mage_mod", "mana_reg_description_2");
					talent._feedbackData = new Il2CppSystem.Collections.Generic.List<ITeachedFeedbackData>();
					talent._guid = GuidTalent;
					var loca = new TalentLoca
					{
						_name = LocalizationAccess.GetLocalizedString("mage_mod", "mana_reg_name"),
						_description = LocalizationAccess.GetLocalizedString("mage_mod", "mana_reg_description")
					};
					talent._loca = loca.Cast<ITalentLoca>();
					TalentContainerDatabase.AddTalent(talent);
				}
				_isInitialized = true;
			}
		}

		public static void ActivateRegenMod()
		{
			MelonLogger.Msg("Activating regen mod");
			var passiveReg = PlayerAccess.GetPlayer().GetFlowBehaviour()._passiveReg;
			passiveReg._flowMultStat.CreateModifier(1.5f, ModifiableFloat.Mode.Mult);
			passiveReg._orientationStat.CreateModifier(100, ModifiableFloat.Mode.Add);
		}

		public static void ActivatePlayerPunishment()
		{
			var player = PlayerAccess.GetPlayer();
			if (player == null || !LoadingScreenHandler.Instance.IsWorldReady() || SceneGameHandler.IsLoadingScreenActive)
			{
				MelonLogger.Msg("Player not ready yet, delaying punishment");
				_delayedPunishment = true;
				return;
			}
			MelonLogger.Msg("Player punishment activated");
			
			var stats = player._stats.Cast<PlayerAttributeStats>();
			if (stats.LearningData._healthPerLevel != 5) return;
			stats.LearningData._healthPerLevel /= 2;
			if (_isPunishmentActive) return;
			_isPunishmentActive = true;
			player._health.SetMaxHealth(player._health.MaxHealth / 2);

		}
	}
}
