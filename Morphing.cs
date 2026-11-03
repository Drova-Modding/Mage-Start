using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Morphing;
using Il2Cpp;
using Il2CppCustomFramework.Localization;
using Il2CppDrova;
using Il2CppDrova.Items.Stats;
using Il2CppDrova.Talent;
using MelonLoader;
using UnityEngine;

namespace MageDrova
{
    /// <summary>
    /// Rowan's morph spells (<c>Items/mage_mod.json</c>) scale with the caster's level and attributes, and his morph talents add to them.
    /// The API morphs only the local player, so a teammate's talents never touch this machine's creature.
    /// </summary>
    internal static class Morphing
    {
        /// <summary>
        /// Crit damage factor of the creature's attacks, the one of the player's swords, axes and spears.
        /// </summary>
        internal const float CritMultiplier = 2f;

        private const string LocalizationTable = "MageDrovaMorph";
        private const float HealthPerLevel = 0.03f;
        /// <summary>
        /// Damage per level past a form's unlock level. Weapons keep improving after the last form unlocks.
        /// </summary>
        private const float DamagePerLevel = 0.05f;
        private const float BondHealth = 0.25f;
        private const float BondDamage = 0.15f;
        private const int BondArmor = 10;
        private const float LingeringDuration = 1.5f;
        private const float RecoveryShare = 0.2f;
        private const float FeastShare = 0.15f;

        /// <summary>
        /// Seconds after the start in which a morph is held in combat stance. The game drops the creature back to normal
        /// stance shortly after the start. Ranged attacks pass through normal stance later, so it is not held longer.
        /// </summary>
        private const float DrawWeaponSeconds = 2f;
        private const string BeastBond1 = "4192a8ff-966e-4156-aff7-5718d77c9c89";
        private const string BeastBond2 = "2111ec58-5a24-4b96-9d62-96d897f73fc9";
        private const string BeastBond3 = "0ed8e518-04e8-480c-b702-80530dd5ec4f";
        private const string FeralRecovery = "76e8c94f-c247-410b-ae1a-fa3c586408ab";
        private const string PredatorsFeast = "156b7f06-330f-456c-946e-58d313bf9794";
        private const string LingeringForm = "5660ae1e-ce58-47ac-8d9d-0d3c53d9c7be";
        private const string SecondSkin = "da1b81b3-91b8-4516-8d5b-34c155961d3c";

        /// <summary>
        /// Health share at level 1, damage factor and the spell's RequiredLevel in Items/mage_mod.json.
        /// Creatures swing slower than a sword combo, so the damage factor lifts each form to a weapon of its level.
        /// </summary>
        private static readonly Dictionary<string, (float Health, float Damage, int Level)> FormByCreature = new(StringComparer.OrdinalIgnoreCase)
        {
            [AddressableAccess.Creatures.WaterRat.AssetGUID] = (1f, 1f, 1),
            [AddressableAccess.Creatures.Bloodquito_Auwald.AssetGUID] = (1f, 1.1f, 3),
            [AddressableAccess.Creatures.HungryRipper.AssetGUID] = (1f, 1f, 6),
            [AddressableAccess.Creatures.Bloodquito_Rotmoor.AssetGUID] = (0.8f, 1f, 8),
            [AddressableAccess.Creatures.Ripper.AssetGUID] = (0.35f, 1.2f, 10),
            [AddressableAccess.Creatures.Boar.AssetGUID] = (0.35f, 1f, 12),
            [AddressableAccess.Creatures.ShadowRipper.AssetGUID] = (0.25f, 1.1f, 15),
            [AddressableAccess.Creatures.Harpy.AssetGUID] = (0.2f, 1.3f, 18),
            [AddressableAccess.Creatures.Bear.AssetGUID] = (0.2f, 2f, 20)
        };

        /// <summary>
        /// Each talent after the talent it needs, if any.
        /// </summary>
        private static readonly (string Name, string Guid, int Cost, string? Predecessor)[] Talents =
        [
            ("BeastBond1", BeastBond1, 10, null),
            ("BeastBond2", BeastBond2, 15, BeastBond1),
            ("BeastBond3", BeastBond3, 20, BeastBond2),
            ("FeralRecovery", FeralRecovery, 10, null),
            ("PredatorsFeast", PredatorsFeast, 10, null),
            ("LingeringForm", LingeringForm, 15, null),
            ("SecondSkin", SecondSkin, 20, null)
        ];

