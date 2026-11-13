using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            Assert.That(All<GateDefinitionAsset>(), Is.SubsetOf(content.Gates));
            Assert.That(All<BossDefinitionAsset>(), Is.SubsetOf(content.Bosses));
            Assert.That(content.Bosses, Is.Unique);
            Assert.That(content.Gates, Is.Unique);
            Assert.That(content.Skills, Is.Unique);
            Assert.That(content.Modifiers, Is.Unique);
            Assert.That(content.Enemies, Is.Unique);
        }

        [Test]
        public void EveryGateIsACompleteChainOfRoomPieces()
        {
            var content = Content();
            Assert.That(content.Gates, Is.Not.Empty.And.All.Not.Null);
            Assert.That(content.Gates.Select(g => g.Id), Is.Unique, "gate ids name gates in saves and over the network");
            var config = new SimulationConfig();
            content.ApplyTo(config);

            foreach (var asset in content.Gates)
            {
                Assert.That(asset.Id, Is.Not.Empty, asset.name);
                Assert.That(asset.Rooms, Is.Not.Empty.And.All.Not.Null, $"{asset.name}: rooms");
                Assert.That(asset.Rooms[0].Spawns, Is.Empty, $"{asset.name}: the entrance room is safe to arrive in");
                foreach (var room in asset.Rooms)
                {
                    foreach (var spawn in room.Spawns)
                    {
                        Assert.That(spawn.enemy, Is.Not.Null, $"{room.name}: a spawn with no enemy");
                        Assert.That(content.Enemies, Has.Member(spawn.enemy), $"{room.name}: an enemy that is not in the content list");
                        Assert.That(Mathf.Abs(spawn.offset.x), Is.LessThanOrEqualTo(room.Width / 2f), $"{room.name}: a spawn outside the room");
                        Assert.That(Mathf.Abs(spawn.offset.z), Is.LessThanOrEqualTo(room.Length / 2f), $"{room.name}: a spawn outside the room");
                    }
                }

                foreach (var modifier in asset.RewardPool)
                    Assert.That(content.Modifiers, Has.Member(modifier), $"{asset.name}: a reward that is not in the content list");
                foreach (var room in asset.Rooms.Where(r => r.Boss != null))
                {
                    Assert.That(content.Bosses, Has.Member(room.Boss), $"{room.name}: a boss that is not in the content list");
                    Assert.That(Mathf.Abs(room.BossOffset.x) <= room.Width / 2f && Mathf.Abs(room.BossOffset.z) <= room.Length / 2f, Is.True,
                        $"{room.name}: the boss waits outside the room");
                }

                var gate = config.Gates.Single(g => g.Id == asset.Id);
                var layout = new GateLayout(gate); // throws on a room with no floor
                Assert.That(layout.Checkpoints, Is.Not.Empty, $"{asset.name}: at least one checkpoint");
                Assert.That(gate.SoloRevives, Is.EqualTo(3), $"{asset.name}: the spec's three solo revives");
                Assert.That(gate.Rooms.SelectMany(r => r.Spawns).Select(sp => sp.Enemy).Distinct().Count(),
                    Is.EqualTo(asset.Rooms.SelectMany(r => r.Spawns).Select(sp => sp.enemy).Distinct().Count() +
                               asset.Rooms.Where(r => r.Boss != null).Select(r => r.Boss).Distinct().Count()),
                    $"{asset.name}: each enemy type is one shared definition, so snapshots stay small");
            }
        }

        [Test]
        public void EveryBossHasReadableAttacksAndUsableAdds()
        {
            var content = Content();
            Assert.That(content.Bosses, Is.Not.Empty.And.All.Not.Null);
            foreach (var boss in content.Bosses)
            {
                Assert.That(boss.Attacks, Is.Not.Empty, $"{boss.name}: attacks");
                Assert.That(boss.Attacks.Select(a => a.kind), Is.Unique, $"{boss.name}: at most one attack of each kind");
                Assert.That(boss.Attacks.Select(a => a.windupSeconds), Has.All.GreaterThanOrEqualTo(0.5f),
                    $"{boss.name}: every telegraph long enough to read (half a second)");
                Assert.That(boss.Attacks.Where(a => a.kind == BossAttackKind.Charge).Select(a => a.speed), Has.All.GreaterThan(0f),
                    $"{boss.name}: a charge needs speed");
                foreach (var add in boss.EnrageAdds)
                    Assert.That(content.Enemies, Has.Member(add.enemy), $"{boss.name}: an add that is not in the content list");
            }
        }

        [Test]
        public void TheHubSitsClearOfEveryGate()
        {
            var content = Content();
            Assert.That(content.Hub, Is.Not.Null, "the content has a hub");
            Assert.That(All<HubAsset>().Length, Is.EqualTo(1), "one hub");
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var hub = config.Hub.Floor;
            foreach (var gate in config.Gates)
            foreach (var room in new GateLayout(gate).Rooms)
            {
                var overlaps = room.MinX < hub.MaxX && room.MaxX > hub.MinX && room.MinZ < hub.MaxZ && room.MaxZ > hub.MinZ;
                Assert.That(overlaps, Is.False, $"{gate.Id}: a room overlaps the hub");
            }
        }

        [Test]
        public void EveryGateWithABossHasARewardPool()
        {
            foreach (var gate in Content().Gates.Where(g => g.Rooms.Any(r => r != null && r.Boss != null)))
                Assert.That(gate.RewardPool, Is.Not.Empty.And.All.Not.Null, $"{gate.name}: a boss gate gives a guaranteed reward");
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
