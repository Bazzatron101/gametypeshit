using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Party-size scaling (ticket 27): enemies scale in three tiers by how many players are present
    /// (solo, 2 to 4, 5 to 10) through health, damage and spawn counts, all from data. The tier is
    /// taken when enemies spawn, so a friend joining never changes a fight already under way
    /// (ADR 0003). Players are never scaled, and neither are rewards.
    /// </summary>
    public class PartyScalingTest
    {
        private static EnemyDefinition Grunt => new EnemyDefinition
        {
            Name = "Grunt", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 10, AttackRange = 1.5f, AttackWindupSeconds = 0.1f,
            AttackCooldownSeconds = 10f, XpReward = 40,
        };

        private static EnemyDefinition Warlord => new EnemyDefinition
        {
            Name = "Warlord", MaxHealth = 1000, MoveSpeed = 0f, XpReward = 300,
            Boss = new BossDefinition
            {
                Attacks = { new BossAttackDefinition { Kind = BossAttackKind.Slam, Damage = 20, WindupSeconds = 0.1f, CooldownSeconds = 10f, Radius = 3f } },
                EnrageAtHealthPercent = 50,
                EnrageAdds = { new RoomSpawn(Grunt, new Vec3(-2f, 0f, 0f)), new RoomSpawn(Grunt, new Vec3(2f, 0f, 0f)) },
            },
        };

        /// <summary>Two rooms of two grunts each, then a boss room.</summary>
        private static GateDefinition Gate() => new GateDefinition
        {
            Id = "scaled", Name = "Scaled",
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition { Width = 10f, Length = 10f, Spawns = { new RoomSpawn(Grunt, new Vec3(-3f, 0f, 3f)), new RoomSpawn(Grunt, new Vec3(3f, 0f, 3f)) } },
                new RoomDefinition { Width = 10f, Length = 10f, Spawns = { new RoomSpawn(Grunt, new Vec3(-3f, 0f, 3f)), new RoomSpawn(Grunt, new Vec3(3f, 0f, 3f)) } },
                new RoomDefinition { Width = 16f, Length = 16f, Spawns = { new RoomSpawn(Warlord, new Vec3(0f, 0f, 4f)) } },
            },
        };

        private static PartyScalingDefinition Scaling() => new PartyScalingDefinition
        {
            Small = new PartyTier { HealthPercent = 160, DamagePercent = 115, SpawnPercent = 150 },
            Large = new PartyTier { HealthPercent = 250, DamagePercent = 130, SpawnPercent = 200 },
        };

        private static SimulationWorld World(int players)
        {
            var world = new SimulationWorld(new SimulationConfig
            {
                TickRate = 60, Gates = new[] { Gate() }, PartyScaling = Scaling(),
                Progression = new ProgressionDefinition { LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 } },
            });
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), Vec3.Zero);
            world.StartGate("scaled");
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        /// <summary>The first player walks into room <paramref name="room"/>; returns the enemies that appeared.</summary>
        private static List<EnemyState> Enter(SimulationWorld world, int room)
        {
            var before = world.EnemyIds.ToList();
            world.PlacePlayer(new PlayerId(1), world.Gate.Layout.Rooms[room].Centre + new Vec3(0f, 0f, -4f));
            Step(world);
            return world.EnemyIds.Except(before).Select(world.GetEnemy).ToList();
        }

        [TestCase(1, 2, 100)]
        [TestCase(2, 3, 160)]
        [TestCase(4, 3, 160)]
        [TestCase(5, 4, 250)]
        [TestCase(10, 4, 250)]
        public void EachTierSetsHowManyEnemiesAppearAndHowToughTheyAre(int players, int expectedCount, int expectedHealth)
        {
            var world = World(players);

            var enemies = Enter(world, 1);

            Assert.That(enemies.Count, Is.EqualTo(expectedCount), "a room of two grunts");
            Assert.That(enemies.Select(e => e.MaxHealth), Has.All.EqualTo(expectedHealth));
            Assert.That(enemies.Select(e => e.Health), Has.All.EqualTo(expectedHealth), "they start at full health");
            Assert.That(enemies.Select(e => e.Position).Distinct().Count(), Is.EqualTo(expectedCount), "extra enemies stand apart");
            Assert.That(world.Gate.PartyTier, Is.EqualTo(players == 1 ? PartyTierKind.Solo : players <= 4 ? PartyTierKind.Small : PartyTierKind.Large));
        }

        [TestCase(1, 10)]
        [TestCase(3, 12)]   // 10 x 115% = 11.5, rounded half up
        [TestCase(6, 13)]
        public void EnemiesHitHarderInABiggerParty(int players, int expectedDamage)
        {
            var world = World(players);
            Enter(world, 1);
            var grunt = world.EnemyIds.First();
            var p1 = new PlayerId(1);
            world.PlacePlayer(p1, world.GetEnemy(grunt).Position + new Vec3(0f, 0f, -1f));

            var hit = Step(world, 30).OfType<PlayerDamaged>().First(d => d.Target == p1);

            Assert.That(hit.Amount, Is.EqualTo(expectedDamage));
        }

        [Test]
        public void TheBossIsToughenedAndCallsMoreHelpInABiggerParty()
        {
            var world = World(players: 3);

            var boss = Enter(world, 3).Single(e => e.IsBoss);
            Assert.That(boss.MaxHealth, Is.EqualTo(1600));
            world.DamageEnemy(boss.Id, new PlayerId(1), 900); // past half of 1600
            Step(world, 3);

            var grunts = world.EnemyIds.Select(world.GetEnemy).Where(e => e.Definition.Name == "Grunt" && !e.IsDefeated).ToList();
            Assert.That(grunts.Count, Is.EqualTo(3), "two adds at 150%");
            Assert.That(grunts.Select(g => g.MaxHealth), Has.All.EqualTo(160));
        }

        [Test]
        public void OnlyPlayersPresentCount()
        {
            var world = World(players: 2);
            world.SetAway(new PlayerId(2), true);

            Assert.That(Enter(world, 1).Count, Is.EqualTo(2), "alone in practice: solo");
        }

        [Test]
        public void AFriendJoiningMidFightChangesOnlyWhatSpawnsAfterwards()
        {
            var world = World(players: 1);
            var first = Enter(world, 1);
            world.AddPlayer(new PlayerId(2), world.Gate.JoinPosition(new PlayerId(2)));
            Step(world);

            Assert.That(first.Select(e => world.GetEnemy(e.Id).MaxHealth), Has.All.EqualTo(100), "the fight under way keeps its enemies");
            var next = Enter(world, 2);
            Assert.That(next.Count, Is.EqualTo(3));
            Assert.That(next.Select(e => e.MaxHealth), Has.All.EqualTo(160), "the next room is for a party of two");
        }

        [Test]
        public void PlayersAndRewardsAreNeverScaled()
        {
            var solo = World(players: 1);
            var party = World(players: 6);
            foreach (var world in new[] { solo, party })
            {
                var grunt = Enter(world, 1).First();
                world.DamageEnemy(grunt.Id, new PlayerId(1), 100000);
            }

            var soloXp = Step(solo).OfType<XpGained>().Where(x => x.Player == new PlayerId(1)).Sum(x => x.Amount);
            var partyXp = Step(party).OfType<XpGained>().Where(x => x.Player == new PlayerId(1)).Sum(x => x.Amount);

            Assert.That(partyXp, Is.EqualTo(soloXp).And.EqualTo(40), "the same XP for the same kill");
            Assert.That(party.GetPlayer(new PlayerId(1)).MaxHealth, Is.EqualTo(solo.GetPlayer(new PlayerId(1)).MaxHealth), "players are not scaled");
            Assert.That(party.GetPlayer(new PlayerId(1)).Stats[Stat.Strength], Is.EqualTo(solo.GetPlayer(new PlayerId(1)).Stats[Stat.Strength]));
        }

        private static EnemyDefinition Brute => new EnemyDefinition { Name = "Brute", MaxHealth = 300, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f };

        private static GateDefinition MixedGate() => new GateDefinition
        {
            Id = "mixed", Name = "Mixed",
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition
                {
                    Width = 6f, Length = 10f, // narrow, so copies of the enemies near its side walls must be kept in
                    Spawns =
                    {
                        new RoomSpawn(Grunt, new Vec3(-2.8f, 0f, 2.8f)), new RoomSpawn(Grunt, new Vec3(2.8f, 0f, 2.8f)), new RoomSpawn(Brute, new Vec3(0f, 0f, 2.8f)),
                        new RoomSpawn(Grunt, new Vec3(-2.8f, 0f, -2.8f)),
                    },
                },
                new RoomDefinition { Width = 10f, Length = 10f },
            },
        };

        private static SimulationWorld MixedWorld(int players)
        {
            var world = new SimulationWorld(new SimulationConfig { Gates = new[] { MixedGate() }, PartyScaling = Scaling() });
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), Vec3.Zero);
            world.StartGate("mixed");
            return world;
        }

        [Test]
        public void SoloIsExactlyTheRoomAsAuthoredInItsOrder()
        {
            var enemies = Enter(MixedWorld(1), 1);

            Assert.That(enemies.Select(e => e.Definition.Name), Is.EqualTo(new[] { "Grunt", "Grunt", "Brute", "Grunt" }));
        }

        [Test]
        public void ExtraEnemiesStayInTheirRoomAndCopyTheWholeMix()
        {
            var world = MixedWorld(6); // 200%: four extra
            var enemies = Enter(world, 1);
            var room = world.Gate.Layout.Rooms[1];

            Assert.That(enemies.Count, Is.EqualTo(8));
            Assert.That(enemies.Take(4).Select(e => e.Definition.Name), Is.EqualTo(new[] { "Grunt", "Grunt", "Brute", "Grunt" }), "the authored ones first");
            Assert.That(enemies.Count(e => e.Definition.Name == "Brute"), Is.EqualTo(2), "the Brute is copied too, not only the first ones");
            foreach (var e in enemies) Assert.That(room.Contains(e.Position), Is.True, $"{e.Definition.Name} at {e.Position} is in its own room");
        }

        [Test]
        public void TheBossesXpAndTheClearRewardAreTheSameInAnyParty()
        {
            foreach (var players in new[] { 1, 6 })
            {
                var world = World(players);
                world.Gate.Definition.RewardPool.Clear();
                world.Gate.Definition.RewardPool.Add(new ModifierDefinition { Id = "edge", Name = "Edge", AppliesToTag = "melee", DamagePercent = 10 });
                var boss = Enter(world, 3).Single(e => e.IsBoss);
                world.DamageEnemy(boss.Id, new PlayerId(1), 100000);

                var events = Step(world);

                Assert.That(events.OfType<XpGained>().Where(x => x.Player == new PlayerId(1)).Sum(x => x.Amount), Is.EqualTo(300), $"{players} players: the boss's XP");
                Assert.That(events.OfType<GateRewardGranted>().Count(), Is.EqualTo(players), "one reward for each player present");
            }
        }

        [Test]
        public void WithoutScalingDataEveryTierIsTheAuthoredGate()
        {
            var world = new SimulationWorld(new SimulationConfig { Gates = new[] { Gate() } });
            for (var i = 1; i <= 6; i++) world.AddPlayer(new PlayerId(i), Vec3.Zero);
            world.StartGate("scaled");

            var enemies = Enter(world, 1);

            Assert.That(enemies.Count, Is.EqualTo(2));
            Assert.That(enemies.Select(e => e.MaxHealth), Has.All.EqualTo(100));
        }

        [Test]
        public void ScaledEnemiesSurviveASnapshot()
        {
            var world = World(players: 3);
            Enter(world, 1);

            var copy = SimulationWorld.FromSnapshot(new SimulationConfig
            {
                TickRate = 60, Gates = new[] { Gate() }, PartyScaling = Scaling(),
                Progression = new ProgressionDefinition { LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 } },
            }, world.CaptureSnapshot());

            Assert.That(copy.EnemyIds.Select(copy.GetEnemy).Select(e => e.MaxHealth), Has.All.EqualTo(160));
            Step(world, 60);
            Step(copy, 60);
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }
    }
}