        private static readonly (string Key, string En, string De)[] Texts =
        [
            ("BeastBond1_name", "Beast Bond I", "Tierbund I"),
            ("BeastBond1_description", "Your creature forms get 25% more health and deal 15% more damage.", "Deine Tiergestalten haben 25 % mehr Lebenspunkte und verursachen 15 % mehr Schaden."),
            ("BeastBond2_name", "Beast Bond II", "Tierbund II"),
            ("BeastBond2_description", "Your creature forms get another 25% health and deal another 15% damage. Needs Beast Bond I.",
            "Deine Tiergestalten haben weitere 25 % Lebenspunkte und verursachen weitere 15 % Schaden. Erfordert Tierbund I."),
            ("BeastBond3_name", "Beast Bond III", "Tierbund III"),
            ("BeastBond3_description", "Another 25% health and 15% damage, and your creature forms get 10 armor. Needs Beast Bond II.",
            "Weitere 25 % Lebenspunkte und 15 % Schaden, und deine Tiergestalten erhalten 10 Rüstung. Erfordert Tierbund II."),
            ("FeralRecovery_name", "Feral Recovery", "Wilde Erholung"),
            ("FeralRecovery_description", "When a morph ends, you heal 20% of your health.", "Endet eine Verwandlung, heilst du 20 % deiner Lebenspunkte."),
            ("PredatorsFeast_name", "Predator's Feast", "Festmahl des Jägers"),
            ("PredatorsFeast_description", "Every kill in creature form heals the creature by 15% of its health.", "Jede Beute in Tiergestalt heilt die Kreatur um 15 % ihrer Lebenspunkte."),
            ("LingeringForm_name", "Lingering Form", "Beständige Gestalt"),
            ("LingeringForm_description", "Your morphs last 50% longer.", "Deine Verwandlungen halten 50 % länger."),
            ("SecondSkin_name", "Second Skin", "Zweite Haut"),
            ("SecondSkin_description", "When your creature form dies, you survive with 1 health.", "Stirbt deine Tiergestalt, überlebst du mit 1 Lebenspunkt."),
            ("mage_morph_waterrat_name", "Water Rat Form", "Gestalt der Wasserratte"),
            ("mage_morph_waterrat_description", "Turns you into a water rat. Costs 10% health. Needs level 1.", "Verwandelt dich in eine Wasserratte. Kostet 10 % Lebenspunkte. Ab Level 1."),
            ("mage_morph_bloodfly_name", "Giant Gnat Form", "Gestalt der Riesenmücke"),
            ("mage_morph_bloodfly_description", "Turns you into a flying giant gnat. Costs 10% health. Needs level 3.",
            "Verwandelt dich in eine fliegende Riesenmücke. Kostet 10 % Lebenspunkte. Ab Level 3."),
            ("mage_morph_hungryripper_name", "Famished Ripper Form", "Gestalt des ausgehungerten Reißers"),
            ("mage_morph_hungryripper_description", "Turns you into a famished ripper. Costs 15% health. Needs level 6.",
            "Verwandelt dich in einen ausgehungerten Reißer. Kostet 15 % Lebenspunkte. Ab Level 6."),
            ("mage_morph_rotmoorfly_name", "Bloodfly Form", "Gestalt der Blutfliege"),
            ("mage_morph_rotmoorfly_description", "Turns you into a flying bloodfly. Costs 15% health. Needs level 8.",
            "Verwandelt dich in eine fliegende Blutfliege. Kostet 15 % Lebenspunkte. Ab Level 8."),
            ("mage_morph_ripper_name", "Ripper Form", "Gestalt des Reißers"),
            ("mage_morph_ripper_description", "Turns you into a ripper. Costs 20% health. Needs level 10.", "Verwandelt dich in einen Reißer. Kostet 20 % Lebenspunkte. Ab Level 10."),
            ("mage_morph_boar_name", "Boar Form", "Gestalt des Ebers"),
            ("mage_morph_boar_description", "Turns you into a boar. Costs 20% health. Needs level 12.", "Verwandelt dich in einen Eber. Kostet 20 % Lebenspunkte. Ab Level 12."),
            ("mage_morph_shadowripper_name", "Shadowripper Form", "Gestalt des Schattenreißers"),
            ("mage_morph_shadowripper_description", "Turns you into a shadowripper. Costs 25% health. Needs level 15.", "Verwandelt dich in einen Schattenreißer. Kostet 25 % Lebenspunkte. Ab Level 15."),
            ("mage_morph_harpy_name", "Harpy Form", "Gestalt der Harpyie"),
            ("mage_morph_harpy_description", "Turns you into a harpy. Costs 25% health. Needs level 18.", "Verwandelt dich in eine Harpyie. Kostet 25 % Lebenspunkte. Ab Level 18."),
            ("mage_morph_bear_name", "Bear Form", "Gestalt des Bären"),
            ("mage_morph_bear_description", "Turns you into a bear. Costs 30% health. Needs level 20.", "Verwandelt dich in einen Bären. Kostet 30 % Lebenspunkte. Ab Level 20."),
            ("mage_recipe_morph_waterrat_name", "Spell: Water Rat Form", "Zauber: Gestalt der Wasserratte"),
            ("mage_recipe_morph_bloodfly_name", "Spell: Giant Gnat Form", "Zauber: Gestalt der Riesenmücke"),
            ("mage_recipe_morph_hungryripper_name", "Spell: Famished Ripper Form", "Zauber: Gestalt des ausgehungerten Reißers"),
            ("mage_recipe_morph_rotmoorfly_name", "Spell: Bloodfly Form", "Zauber: Gestalt der Blutfliege"),
            ("mage_recipe_morph_ripper_name", "Spell: Ripper Form", "Zauber: Gestalt des Reißers"),
            ("mage_recipe_morph_boar_name", "Spell: Boar Form", "Zauber: Gestalt des Ebers"),
            ("mage_recipe_morph_shadowripper_name", "Spell: Shadowripper Form", "Zauber: Gestalt des Schattenreißers"),
            ("mage_recipe_morph_harpy_name", "Spell: Harpy Form", "Zauber: Gestalt der Harpyie"),
            ("mage_recipe_morph_bear_name", "Spell: Bear Form", "Zauber: Gestalt des Bären"),
            ("recipe_description",
            "Teaches a morph when you carry a runestone. Mind makes it last longer and hit harder, Strength makes it tougher, Dexterity crits more often. In creature form the weapon swap key switches its attacks and the dodge key uses its special attack.",
            "Lehrt eine Verwandlung, wenn du einen Runenstein trägst. Geist verlängert sie und erhöht den Schaden, Stärke macht sie zäher, Geschick erhöht die Chance auf kritische Treffer. In Tiergestalt wechselt die Waffenwechsel-Taste die Angriffe, die Ausweichen-Taste löst den Spezialangriff aus."),
        ];

