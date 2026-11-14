using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Jumping and sprinting. Sprint is held: faster on foot, and it ends the moment you fight.
    /// A jump is a hop that carries you over ground-level attacks (a boss's slam) but not over an
    /// enemy: clearing a charge means jumping higher than the enemy is tall, which a base jump
    /// cannot. Classes will raise a player's jump height. Sweeps and ordinary swings still hit
    /// you in the air, and you can't attack mid-air.
    /// </summary>
    public class JumpAndSprintTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 5f, PlayerMaxHealth = 1000,
            SprintSpeedPercent = 160, JumpHeight = 0.9f, JumpSeconds = 0.6f,
        };

        private static List<SimulationEvent> Step(SimulationWorld world, PlayerCommand command, int ticks = 1)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
                all.AddRange(world.Events);
            }
            return all;
        }

        private static SimulationWorld World()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, Vec3.Zero);
            return world;
        }

        private static readonly PlayerCommand Jump = new PlayerCommand(0f, 0f, jump: true);

        // --- Sprint --------------------------------------------------------------------------------

        [Test]
        public void HoldingSprintIsFaster()
        {
            var walk = World();
            var run = World();

            Step(walk, new PlayerCommand(1f, 0f), 60);
            Step(run, new PlayerCommand(1f, 0f, sprint: true), 60);

            Assert.That(walk.GetPlayer(P).Position.X, Is.EqualTo(5f).Within(0.01f));
            Assert.That(run.GetPlayer(P).Position.X, Is.EqualTo(8f).Within(0.01f), "160% of walking speed");
            Assert.That(run.GetPlayer(P).IsSprinting, Is.True);
        }

        [Test]
        public void SprintingEndsWhenYouFight()
        {
            var world = World();
            Step(world, new PlayerCommand(1f, 0f, sprint: true), 10);

            Step(world, new PlayerCommand(1f, 0f, sprint: true, lightAttack: true));

            Assert.That(world.GetPlayer(P).IsSprinting, Is.False, "the swing takes the tick");
        }

        [Test]
        public void AfterASwingTheRunPausesBrieflyThenResumes()
        {
            var world = World();
            Step(world, new PlayerCommand(1f, 0f, sprint: true, lightAttack: true));
            var x = world.GetPlayer(P).Position.X;

            Step(world, new PlayerCommand(1f, 0f, sprint: true), 10);
            Assert.That(world.GetPlayer(P).Position.X - x, Is.EqualTo(10 / 60f * 5f).Within(0.01f), "walking just after the swing");

            Step(world, new PlayerCommand(1f, 0f, sprint: true), 30);
            Assert.That(world.GetPlayer(P).IsSprinting, Is.True, "running again");
        }

        [Test]
        public void MashingAttackOnCooldownDoesNotBreakTheRun()
        {
            var world = World();
            Step(world, new PlayerCommand(1f, 0f, sprint: true), 60); // no swing yet: attack is ready
            var runner = World();
            // Swing once, wait out the pause, then keep pressing attack while it is still cooling down.
            Step(runner, new PlayerCommand(1f, 0f, sprint: true, lightAttack: true));
            Step(runner, new PlayerCommand(1f, 0f, sprint: true), 24);
            var x = runner.GetPlayer(P).Position.X;
            Step(runner, new PlayerCommand(1f, 0f, sprint: true, lightAttack: true)); // on cooldown: nothing happens

            Assert.That(runner.GetPlayer(P).Position.X - x, Is.EqualTo(8f / 60f).Within(0.001f), "still at sprint speed");
        }

        [Test]
        public void PlacingAPlayerMidJumpPutsThemOnTheGround()
        {
            var world = World();
            Step(world, Jump);
            Step(world, PlayerCommand.None, 5);

            world.PlacePlayer(P, new Vec3(3f, 0f, 3f));
            Step(world, PlayerCommand.None);

            Assert.That(world.GetPlayer(P).IsAirborne, Is.False);
            Assert.That(world.GetPlayer(P).Position.Y, Is.Zero);
        }

        [Test]
        public void SprintingAndChargingDoNotMix()
        {
            var config = Config();
            config.Skills = new[] { new SkillDefinition { Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area, Damage = 1, Radius = 2f, ChargeSeconds = 1f, ChargedSizePercent = 50 } };
            var world = new SimulationWorld(config);
            world.AddPlayer(P, Vec3.Zero);
            Step(world, new PlayerCommand(0f, 0f, useSkillSlot: 0, heldSkillSlots: 1));
            var before = world.GetPlayer(P).Position.X;

            Step(world, new PlayerCommand(1f, 0f, sprint: true, heldSkillSlots: 1), 60);

            Assert.That(world.GetPlayer(P).Position.X - before, Is.EqualTo(5f * config.ChargeMoveSpeedPercent / 100f).Within(0.01f), "the charge's slow walk, not a sprint");
        }

        // --- Jump ----------------------------------------------------------------------------------

        [Test]
        public void AJumpRisesToItsHeightAndLandsOnTime()
        {
            var world = World();
            var peak = 0f;

            var events = Step(world, Jump);
            var ticks = 1;
            while (world.GetPlayer(P).IsAirborne && ticks < 120)
            {
                peak = System.Math.Max(peak, world.GetPlayer(P).Position.Y);
                Step(world, PlayerCommand.None);
                ticks++;
            }

            Assert.That(events.OfType<PlayerJumped>().Count(), Is.EqualTo(1));
            Assert.That(peak, Is.EqualTo(0.9f).Within(0.02f));
            Assert.That(ticks, Is.EqualTo(36).Within(1), "0.6 s");
            Assert.That(world.GetPlayer(P).Position.Y, Is.Zero, "back on the ground");
        }

        [Test]
        public void YouCanSteerInTheAirButNotJumpAgainOrAttack()
        {
            var world = World();
            Step(world, Jump);
            Step(world, PlayerCommand.None, 5);

            var events = Step(world, new PlayerCommand(1f, 0f, jump: true, lightAttack: true));

            Assert.That(events.OfType<PlayerJumped>(), Is.Empty, "no double jump");
            Assert.That(events.OfType<AttackPerformed>(), Is.Empty, "no attacks in the air");
            Assert.That(world.GetPlayer(P).Position.X, Is.GreaterThan(0f), "but you can steer");
        }

        [Test]
        public void SprintCarriesIntoAJump()
        {
            var world = World();
            Step(world, new PlayerCommand(1f, 0f, sprint: true, jump: true));
            Step(world, new PlayerCommand(1f, 0f, sprint: true), 35);

            Assert.That(world.GetPlayer(P).Position.X, Is.EqualTo(36 / 60f * 8f).Within(0.05f));
        }

        [Test]
        public void YouCannotJumpWhileDodgingOrDown()
        {
            var world = World();
            Step(world, new PlayerCommand(1f, 0f, dodge: true));

            Assert.That(Step(world, Jump).OfType<PlayerJumped>(), Is.Empty, "mid-dodge");
        }

        // --- What a jump clears ------------------------------------------------------------------------

        private static EnemyDefinition Warlord(float height = 3.5f) => new EnemyDefinition
        {
            Name = "Warlord", MaxHealth = 10000, MoveSpeed = 0f, Height = height,
            Boss = new BossDefinition
            {
                TurnDegreesPerSecond = 720f,
                Attacks =
                {
                    new BossAttackDefinition { Kind = BossAttackKind.Slam, Damage = 30, WindupSeconds = 0.5f, CooldownSeconds = 5f, Radius = 3f },
                    new BossAttackDefinition { Kind = BossAttackKind.Charge, Damage = 30, WindupSeconds = 0.5f, CooldownSeconds = 5f, Range = 10f, Radius = 1f, Speed = 10f },
                },
                EnrageAtHealthPercent = 0,
            },
        };

        private static EnemyDefinition SlamOnly()
        {
            var boss = Warlord();
            boss.Boss.Attacks.RemoveAll(a => a.Kind != BossAttackKind.Slam);
            return boss;
        }

        /// <summary>Waits until the boss's attack is about to land, then jumps so the player is in the air when it does.</summary>
        private static List<SimulationEvent> JumpInTime(SimulationWorld world, int leadTicks)
        {
            var events = new List<SimulationEvent>();
            BossAttackStarted started = null;
            for (var i = 0; i < 300 && started == null; i++)
            {
                var step = Step(world, PlayerCommand.None);
                events.AddRange(step);
                started = step.OfType<BossAttackStarted>().FirstOrDefault();
            }
            Assert.That(started, Is.Not.Null, "test setup: the boss attacks");
            events.AddRange(Step(world, PlayerCommand.None, started.LandsAtTick - world.Tick - leadTicks));
            events.AddRange(Step(world, Jump));
            events.AddRange(Step(world, PlayerCommand.None, 60));
            return events;
        }

        [Test]
        public void AJumpClearsASlam()
        {
            var world = World();
            world.SpawnEnemy(SlamOnly(), new Vec3(0f, 0f, 2f)); // close: it slams

            var events = JumpInTime(world, leadTicks: 10);

            Assert.That(events.OfType<BossAttackStarted>().First().Kind, Is.EqualTo(BossAttackKind.Slam));
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
            Assert.That(events.OfType<AttackJumped>().Count(), Is.EqualTo(1), "and the game can say so");
        }

        [Test]
        public void ABaseJumpDoesNotClearACharge()
        {
            var world = World();
            world.SpawnEnemy(Warlord(), new Vec3(0f, 0f, 8f)); // far: it charges

            var events = JumpInTime(world, leadTicks: -30); // near the top of the jump as it arrives (7 m at 10 m/s: 42 ticks)

            Assert.That(events.OfType<BossAttackStarted>().First().Kind, Is.EqualTo(BossAttackKind.Charge));
            Assert.That(events.OfType<PlayerDamaged>().Count(), Is.EqualTo(1), "you can't jump over something 3.5 m tall");
        }

        [Test]
        public void AHighEnoughJumpClearsAChargeOverTheEnemy()
        {
            // What a class will do: raise the player's jump height (here to 5x, 4.5 m) above the enemy's.
            var world = World();
            world.SetJumpHeightPercent(P, 500);
            world.SpawnEnemy(Warlord(), new Vec3(0f, 0f, 8f));

            var events = JumpInTime(world, leadTicks: -30);

            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
            Assert.That(events.OfType<AttackJumped>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void SweepsAndOrdinarySwingsStillHitYouInTheAir()
        {
            var world = World();
            world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Goblin", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 7, AttackRange = 1.5f, AttackWindupSeconds = 0.3f, AttackCooldownSeconds = 5f,
            }, new Vec3(0f, 0f, 1f));
            Step(world, PlayerCommand.None, 10); // its swing starts

            var events = Step(world, Jump);
            events.AddRange(Step(world, PlayerCommand.None, 30));

            Assert.That(events.OfType<PlayerDamaged>().Count(), Is.EqualTo(1));
        }

        // --- Snapshots --------------------------------------------------------------------------------

        [Test]
        public void AJumpInTheAirSurvivesASnapshot()
        {
            var world = World();
            world.SetJumpHeightPercent(P, 150);
            Step(world, new PlayerCommand(1f, 0f, jump: true, sprint: true));
            Step(world, new PlayerCommand(1f, 0f, sprint: true), 10);

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());
            Step(world, new PlayerCommand(1f, 0f, sprint: true), 40);
            Step(copy, new PlayerCommand(1f, 0f, sprint: true), 40);

            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }
    }
}
