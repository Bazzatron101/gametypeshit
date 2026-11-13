using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// A bot plays like a simple player: it walks to the nearest enemy, attacks it, uses its skills,
    /// dodges hits it sees coming and spends its stat points. Bots let one developer fill a
    /// ten-player session (ticket 14).
    /// </summary>
    public class BotBrainTest
    {
        private static readonly PlayerId Me = new PlayerId(1);

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
        };

        private static EnemyDefinition Dummy(int health = 100000) => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static EnemyDefinition Biter => new EnemyDefinition
        {
            Name = "Biter", MaxHealth = 100000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 5, AttackRange = 3f,
            AttackWindupSeconds = 0.5f, AttackCooldownSeconds = 1f, XpReward = 0,
        };

        /// <summary>Runs the world with the bot driving player 1, feeding it each tick's events.</summary>
        private static List<SimulationEvent> Run(SimulationWorld world, BotBrain bot, int ticks)
        {
            var all = new List<SimulationEvent>();
            var commands = new Dictionary<PlayerId, PlayerCommand>();
            for (var i = 0; i < ticks; i++)
            {
                commands[Me] = bot.Next(world, Me, world.Events);
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        [Test]
        public void WalksTowardsTheNearestEnemy()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 10f));
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, -20f));

            var command = new BotBrain(seed: 1).Next(world, Me, world.Events);

            Assert.That(command.MoveZ, Is.GreaterThan(0.9f), "towards the enemy 10 m away, not the one 20 m away");
        }

        [Test]
        public void ReachesAnEnemyAndDamagesIt()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(), new Vec3(4f, 0f, 6f));

            var events = Run(world, new BotBrain(seed: 1), 300);

            Assert.That(events.OfType<DamageDealt>().Count(d => d.Attacker == Me), Is.GreaterThan(5));
        }

        [Test]
        public void MovesOnToTheNextEnemyOnceOneIsDefeated()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(health: 10), new Vec3(0f, 0f, 2f));
            world.SpawnEnemy(Dummy(health: 10), new Vec3(6f, 0f, 2f));

            var events = Run(world, new BotBrain(seed: 1), 600);

            Assert.That(events.OfType<EnemyDefeated>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void DodgesHitsItSeesComing()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Biter, new Vec3(0f, 0f, 1.5f));

            var events = Run(world, new BotBrain(seed: 1), 600);

            var dodged = events.OfType<AttackDodged>().Count(a => a.Player == Me);
            var taken = events.OfType<PlayerDamaged>().Count(d => d.Target == Me);
            Assert.That(dodged, Is.GreaterThan(3), "telegraphed hits are dodged");
            Assert.That(dodged, Is.GreaterThan(taken), "most of them");
        }

        [Test]
        public void SpendsItsStatPoints()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.GrantXp(Me, 60); // level 2: three points to spend

            Run(world, new BotBrain(seed: 1), 30);

            Assert.That(world.GetPlayer(Me).Level, Is.EqualTo(2));
            Assert.That(world.GetPlayer(Me).UnspentStatPoints, Is.Zero);
        }

        [Test]
        public void KeepsFightingWhileItSpendsItsPoints()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 1f));
            world.GrantXp(Me, 60);

            var events = Run(world, new BotBrain(seed: 1), 60);

            Assert.That(events.OfType<StatPointAllocated>().Count(), Is.EqualTo(3));
            Assert.That(events.OfType<DamageDealt>().Count(d => d.Attacker == Me), Is.GreaterThan(1), "points are spent alongside attacks");
        }

        [Test]
        public void AskedEveryFrameItStillSpendsOnePointPerTick()
        {
            // In the game the bot is sampled once a frame, often several frames per tick.
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.GrantXp(Me, 60);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            var bot = new BotBrain(seed: 1);

            var allocations = Enumerable.Range(0, 4).Count(_ => bot.Next(world, Me, world.Events).AllocateStat.HasValue);

            Assert.That(allocations, Is.EqualTo(1), "four frames before the next tick: one point asked for, not four");
        }

        [Test]
        public void AskedTwiceBeforeATickItGivesTheSameDodgeBothTimes()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 1f));
            var bot = new BotBrain(seed: 1);
            var hit = new SimulationEvent[] { new EnemyAttackStarted(world.Tick, world.EnemyIds[0], Me, world.Tick + 3) };

            var first = bot.Next(world, Me, hit);
            var second = bot.Next(world, Me, System.Array.Empty<SimulationEvent>()); // the next frame, same tick

            Assert.That(first.Dodge, Is.True);
            Assert.That(second, Is.EqualTo(first), "the input buffer keeps the latest movement, so a second frame must not turn the dodge round");
        }

        [Test]
        public void AnyIntIsASafeSeed()
        {
            Assert.DoesNotThrow(() => new BotBrain(int.MinValue));
            Assert.DoesNotThrow(() => new BotBrain(int.MaxValue));
        }

        [Test]
        public void UsesItsSkills()
        {
            var config = Config();
            config.Skills = new[]
            {
                new SkillDefinition { Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3f, CooldownSeconds = 2f, Tags = new[] { "area" } },
            };
            var world = new SimulationWorld(config);
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            var events = Run(world, new BotBrain(seed: 1), 600);

            Assert.That(events.OfType<SkillUsed>().Count(s => s.Player == Me), Is.GreaterThan(1));
        }

        [Test]
        public void ADefeatedBotOnlyAsksToGetUp()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(Me, Vec3.Zero);
            world.SpawnEnemy(Biter, new Vec3(0f, 0f, 1.5f));
            var bot = new BotBrain(seed: 1);

            // Without dodging, the biter wears the bot down; a defeated bot then sends nothing but a request to get up.
            var commands = new Dictionary<PlayerId, PlayerCommand>();
            for (var i = 0; i < 6000 && !world.GetPlayer(Me).IsDefeated; i++)
            {
                commands[Me] = PlayerCommand.None;
                world.Step(commands);
            }
            Assume.That(world.GetPlayer(Me).IsDefeated, Is.True);
            var asked = false;

            for (var i = 0; i < 60; i++)
            {
                var command = bot.Next(world, Me, world.Events);
                asked |= command.Respawn;
                Assert.That(command, Is.EqualTo(PlayerCommand.None).Or.EqualTo(new PlayerCommand(0f, 0f, respawn: true)), $"tick {i}");
                commands[Me] = PlayerCommand.None;
                world.Step(commands);
            }
            Assert.That(asked, Is.True, "within a second it asks");
        }
    }
}