        private static readonly string[] MorphIds = ["waterrat", "bloodfly", "hungryripper", "rotmoorfly", "ripper", "boar", "shadowripper", "harpy", "bear"];

        /// <summary>
        /// Whether the running or last morph is one of Rowan's. The API clears the creature guid before the end event.
        /// </summary>
        private static bool _isOwnMorph;

        private static float _drawWeaponUntil;
        /// <summary>
        /// The factors the last Rowan morph got, for the log and the test agent.
        /// </summary>
        internal static string LastScaling { get; private set; } = "none";

        internal static void Register()
        {
            MorphManager.OnMorphStarted += OnMorphStarted;
            MorphManager.OnMorphEnded += OnMorphEnded;
            MorphManager.AdjustDuration = AdjustDuration;
            MorphManager.SpareCaster = SpareCaster;
            GameEvents.OnPlayerKilledActor += OnPlayerKilledActor;
        }

        internal static void AddTalents(EntityInfo teacher)
        {
            Dictionary<string, TalentContainer> added = [];
            foreach ((string name, string guid, int cost, string? predecessor) in Talents)
            {
                TalentContainer? before = predecessor != null ? added[predecessor] : null;
                added[guid] = Core.AddTalent(teacher, name, guid, cost, LocalizationTable, name + "_name", name + "_description", predecessor: before);
            }
        }

        private static void OnMorphStarted(Actor caster, Actor body)
        {
            _isOwnMorph = MorphManager.BodyGuid != null && FormByCreature.ContainsKey(MorphManager.BodyGuid);
            if (!_isOwnMorph)
            {
                return;
            }

            _drawWeaponUntil = Time.time + DrawWeaponSeconds;
            AttributeStats stats = caster.GetStats();
            StatContainer container = GlobalStatsSettings.GetStatContainer();
            int bond = BondRanks(caster);
            (float healthFactor, float damageFactor, int requiredLevel) = FormByCreature[MorphManager.BodyGuid!];

            // Strength adds health as it adds weapon damage. Mind adds damage as it adds spell damage.
            float strength = DamageCalculator.AdditionalStrengthDmg(Attribute(stats, container.StrengthStat), 1f, container.StrengthGlobalScalingMultiplyer);
            float health = healthFactor * (1f + HealthPerLevel * (stats.Level - 1)) * (1f + strength) * (1f + BondHealth * bond);
            float levelsPast = 1f + DamagePerLevel * Math.Max(0, stats.Level - requiredLevel);
            float damage = damageFactor * (1f + Mind(caster)) * levelsPast * (1f + BondDamage * bond);
            bool buffed = ActorBuffAccess.TryApply(body, new ActorBuff(health, damage, bond >= 3 ? BondArmor : 0));

            // The same crit chance Dexterity gives the player. The creature rolls it, since it counts as the player while morphed.
            float crit = CritData.CalcCritChanceFromDex(Attribute(stats, container.DexStat));
            body.GetStats().GetGenericStat(container.CritChanceStat).CreateModifier(crit, ModifiableFloat.Mode.Add);
            LastScaling = $"health={health:0.###} damage={damage:0.###} crit={crit:0.###} bond={bond} buffed={(buffed ? 1 : 0)}";
            MelonLogger.Msg($"[Morph] {body.name}: {LastScaling}");
        }

