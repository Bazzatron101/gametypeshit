using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Content validation: every data asset in the project loads into a simulation without being
    /// rejected. A bad asset fails here, in CI, before anyone meets it in a fight.
    /// </summary>
    public class ContentValidationTest
    {
        private static T[] All<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

        private static SimulationConfig ProjectConfig()
        {
            var progression = All<ProgressionAsset>().SingleOrDefault();
            return new SimulationConfig
            {
                Progression = progression != null ? progression.ToProgression() : new ProgressionDefinition(),
                StatEffects = progression != null ? progression.ToStatEffects() : new StatEffectsDefinition(),
                Skills = All<SkillDefinitionAsset>().Select(s => s.ToDefinition()).ToArray(),
            };
        }

        [Test]
        public void EverySkillAssetPassesValidation()
        {
            Assert.That(All<SkillDefinitionAsset>(), Is.Not.Empty, "the project has skills to check");
            Assert.DoesNotThrow(() => new SimulationWorld(ProjectConfig()));
        }

        [Test]
        public void EveryModifierAssetPassesValidation()
        {
            var world = new SimulationWorld(ProjectConfig());
            var player = new PlayerId(1);
            world.AddPlayer(player, Vec3.Zero);

            foreach (var asset in All<ModifierDefinitionAsset>())
                Assert.DoesNotThrow(() => world.GrantModifier(player, asset.ToDefinition()), asset.name);
        }

        [Test]
        public void EveryEnemyAssetLoads()
        {
            var world = new SimulationWorld(ProjectConfig());
            foreach (var asset in All<EnemyDefinitionAsset>())
                Assert.DoesNotThrow(() => world.SpawnEnemy(asset.ToDefinition(), Vec3.Zero), asset.name);
        }

        [Test]
        public void SkillAndModifierIdsAreUniqueAcrossAssets()
        {
            Assert.That(All<SkillDefinitionAsset>().Select(s => s.Id), Is.Unique);
            Assert.That(All<ModifierDefinitionAsset>().Select(m => m.Id), Is.Unique);
        }
    }
}
