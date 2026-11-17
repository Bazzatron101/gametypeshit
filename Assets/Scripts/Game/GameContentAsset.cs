using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// The one list of everything in the game: player tuning, progression, skills, modifiers, gear, enemies, bosses and gates. Every
    /// scene (the greybox, the test arena, later gates) points at this asset instead of keeping its
    /// own lists, so content is defined once. The content validation test checks that no asset of
    /// these types sits outside it.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Game Content", fileName = "GameContent")]
    public sealed class GameContentAsset : ScriptableObject
    {
        [SerializeField] private PlayerTuningAsset playerTuning;
        [SerializeField] private ProgressionAsset progression;
        [SerializeField, Tooltip("The classes a new character picks from, in the order shown")] private ClassDefinitionAsset[] classes = new ClassDefinitionAsset[0];
        [SerializeField, Tooltip("Starters (unlock level 1) fill the bar in this order")] private SkillDefinitionAsset[] skills = new SkillDefinitionAsset[0];
        [SerializeField] private ModifierDefinitionAsset[] modifiers = new ModifierDefinitionAsset[0];
        [SerializeField, Tooltip("Every piece of gear (ticket 23); gates' gear pools name pieces from here")] private GearDefinitionAsset[] gear = new GearDefinitionAsset[0];
        [SerializeField] private EnemyDefinitionAsset[] enemies = new EnemyDefinitionAsset[0];
        [SerializeField, Tooltip("Every gate, each a chain of room pieces from the kit")] private GateDefinitionAsset[] gates = new GateDefinitionAsset[0];
        [SerializeField, Tooltip("Every boss; room pieces place them in gates")] private BossDefinitionAsset[] bosses = new BossDefinitionAsset[0];
        [SerializeField, Tooltip("The hub the party gathers in between gates (used by scenes that ask for it)")] private HubAsset hub;
        [SerializeField, Tooltip("Text-only story scenes and what makes each play")] private TextSceneAsset[] scenes = new TextSceneAsset[0];
        [SerializeField, Tooltip("How gates' enemies scale with party size")] private PartyScalingAsset partyScaling;
        [SerializeField, Tooltip("Stores that buy duplicates, wherever they stand")] private ShopAsset[] shops = new ShopAsset[0];
        [SerializeField, Tooltip("The odds of each rarity in a gate's clear draw, and how gate rank and Luck tip them (ticket 61)")] private RarityOdds rarityOdds = RarityOdds.Default;
        [SerializeField, Tooltip("The species a hunter can be (ticket 81), in the order the picker shows them. Saves name them by id: never rename an id")] private Species[] species = Species.Standard();

        /// <summary>One species as authored (ticket 81). See <see cref="SpeciesDefinition"/> and <see cref="SpeciesPerk"/> for what the perk numbers mean.</summary>
        [System.Serializable]
        public struct Species
        {
            [Tooltip("Stable, unique id. Saves and snapshots refer to it; never rename it once players have hunters")] public string id;
            public string displayName;
            [TextArea, Tooltip("Shown in the species picker and on the stat screen")] public string description;
            [Tooltip("The stat the species is good at")] public Stat stat;
            [Min(0), Tooltip("Added to the stat at E-rank")] public int bonusAtFirstRank;
            [Min(0), Tooltip("Added on top at each rank promotion")] public int bonusPerRank;
            [Tooltip("The species' small perk")] public SpeciesPerk perk;
            [Tooltip("Ticket 90: how the hunter travels when they mount up. Every style goes at the same speed")] public TravelStyle travel;
            [Min(0), Tooltip("The perk's size: metres for knockback; Luck points for Lucky; a percentage for the rest")] public float perkAmount;
            [Min(0), Tooltip("FinishingBlow: the enemy's health %, at or below. RestfulHealing: quiet seconds first. Unused by the rest")] public float perkThreshold;

            public SpeciesDefinition ToDefinition() => new SpeciesDefinition
            {
                Id = id, Name = displayName, Description = description, BonusStat = stat, BonusAtFirstRank = bonusAtFirstRank, BonusPerRank = bonusPerRank,
                Perk = perk, PerkAmount = perkAmount, PerkThreshold = perkThreshold, Travel = travel,
            };

            public static Species From(SpeciesDefinition d) => new Species
            {
                id = d.Id, displayName = d.Name, description = d.Description, stat = d.BonusStat, bonusAtFirstRank = d.BonusAtFirstRank, bonusPerRank = d.BonusPerRank,
                perk = d.Perk, perkAmount = d.PerkAmount, perkThreshold = d.PerkThreshold, travel = d.Travel,
            };

            /// <summary>The eight the game ships with (ticket 81).</summary>
            public static Species[] Standard() => System.Array.ConvertAll(SpeciesDefinition.Standard(), From);
        }

        /// <summary>The rarity odds as authored (ticket 61). See <see cref="RarityOddsDefinition"/>.</summary>
        [System.Serializable]
        public struct RarityOdds
        {
            [Min(1), Tooltip("Weight of one Common item in a pool")] public int common;
            [Min(1), Tooltip("Weight of one Rare item")] public int rare;
            [Min(1), Tooltip("Weight of one Epic item")] public int epic;
            [Min(1), Tooltip("Weight of one Legendary item")] public int legendary;
            [Min(0), Tooltip("Per rank the gate is above E: each tier above Common weighs this many percent more, per tier")] public int rankBonusPercent;
            [Min(0), Tooltip("Per Luck point above the start: each tier above Common weighs this many percent more, per tier")] public int luckBonusPercent;

            public static RarityOdds Default => new RarityOdds { common = 60, rare = 28, epic = 10, legendary = 2, rankBonusPercent = 25, luckBonusPercent = 2 };

            public RarityOddsDefinition ToDefinition()
            {
                var odds = common == 0 && rare == 0 && epic == 0 && legendary == 0 ? Default : this; // never set (an asset from before ticket 61)
                return new RarityOddsDefinition
                {
                    BaseWeights = new[] { odds.common, odds.rare, odds.epic, odds.legendary },
                    RankBonusPercent = odds.rankBonusPercent, LuckBonusPercent = odds.luckBonusPercent,
                };
            }
        }

        public PlayerTuningAsset PlayerTuning => playerTuning;
        public ProgressionAsset Progression => progression;
        public IReadOnlyList<ClassDefinitionAsset> Classes => classes;
        public IReadOnlyList<SkillDefinitionAsset> Skills => skills;
        public IReadOnlyList<ModifierDefinitionAsset> Modifiers => modifiers;
        public IReadOnlyList<GearDefinitionAsset> Gear => gear;
        public IReadOnlyList<EnemyDefinitionAsset> Enemies => enemies;
        public IReadOnlyList<GateDefinitionAsset> Gates => gates;
        public IReadOnlyList<BossDefinitionAsset> Bosses => bosses;
        public HubAsset Hub => hub;
        public IReadOnlyList<TextSceneAsset> Scenes => scenes;
        public PartyScalingAsset PartyScaling => partyScaling;
        public IReadOnlyList<ShopAsset> Shops => shops;
        public IReadOnlyList<Species> SpeciesList => species ?? new Species[0];

        /// <summary>
        /// Fills the content parts of a simulation config. The runner and the content tests both go
        /// through here, so they see exactly the same data. Empty list entries are skipped with a warning.
        /// </summary>
        public void ApplyTo(SimulationConfig config)
        {
            if (playerTuning != null) playerTuning.ApplyTo(config);
            config.Progression = progression != null ? progression.ToProgression() : new ProgressionDefinition();
            config.StatEffects = progression != null ? progression.ToStatEffects() : new StatEffectsDefinition();

            var classList = new List<ClassDefinition>();
            foreach (var asset in classes)
            {
                if (asset == null) { Debug.LogWarning($"{name}: an empty entry in the classes list was skipped.", this); continue; }
                classList.Add(asset.ToDefinition());
            }
            config.Classes = classList.ToArray();
            config.Species = species != null && species.Length > 0 ? System.Array.ConvertAll(species, s => s.ToDefinition()) : SpeciesDefinition.Standard(); // ticket 81

            var list = new List<SkillDefinition>();
            foreach (var asset in skills)
            {
                if (asset == null) { Debug.LogWarning($"{name}: an empty entry in the skills list was skipped.", this); continue; }
                list.Add(asset.ToDefinition());
            }
            config.Skills = list.ToArray();

            // One definition per gear asset, shared by the gear list and the gates' pools.
            var gearDefinitions = new Dictionary<GearDefinitionAsset, GearDefinition>();
            var gearList = new List<GearDefinition>();
            foreach (var asset in gear ?? new GearDefinitionAsset[0])
            {
                if (asset == null) { Debug.LogWarning($"{name}: an empty entry in the gear list was skipped.", this); continue; }
                gearList.Add(asset.ToDefinition(gearDefinitions));
            }
            config.Gear = gearList.ToArray();
            config.RarityOdds = rarityOdds.ToDefinition();

            // One simulation definition per enemy asset across all gates, so snapshots write each once.
            var enemyDefinitions = new Dictionary<ScriptableObject, EnemyDefinition>();
            var gateList = new List<GateDefinition>();
            foreach (var asset in gates)
            {
                if (asset == null) { Debug.LogWarning($"{name}: an empty entry in the gates list was skipped.", this); continue; }
                gateList.Add(asset.ToDefinition(enemyDefinitions, gearDefinitions));
            }
            config.Gates = gateList.ToArray();
            config.Hub = hub != null ? hub.ToDefinition() : null;
            config.PartyScaling = partyScaling != null ? partyScaling.ToDefinition() : new PartyScalingDefinition();

            var sceneList = new List<StorySceneDefinition>();
            foreach (var scene in scenes)
            {
                if (scene == null) { Debug.LogWarning($"{name}: an empty entry in the scenes list was skipped.", this); continue; }
                sceneList.Add(scene.ToDefinition());
            }
            config.StoryScenes = sceneList.ToArray();

            var shopList = new List<ShopDefinition>();
            foreach (var shop in shops)
            {
                if (shop == null) { Debug.LogWarning($"{name}: an empty entry in the shops list was skipped.", this); continue; }
                shopList.Add(shop.ToDefinition());
            }
            config.Shops = shopList.ToArray();
        }

        /// <summary>For editor tooling: the gear list (ticket 61).</summary>
        public void SetGear(GearDefinitionAsset[] gearAssets) => gear = gearAssets ?? new GearDefinitionAsset[0];

        /// <summary>For editor tooling: replaces the whole content list.</summary>
        public void ReplaceContent(PlayerTuningAsset tuningAsset, ProgressionAsset progressionAsset, ClassDefinitionAsset[] classAssets, SkillDefinitionAsset[] skillAssets,
            ModifierDefinitionAsset[] modifierAssets, EnemyDefinitionAsset[] enemyAssets, GateDefinitionAsset[] gateAssets, BossDefinitionAsset[] bossAssets, HubAsset hubAsset, TextSceneAsset[] sceneAssets, PartyScalingAsset scalingAsset, ShopAsset[] shopAssets)
        {
            shops = shopAssets;
            classes = classAssets;
            partyScaling = scalingAsset;
            scenes = sceneAssets;
            hub = hubAsset;
            bosses = bossAssets;
            gates = gateAssets;
            playerTuning = tuningAsset;
            progression = progressionAsset;
            skills = skillAssets;
            modifiers = modifierAssets;
            enemies = enemyAssets;
        }
    }
}
