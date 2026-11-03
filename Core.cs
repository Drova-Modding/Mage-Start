using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Register;
using Drova_Modding_API.Systems.GlobalVars;
using Drova_Modding_API.Systems.Morphing;
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
using UnityEngine.InputSystem;
#if DEBUG
using Drova_Modding_API.Systems.Testing;
using Il2CppDrova.GUI;
#endif

[assembly: MelonInfo(typeof(MageDrova.Core), "MageDrova", "1.3.0", "TrustNoOneElse")]
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
        private const int DefaultHealthPerLevel = 5;
        private const int BasePlayerMaxHealth = 40;
        private bool _isInitialized;
        private static int _appliedMaxHealthPenalty;

        public override void OnInitializeMelon()
        {
            PlayerAccess.OnPlayerFound += OnPlayerAccessOnOnPlayerFound;
            SaveGameSystem.BeforeSaveGameLoaded += OnBeforeSaveGameLoaded;
            MageOptions.Register();
            Morphing.Register();
#if DEBUG
            TestAgent.RegisterCommand("mage_morph", _ => Morphing.LastScaling);
            TestAgent.RegisterCommand("mage_stance", _ => MorphManager.Body is Actor body
                ? $"mode={body.GetInteractionModule().CurrentMode} changing={(body.GetInteractionModule().IsChangingMode ? 1 : 0)}"
                : "none");
            TestAgent.RegisterCommand("mage_learnwindow", _ =>
                LearnWindowInfo(GUIGameHandler.TryGet().GetLearnGUI(true, true)));
#endif

            GVarSyncAccess.MarkListLocal(GVarListName);
        }

        private void OnBeforeSaveGameLoaded(Savegame saveGame)
        {
            MelonLogger.Msg("Reset punishment state");
            _appliedMaxHealthPenalty = 0;
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
            if (choosePath.GetValue()) ApplyPenaltyState();
        }

        private static void OnPlayerAccessOnOnPlayerFound(Actor player)
        {
            MelonLogger.Msg("Player found, checking state");
            if (player._talentModule.HasTalentLearned(GuidTalent))
            {
                ActivateRegenMod();
            }

            _appliedMaxHealthPenalty = 0;
            if (GetGvar(out var choosePath) || choosePath == null) return;
            MelonLogger.Msg($"[OnPlayerAccessOnOnPlayerFound] Choose_Path value: {choosePath.GetValue()}");
            if (!choosePath.GetValue())
            {
                GvarBusSystem.OnGBoolValueChanged -= OnGvarBusSystemOnOnGBoolValueChanged;
                GvarBusSystem.OnGBoolValueChanged += OnGvarBusSystemOnOnGBoolValueChanged;
                DeactivatePlayerPunishment();
                return;
            }

            ApplyPenaltyState();
        }

#if DEBUG
        private static string LearnWindowInfo(GUI_LearnWindow window)
        {
            List<string> shown = [];
            foreach (GUI_Talent_LearnButton button in window._talentButtons)
            {
                if (button.gameObject.activeInHierarchy)
                {
                    shown.Add(button._perkButton.GetTalentContainer()?.name ?? "?");
                }
            }

            return $"{window.name} open={(window.gameObject.activeInHierarchy ? 1 : 0)} buttons={window._talentButtons.Count} shown={string.Join(",", shown)}";
        }