        private static float AdjustDuration(Actor caster, string creature, float duration)
        {
            if (!FormByCreature.ContainsKey(creature))
            {
                return duration;
            }

            // Mind lengthens the morph as much as it raises a mage's spell damage.
            float scaled = duration * (1f + Mind(caster));
            return HasTalent(caster, LingeringForm) ? scaled * LingeringDuration : scaled;
        }

        private static void OnMorphEnded(Actor caster)
        {
            if (_isOwnMorph && HasTalent(caster, FeralRecovery))
            {
                DamageAccess.TryHeal(caster, Mathf.RoundToInt(caster.GetHealth().MaxHealth * RecoveryShare));
            }
        }

        private static bool SpareCaster(Actor caster)
        {
            if (!_isOwnMorph || !HasTalent(caster, SecondSkin))
            {
                return false;
            }

            caster.GetHealth().SetHealth(1, null);
            return true;
        }

        private static void OnPlayerKilledActor(Actor victim)
        {
            Actor? caster = MorphManager.Caster;
            Actor? body = MorphManager.Body;
            if (!_isOwnMorph || caster == null || body == null || !HasTalent(caster, PredatorsFeast))
            {
                return;
            }

            DamageAccess.TryHeal(body, Mathf.RoundToInt(body.GetHealth().MaxHealth * FeastShare));
        }

        /// <summary>
        /// Called every frame. Puts a fresh creature into combat stance, so it can attack without drawing a weapon first.
        /// </summary>
        internal static void Update()
        {
            Actor? body = MorphManager.Body;
            if (!_isOwnMorph || body == null || Time.time > _drawWeaponUntil)
            {
                return;
            }

            Actor.InteractionModeModule stance = body.GetInteractionModule();
            if (stance.CurrentMode == Actor.EInteractionMode.Normal && !stance.IsChangingMode)
            {
                stance.RequestMode(Actor.EInteractionMode.Combat);
            }
        }

        /// <summary>
        /// Whether <paramref name="entity"/> is the creature of a running Rowan morph.
        /// </summary>
        internal static bool IsOwnBody(Entity? entity)
        {
            Actor? body = MorphManager.Body;
            return _isOwnMorph && entity != null && body != null && entity.Pointer == body.Pointer;
        }

        private static int BondRanks(Actor caster)
        {
            return (HasTalent(caster, BeastBond1) ? 1 : 0) + (HasTalent(caster, BeastBond2) ? 1 : 0) + (HasTalent(caster, BeastBond3) ? 1 : 0);
        }

        private static bool HasTalent(Actor caster, string guid)
        {
            return caster._talentModule.HasTalentLearned(guid);
        }

        private static float Mind(Actor caster)
        {
            return DamageCalculator.AdditionalMindDmg(Attribute(caster.GetStats(), GlobalStatsSettings.GetStatContainer().MindStat), 1f, 1f);
        }

        private static float Attribute(AttributeStats stats, GenericStatDesc attribute)
        {
            return stats.GetGenericStat(attribute).GetFloatValue();
        }

        internal static void RegisterLocalization()
        {
            List<LocalizationAccess.LocalizationEntry> entries = [];
            foreach ((string key, string en, string de) in Texts)
            {
                // Every recipe shares one description, under its own key.
                IEnumerable<string> keys = key == "recipe_description" ? MorphIds.Select(id => "mage_recipe_morph_" + id + "_description") : [key];
                foreach (string entryKey in keys)
                {
                    entries.Add(new LocalizationAccess.LocalizationEntry(entryKey, en, LocalizationDB.ELanguage.en));
                    entries.Add(new LocalizationAccess.LocalizationEntry(entryKey, de, LocalizationDB.ELanguage.de));
                }
            }

            LocalizationAccess.CreateLocalizationEntries(entries, LocalizationTable);
        }
    }
}