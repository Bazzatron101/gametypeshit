using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationProgressionTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        /// <summary>Level 1 to 2 takes 50 XP, 2 to 3 takes 100, 3 to 4 takes 150. Cap at 4.</summary>
        private static SimulationConfig Config(int cap = 4, int[] curve = null) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            PlayerMaxHealth = 100,
            LightAttack = new AttackDefinition { Damage = 10, Range = 2f, ArcDegrees = 120f, CooldownSeconds = 0.4f },
            Progression = new ProgressionDefinition
            {
                LevelCap = cap,
                XpToNextLevel = curve ?? new[] { 50, 100, 150 },
                FreePointsPerLevel = 3,
                StartingStatValue = 10,
            },
            StatEffects = new StatEffectsDefinition { DamagePerStrength = 0.1f, HealthPerVitality = 10, MoveSpeedPerAgility = 0.05f },
        };

        /// <summary>Dies to one light attack, never fights back, and is worth <paramref name="xp"/>.</summary>
        private static EnemyDefinition Prey(int xp, int health = 1) => new EnemyDefinition
        {
            Name = "Prey", MaxHealth = health, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = xp,
        };

        /// <summary>Hits hard enough to show health changes, slowly enough to control.</summary>
        private static EnemyDefinition Brute(int damage) => new EnemyDefinition
        {
            Name = "Brute", MaxHealth = 1000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = damage, AttackRange = 1.6f,
            AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 100f, XpReward = 0,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerCommand cmd) =>
            new Dictionary<PlayerId, PlayerCommand> { [P1] = cmd };

        private static readonly PlayerCommand Light = new PlayerCommand(0f, 0f, lightAttack: true);

        private static SimulationWorld World(SimulationConfig config = null)
        {
            var sim = new SimulationWorld(config ?? Config());
            sim.AddPlayer(P1, Vec3.Zero);
            return sim;
        }

        /// <summary>Spawns prey in front of P1 and kills it with one light attack. Returns that tick's events.</summary>
        private static List<SimulationEvent> Kill(SimulationWorld sim, int xp)
        {
            var at = sim.GetPlayer(P1).Position + new Vec3(0f, 0f, 1f);
            sim.SpawnEnemy(Prey(xp), at);
            var events = new List<SimulationEvent>();
            for (var i = 0; i < 30; i++) // past the light attack cooldown
            {
                sim.Step(i == 0 ? Cmd(Light) : NoInput);
                events.AddRange(sim.Events);
            }
            return events;
        }

        private static List<SimulationEvent> Allocate(SimulationWorld sim, Stat stat, int times)
        {
            var events = new List<SimulationEvent>();
            for (var i = 0; i < times; i++)
            {
                sim.Step(Cmd(new PlayerCommand(0f, 0f, allocateStat: stat)));
                events.AddRange(sim.Events);
            }
            return events;
        }

        // --- XP and levels ----------------------------------------------------------------

        [Test]
        public void NewPlayerStartsAtLevelOneWithStartingStats()
        {
            var state = World().GetPlayer(P1);

            Assert.That((state.Level, state.Xp, state.XpToNextLevel, state.UnspentStatPoints), Is.EqualTo((1, 0, 50, 0)));
            foreach (Stat stat in Enum.GetValues(typeof(Stat))) Assert.That(state.Stats[stat], Is.EqualTo(10), stat.ToString());
        }

        [Test]
        public void DefeatingAnEnemyGrantsItsXp()
        {
            var sim = World();

            var events = Kill(sim, xp: 20);

            Assert.That(sim.GetPlayer(P1).Xp, Is.EqualTo(20));
            var gained = events.OfType<XpGained>().Single();
            Assert.That((gained.Player, gained.Amount), Is.EqualTo((P1, 20)));
            Assert.That(events.OfType<PlayerLeveledUp>(), Is.Empty);
        }

        [Test]
        public void ReachingTheThresholdLevelsUpAndCarriesTheRemainder()
        {
            var sim = World();
            Kill(sim, xp: 30);

            var events = Kill(sim, xp: 30); // 60 total: level 2 with 10 toward the next 100

            var state = sim.GetPlayer(P1);
            Assert.That((state.Level, state.Xp, state.XpToNextLevel), Is.EqualTo((2, 10, 100)));
            Assert.That(state.UnspentStatPoints, Is.EqualTo(3));
            var levelUp = events.OfType<PlayerLeveledUp>().Single();
            Assert.That((levelUp.Player, levelUp.NewLevel, levelUp.StatPointsGranted), Is.EqualTo((P1, 2, 3)));
        }

        [Test]
        public void OneBigGainCanLevelSeveralTimes()
        {
            var sim = World();

            var events = Kill(sim, xp: 160); // 50 + 100 = level 3, with 10 left over

            var state = sim.GetPlayer(P1);
            Assert.That((state.Level, state.Xp, state.UnspentStatPoints), Is.EqualTo((3, 10, 6)));
            Assert.That(events.OfType<PlayerLeveledUp>().Select(e => e.NewLevel), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void TheCurveComesFromConfiguration()
        {
            var sim = World(Config(curve: new[] { 10, 10, 10 }));

            Kill(sim, xp: 25);

            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(3));
        }

        [Test]
        public void LevelingStopsAtTheCapAndExtraXpIsDiscarded()
        {
            var sim = World();

            var events = Kill(sim, xp: 10_000);

            var state = sim.GetPlayer(P1);
            Assert.That((state.Level, state.Xp, state.XpToNextLevel), Is.EqualTo((4, 0, 0)));
            Assert.That(events.OfType<PlayerLeveledUp>().Select(e => e.NewLevel), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(events.OfType<XpGained>().Single().Amount, Is.EqualTo(300), "only what fit under the cap");

            events = Kill(sim, xp: 50);
            Assert.That(events.OfType<XpGained>(), Is.Empty, "nothing to gain at the cap");
            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(4));
        }

        [Test]
        public void TheCapIsAConfigurationValue()
        {
            var sim = World(Config(cap: 2));

            Kill(sim, xp: 10_000);

            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(2));
        }

        [Test]
        public void ACurveShorterThanTheCapIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(cap: 10, curve: new[] { 50, 100 })));
        }

        [Test]
        public void EveryPlayerGainsTheFullXpForAKill()
        {
            var sim = World();
            sim.AddPlayer(P2, new Vec3(20f, 0f, 0f)); // far away, did nothing

            Kill(sim, xp: 20);

            Assert.That(sim.GetPlayer(P1).Xp, Is.EqualTo(20));
            Assert.That(sim.GetPlayer(P2).Xp, Is.EqualTo(20), "no reduction for guests or carried players");
        }

        [Test]
        public void LevelingUpRestoresFullHealth()
        {
            var sim = World();
            sim.SpawnEnemy(Brute(damage: 60), new Vec3(1.2f, 0f, 0f));
            for (var i = 0; i < 10; i++) sim.Step(NoInput);
            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(40));

            Kill(sim, xp: 50);

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100));
        }

        // --- Stat points ------------------------------------------------------------------

        [Test]
        public void AllocatingSpendsAPointAndRaisesTheStat()
        {
            var sim = World();
            Kill(sim, xp: 50);

            var events = Allocate(sim, Stat.Strength, times: 2);

            var state = sim.GetPlayer(P1);
            Assert.That(state.Stats[Stat.Strength], Is.EqualTo(12));
            Assert.That(state.UnspentStatPoints, Is.EqualTo(1));
            Assert.That(events.OfType<StatPointAllocated>().Select(e => (e.Stat, e.NewValue)),
                Is.EqualTo(new[] { (Stat.Strength, 11), (Stat.Strength, 12) }));
        }

        [Test]
        public void AllocatingWithoutPointsDoesNothing()
        {
            var sim = World();

            var events = Allocate(sim, Stat.Luck, times: 1);

            Assert.That(sim.GetPlayer(P1).Stats[Stat.Luck], Is.EqualTo(10));
            Assert.That(events.OfType<StatPointAllocated>(), Is.Empty);
        }

        [Test]
        public void StrengthIncreasesAttackDamage()
        {
            var sim = World();
            Kill(sim, xp: 50);
            Allocate(sim, Stat.Strength, times: 3); // +30% at 10% per point
            var target = sim.SpawnEnemy(new EnemyDefinition { Name = "Bag", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f },
                new Vec3(0f, 0f, 1f));

            sim.Step(Cmd(Light));

            Assert.That(sim.Events.OfType<DamageDealt>().Single().Amount, Is.EqualTo(13));
            Assert.That(sim.GetEnemy(target).Health, Is.EqualTo(87));
        }

        [Test]
        public void VitalityRaisesMaxHealthAndHealsTheSameAmount()
        {
            var sim = World();
            Kill(sim, xp: 50);

            Allocate(sim, Stat.Vitality, times: 2);

            var state = sim.GetPlayer(P1);
            Assert.That(state.MaxHealth, Is.EqualTo(120));
            Assert.That(state.Health, Is.EqualTo(120));
        }

        [Test]
        public void AgilityIncreasesMoveSpeed()
        {
            var sim = World();
            Kill(sim, xp: 50);
            Allocate(sim, Stat.Agility, times: 2); // +10% at 5% per point
            var start = sim.GetPlayer(P1).Position;

            for (var i = 0; i < 60; i++) sim.Step(Cmd(new PlayerCommand(1f, 0f)));

            Assert.That(sim.GetPlayer(P1).Position.X - start.X, Is.EqualTo(5.5f).Within(1e-3f));
        }

        // --- Review fixes -----------------------------------------------------------------

        [Test]
        public void StrengthDamageRoundsHalvesUpExactly()
        {
            // 10 damage at +5% is exactly 10.5. Float maths gives 10.4999995 and rounds down.
            var config = Config();
            config.StatEffects.DamagePerStrength = 0.05f;
            var sim = World(config);
            Kill(sim, xp: 50);
            Allocate(sim, Stat.Strength, times: 1);
            sim.SpawnEnemy(new EnemyDefinition { Name = "Bag", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f },
                new Vec3(0f, 0f, 1f));

            sim.Step(Cmd(Light));

            Assert.That(sim.Events.OfType<DamageDealt>().Single().Amount, Is.EqualTo(11));
        }

        [Test]
        public void VitalitySpentWhileDefeatedStillRaisesMaxHealthButDoesNotRevive()
        {
            var sim = World();
            Kill(sim, xp: 50);
            sim.SpawnEnemy(Brute(damage: 1000), new Vec3(1.2f, 0f, 0f));
            for (var i = 0; i < 10; i++) sim.Step(NoInput);
            Assert.That(sim.GetPlayer(P1).IsDefeated, Is.True);

            Allocate(sim, Stat.Vitality, times: 1);

            var state = sim.GetPlayer(P1);
            Assert.That(state.MaxHealth, Is.EqualTo(110));
            Assert.That(state.Health, Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void CurveEntriesMustBePositive(int bad)
        {
            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(curve: new[] { 50, bad, 150 })));
        }

        [Test]
        public void LevelCapOfOneMeansMaxLevelFromTheStart()
        {
            var state = World(Config(cap: 1, curve: new int[0])).GetPlayer(P1);

            Assert.That((state.Level, state.IsAtLevelCap, state.XpToNextLevel), Is.EqualTo((1, true, 0)));
        }

        [Test]
        public void HasPlayerReportsMembership()
        {
            var sim = World();

            Assert.That(sim.HasPlayer(P1), Is.True);
            Assert.That(sim.HasPlayer(P2), Is.False);
        }
    }
}
