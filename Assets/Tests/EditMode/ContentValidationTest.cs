using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Content validation. The game's single content list (GameContent) loads into a simulation
    /// through the same path the runner uses, every entry passes the simulation's checks, and no
    /// skill, modifier or enemy asset exists outside the list. A bad asset fails here, before
    /// anyone meets it in a fight.
    /// </summary>
    public class ContentValidationTest
    {
        private static T[] All<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

        private static GameContentAsset Content()
        {
            var all = All<GameContentAsset>();
            Assert.That(all.Length, Is.EqualTo(1), "exactly one GameContent asset");
            return all[0];
        }

        private static SimulationWorld WorldFromContent(GameContentAsset content)
        {
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return new SimulationWorld(config);
        }

        [Test]
        public void ContentHasNoEmptyEntriesAndOneOfEachSettingsAsset()
        {
            var content = Content();

            Assert.That(All<ProgressionAsset>(), Is.EqualTo(new[] { content.Progression }), "one progression asset, the listed one");
            Assert.That(All<PlayerTuningAsset>(), Is.EqualTo(new[] { content.PlayerTuning }), "one tuning asset, the listed one");
            Assert.That(content.Skills, Is.Not.Empty.And.All.Not.Null);
            Assert.That(content.Modifiers, Is.All.Not.Null);
            Assert.That(content.Enemies, Is.Not.Empty.And.All.Not.Null);
        }

        [Test]
        public void EveryAssetIsInTheContentListSoNothingIsDefinedTwiceOrForgotten()
        {
            var content = Content();

            Assert.That(All<SkillDefinitionAsset>(), Is.SubsetOf(content.Skills));
            Assert.That(All<ModifierDefinitionAsset>(), Is.SubsetOf(content.Modifiers));
            Assert.That(All<EnemyDefinitionAsset>(), Is.SubsetOf(content.Enemies));
            Assert.That(content.Skills, Is.Unique);
            Assert.That(content.Modifiers, Is.Unique);
            Assert.That(content.Enemies, Is.Unique);
        }

        [Test]
        public void EverySkillPassesValidation()
        {
            Assert.DoesNotThrow(() => WorldFromContent(Content()));
        }

        [Test]
        public void EveryModifierPassesValidation()
        {
            var content = Content();
            var world = WorldFromContent(content);
            var player = new PlayerId(1);
            world.AddPlayer(player, Vec3.Zero);

            foreach (var asset in content.Modifiers)
                Assert.DoesNotThrow(() => world.GrantModifier(player, asset.ToDefinition()), asset.name);
        }

        [Test]
        public void EveryEnemyLoads()
        {
            var content = Content();
            var world = WorldFromContent(content);

            foreach (var asset in content.Enemies)
                Assert.DoesNotThrow(() => world.SpawnEnemy(asset.ToDefinition(), Vec3.Zero), asset.name);
        }

        [Test]
        public void SkillAndModifierIdsAreUnique()
        {
            var content = Content();

            Assert.That(content.Skills.Select(s => s.Id), Is.Unique);
            Assert.That(content.Modifiers.Select(m => m.Id), Is.Unique);
        }
    }
}
