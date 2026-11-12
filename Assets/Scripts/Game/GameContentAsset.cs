using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// The one list of everything in the game: player tuning, progression, skills, modifiers and enemies. Every
    /// scene (the greybox, the test arena, later gates) points at this asset instead of keeping its
    /// own lists, so content is defined once. The content validation test checks that no asset of
    /// these types sits outside it.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Game Content", fileName = "GameContent")]
    public sealed class GameContentAsset : ScriptableObject
    {
        [SerializeField] private PlayerTuningAsset playerTuning;
        [SerializeField] private ProgressionAsset progression;
        [SerializeField, Tooltip("Starters (unlock level 1) fill the bar in this order")] private SkillDefinitionAsset[] skills = new SkillDefinitionAsset[0];
        [SerializeField] private ModifierDefinitionAsset[] modifiers = new ModifierDefinitionAsset[0];
        [SerializeField] private EnemyDefinitionAsset[] enemies = new EnemyDefinitionAsset[0];

        public PlayerTuningAsset PlayerTuning => playerTuning;
        public ProgressionAsset Progression => progression;
        public IReadOnlyList<SkillDefinitionAsset> Skills => skills;
        public IReadOnlyList<ModifierDefinitionAsset> Modifiers => modifiers;
        public IReadOnlyList<EnemyDefinitionAsset> Enemies => enemies;

        /// <summary>
        /// Fills the content parts of a simulation config. The runner and the content tests both go
        /// through here, so they see exactly the same data. Empty list entries are skipped with a warning.
        /// </summary>
        public void ApplyTo(SimulationConfig config)
        {
            if (playerTuning != null) playerTuning.ApplyTo(config);
            config.Progression = progression != null ? progression.ToProgression() : new ProgressionDefinition();
            config.StatEffects = progression != null ? progression.ToStatEffects() : new StatEffectsDefinition();

            var list = new List<SkillDefinition>();
            foreach (var asset in skills)
            {
                if (asset == null) { Debug.LogWarning($"{name}: an empty entry in the skills list was skipped.", this); continue; }
                list.Add(asset.ToDefinition());
            }
            config.Skills = list.ToArray();
        }

        /// <summary>For editor tooling: replaces the whole content list.</summary>
        public void ReplaceContent(PlayerTuningAsset tuningAsset, ProgressionAsset progressionAsset, SkillDefinitionAsset[] skillAssets,
            ModifierDefinitionAsset[] modifierAssets, EnemyDefinitionAsset[] enemyAssets)
        {
            playerTuning = tuningAsset;
            progression = progressionAsset;
            skills = skillAssets;
            modifiers = modifierAssets;
            enemies = enemyAssets;
        }
    }
}