#endif

        public override void OnUpdate()
        {
            Morphing.Update();
            InputAction? endMorph = InputActionRegister.Instance?[MageOptions.EndMorphAction];
            if (endMorph != null && endMorph.WasPressedThisFrame())
            {
                MorphManager.EndMorph();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            base.OnSceneWasLoaded(buildIndex, sceneName);
            if (_isInitialized) return;
            if (sceneName == SceneNames.MainMenu)
            {
                MageOptions.RegisterLocalization();
                Morphing.RegisterLocalization();
                if (ExternalEntityInfoRegistry.TryGetByDefinitionId(DefinitionId, out var entityInfo))
                {
                    AddTalent(entityInfo, TalentName, GuidTalent, 20, "mage_mod", "mana_reg_name", "mana_reg_description");
                    Morphing.AddTalents(entityInfo);
                }

                _isInitialized = true;
            }
        }

        /// <summary>
        /// Registers a talent that <paramref name="teacher"/> teaches, learnable once <paramref name="predecessor"/> is.
        /// </summary>
        internal static TalentContainer AddTalent(EntityInfo teacher, string name, string guid, int cost, string table, string nameKey, string descriptionKey,
            TalentContainer? predecessor = null)
        {
            TalentContainer talent = ScriptableObject.CreateInstance<TalentContainer>();
            talent.hideFlags = HideFlags.HideAndDontSave;
            talent.name = name;
            talent._talentCategory = TalentCategory.All;
            talent._teachers ??= new Il2CppSystem.Collections.Generic.List<EntityInfo>();
            talent._teachers.Add(teacher);
            talent._respeccable = true;
            talent._icons = new TalentIcons();
            talent._talentCostLearnPoints = cost;
            // The tooltip reads both, and an empty one hides its extra block, which would only repeat the description.
            talent._additionalDesc = new LocalizedString();
            talent._additionalHeader = new LocalizedString();
            talent._feedbackData = new Il2CppSystem.Collections.Generic.List<ITeachedFeedbackData>();
            talent._guid = guid;
            TalentLoca loca = new()
            {
                _name = LocalizationAccess.GetLocalizedString(table, nameKey), _description = LocalizationAccess.GetLocalizedString(table, descriptionKey)
            };
            talent._loca = loca.Cast<ITalentLoca>();
            if (predecessor != null)
            {
                TalentContainerDatabase.AddTalent(talent, predecessor);
            }
            else
            {
                TalentContainerDatabase.AddTalent(talent);
            }

            return talent;
        }

        public static void ActivateRegenMod()
        {

            var player = PlayerAccess.GetPlayer();
            if (player == null)
            {
                MelonLogger.Msg("Couldn't activating regen mod");
                return;
            }

            MelonLogger.Msg("Activating regen mod");
            var passiveReg = player.GetFlowBehaviour()._passiveReg;
            passiveReg._flowMultStat.CreateModifier(1.5f, ModifiableFloat.Mode.Mult);
            passiveReg._orientationStat.CreateModifier(100, ModifiableFloat.Mode.Add);
        }

        public static void RefreshPenaltyState()
        {
            if (PlayerAccess.GetPlayer() == null) return;
            if (GetGvar(out var choosePath) || choosePath == null) return;
            if (!choosePath.GetValue()) return;
            ApplyPenaltyState();
        }

        private static void ApplyPenaltyState()
        {
            if (MageOptions.IsPenaltyEnabled())
            {
                ActivatePlayerPunishment();
                return;
            }

            DeactivatePlayerPunishment();
        }

        public static void ActivatePlayerPunishment()
        {
            if (!MageOptions.IsPenaltyEnabled()) return;
            if (!TryGetReadyPlayer(out var player))
            {
                MelonLogger.Msg("Player not ready yet, skipping punishment");
                return;
            }

            RepairBakedInHalving(player);
            SetMaxHealthPenalty(player, -(player._health.BaseMaxHealth / 2));
        }

        public static void DeactivatePlayerPunishment()
        {
            if (!TryGetReadyPlayer(out var player))
            {
                MelonLogger.Msg("Player not ready yet, skipping punishment removal");
                return;
            }

            RepairBakedInHalving(player);
            SetMaxHealthPenalty(player, 0);
        }

        private static bool TryGetReadyPlayer(out Actor player)
        {
            player = PlayerAccess.GetPlayer();
            if (player == null) return false;
            return LoadingScreenHandler.Instance.IsWorldReady() && !SceneGameHandler.IsLoadingScreenActive;
        }

        private static void SetMaxHealthPenalty(Actor player, int penalty)
        {
            int delta = penalty - _appliedMaxHealthPenalty;
            if (delta != 0)
            {
                player._health.AddRuntimeMaxHealth(delta);
            }

            _appliedMaxHealthPenalty = penalty;
            MelonLogger.Msg($"Penalty {penalty} applied (delta {delta}), base {player._health.BaseMaxHealth}, max {player._health.MaxHealth}");
        }

        private static void RepairBakedInHalving(Actor player)
        {
            var stats = player._stats.Cast<PlayerAttributeStats>();
            stats.LearningData._healthPerLevel = DefaultHealthPerLevel;

            int minimumMaxHealth = BasePlayerMaxHealth + (stats.Level - 1) * DefaultHealthPerLevel;
            int missing = minimumMaxHealth - player._health.BaseMaxHealth;
            if (missing <= 0) return;

            player._health.ChangeMaxHealth(missing, false);
            MelonLogger.Msg($"Repaired base max health of level {stats.Level} player by {missing} to {player._health.BaseMaxHealth}");
        }

    }
}