using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationChargeTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        // Slots: 0 Slash (damage charge), 1 Slam (size charge), 2 Bolt (no charge), 3 Late (charge unlocks at level 3).
        private const int Slash = 0, Slam = 1, Bolt = 2, Late = 3;

        private static SkillDefinition SlashSkill => new SkillDefinition
        {
            Id = "slash", Name = "Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f, CooldownSeconds = 4f,
            Tags = new[] { "melee" }, ChargeSeconds = 1f, ChargedDamagePercent = 100,
        };

        private static SkillDefinition SlamSkill => new SkillDefinition
        {
            Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3f, CooldownSeconds = 6f,
            Tags = new[] { "area" }, ChargeSeconds = 1f, ChargedSizePercent = 100,
        };

        private static SkillDefinition BoltSkill => new SkillDefinition
        {
            Id = "bolt", Name = "Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 12f, Radius = 0.5f, ProjectileSpeed = 15f,
            CooldownSeconds = 2f, Tags = new[] { "projectile" },
        };

        private static SkillDefinition LateSkill => new SkillDefinition
        {
            Id = "late", Name = "Late", Effect = SkillEffectKind.Area, Damage = 10, Radius = 2f, CooldownSeconds = 1f,
            Tags = new[] { "area" }, ChargeSeconds = 1f, ChargedSizePercent = 50, ChargeUnlockLevel = 3,
        };

        private static SimulationConfig Config(params SkillDefinition[] skills) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            ChargeMoveSpeedPercent = 40,
            ChargeTapGraceSeconds = 0f, // charge from the first held tick, so the maths below is exact
            SoftLockOn = new SoftLockOnDefinition { Enabled = false },
            Dodge = new DodgeDefinition { Distance = 0f, DurationSeconds = 0.25f, InvulnerabilitySeconds = 0.3f, CooldownSeconds = 0.8f },
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Skills = skills.Length > 0 ? skills : new[] { SlashSkill, SlamSkill, BoltSkill, LateSkill },
        };

        private static EnemyDefinition Dummy(int health = 1000) => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        /// <summary>Stands still and swings: starts on tick 1 when in reach, lands on tick 31.</summary>
        private static EnemyDefinition Brute(int damage = 10) => new EnemyDefinition
        {
            Name = "Brute", MaxHealth = 1000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = damage, AttackRange = 1.6f,
            AttackWindupSeconds = 0.5f, AttackCooldownSeconds = 100f, XpReward = 0,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerCommand cmd) => new Dictionary<PlayerId, PlayerCommand> { [P1] = cmd };

        /// <summary>Press the key on its first tick.</summary>
        private static int Keys(params int[] slots) => slots.Aggregate(0, (mask, slot) => mask | (1 << slot));

        private static Dictionary<PlayerId, PlayerCommand> Press(int slot) => Cmd(new PlayerCommand(0f, 0f, useSkillSlot: slot, heldSkillSlots: Keys(slot)));

        private static Dictionary<PlayerId, PlayerCommand> Hold(int slot, float moveX = 0f, bool lightAttack = false, int? otherPress = null, bool dodge = false) =>
            Cmd(new PlayerCommand(moveX, 0f, lightAttack: lightAttack, dodge: dodge, useSkillSlot: otherPress,
                heldSkillSlots: otherPress.HasValue ? Keys(slot, otherPress.Value) : Keys(slot)));

        private static List<SimulationEvent> RunTo(SimulationWorld sim, int lastTick, Func<int, Dictionary<PlayerId, PlayerCommand>> commandsForTick = null)
        {
            var events = new List<SimulationEvent>();
            while (sim.Tick < lastTick)
            {
                sim.Step(commandsForTick?.Invoke(sim.Tick + 1) ?? NoInput);
                events.AddRange(sim.Events);
            }
            return events;
        }

        /// <summary>Press on tick 1, keep holding through tick <paramref name="lastHeldTick"/>, release on the next.</summary>
        private static List<SimulationEvent> ChargeAndRelease(SimulationWorld sim, int slot, int lastHeldTick) =>
            RunTo(sim, lastHeldTick + 1, t => t == 1 ? Press(slot) : t <= lastHeldTick ? Hold(slot) : NoInput);

        private static SimulationWorld World(SimulationConfig config = null)
        {
            var sim = new SimulationWorld(config ?? Config());
            sim.AddPlayer(P1, Vec3.Zero); // faces +Z
            return sim;
        }

        // --- Tap and hold ---------------------------------------------------------------

        [Test]
        public void ATapFiresTheNormalSkillAtOnce()
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => Cmd(new PlayerCommand(0f, 0f, useSkillSlot: Slam))); // pressed and let go within the tick

            var used = events.OfType<SkillUsed>().Single();
            Assert.That((used.ChargeFraction, used.Radius), Is.EqualTo((0f, 3f)));
        }

        [Test]
        public void HoldingStartsAChargeInsteadOfFiring()
        {
            var sim = World();

            var events = RunTo(sim, 10, t => t == 1 ? Press(Slam) : Hold(Slam));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
            var started = events.OfType<SkillChargeStarted>().Single();
            Assert.That((started.Player, started.Skill.Id, started.Slot), Is.EqualTo((P1, "slam", Slam)));
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(Slam));
            Assert.That(sim.GetPlayer(P1).ChargeFraction, Is.EqualTo(9f / 60f).Within(1e-4f));
        }

        [Test]
        public void ReleasingFiresWithASizeBonusScaledByTheCharge()
        {
            var sim = World();
            var outside = sim.SpawnEnemy(Dummy(), new Vec3(4.2f, 0f, 0f)); // beyond the normal 3 m, inside 4.5 m

            var events = ChargeAndRelease(sim, Slam, lastHeldTick: 30); // half a second of a one-second charge

            var used = events.OfType<SkillUsed>().Single();
            Assert.That(used.ChargeFraction, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(used.Radius, Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(sim.GetEnemy(outside).Health, Is.EqualTo(980), "damage is unchanged; only the size grew");
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.Null);
        }

        [Test]
        public void ChargeStopsGrowingAtFull()
        {
            var sim = World();

            var events = ChargeAndRelease(sim, Slam, lastHeldTick: 200);

            var used = events.OfType<SkillUsed>().Single();
            Assert.That((used.ChargeFraction, used.Radius), Is.EqualTo((1f, 6f)));
        }

        [Test]
        public void ADamageChargeHitsHarder()
        {
            var sim = World();
            var target = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            ChargeAndRelease(sim, Slash, lastHeldTick: 100);

            Assert.That(sim.GetEnemy(target).Health, Is.EqualTo(940), "30 at +100%");
        }

        [Test]
        public void TheCooldownStartsWhenTheSkillFires()
        {
            var sim = World();

            ChargeAndRelease(sim, Slam, lastHeldTick: 30);

            Assert.That(sim.GetPlayer(P1).SkillSlots[Slam].ReadyAtTick, Is.EqualTo(31 + 360));
        }

        [Test]
        public void ASkillWithoutAChargeFiresOnPressEvenWhenHeld()
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => Press(Bolt));

            Assert.That(events.OfType<SkillUsed>().Single().Skill.Id, Is.EqualTo("bolt"));
            Assert.That(events.OfType<SkillChargeStarted>(), Is.Empty);
        }

        // --- While charging -------------------------------------------------------------

        [Test]
        public void MovementIsSlowedWhileCharging()
        {
            var sim = World();

            RunTo(sim, 60, t => t == 1 ? Press(Slam) : Hold(Slam, moveX: 1f));

            Assert.That(sim.GetPlayer(P1).Position.X, Is.EqualTo(59 * 5f * 0.4f / 60f).Within(1e-3f), "40% of 5 m/s from tick 2");
        }

        [Test]
        public void AttacksAndOtherSkillsAreBlockedWhileCharging()
        {
            var sim = World();

            var events = RunTo(sim, 20, t => t == 1 ? Press(Slam) : Hold(Slam, lightAttack: true, otherPress: Slash));

            Assert.That(events.OfType<AttackPerformed>(), Is.Empty);
            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(Slam), "still charging");
        }

        [Test]
        public void BeingDefeatedDropsTheCharge()
        {
            var sim = World();
            sim.SpawnEnemy(Brute(damage: 1000), new Vec3(0f, 0f, 1.2f));

            var events = RunTo(sim, 40, t => t == 1 ? Press(Slam) : Hold(Slam));

            Assert.That(sim.GetPlayer(P1).IsDefeated, Is.True);
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.Null);
            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
        }

        // --- Perfect dodge ----------------------------------------------------------------

        [Test]
        public void APerfectDodgeLaunchesTheChargeAtFullAtTheAttacker()
        {
            var sim = World();
            var brute = sim.SpawnEnemy(Brute(), new Vec3(1.2f, 0f, 0f)); // to the player's right; its hit lands on tick 31

            var events = RunTo(sim, 32, t => t == 1 ? Press(Slash) : t == 20 ? Hold(Slash, dodge: true) : Hold(Slash));

            Assert.That(events.OfType<AttackDodged>().Single().Tick, Is.EqualTo(31));
            var used = events.OfType<SkillUsed>().Single();
            Assert.That((used.Tick, used.ChargeFraction, used.IsCounter), Is.EqualTo((31, 1f, true)));
            Assert.That(sim.GetEnemy(brute).Health, Is.EqualTo(940), "full damage charge, aimed at the attacker");
            Assert.That(sim.GetPlayer(P1).Facing.X, Is.EqualTo(1f).Within(1e-3f), "turned to face it");
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.Null, "the charge is spent");
        }

        [Test]
        public void ADodgeThatCatchesNothingKeepsTheCharge()
        {
            var sim = World();

            var events = RunTo(sim, 40, t => t == 1 ? Press(Slam) : t == 5 ? Hold(Slam, dodge: true) : Hold(Slam));

            Assert.That(events.OfType<DodgePerformed>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(Slam));
        }

        // --- Charge unlocks -------------------------------------------------------------

        [Test]
        public void BeforeItsChargeUnlocksASkillOnlyTaps()
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => Press(Late));

            Assert.That(events.OfType<SkillUsed>().Single().Skill.Id, Is.EqualTo("late"));
            Assert.That(sim.GetPlayer(P1).SkillSlots[Late].ChargeUnlocked, Is.False);
        }

        [Test]
        public void TheChargeUnlocksAtItsLevelWithAnEvent()
        {
            var sim = World();

            sim.RaiseToLevel(P1, 3);
            sim.Step(NoInput);

            var unlocked = sim.Events.OfType<SkillChargeUnlocked>().Single();
            Assert.That((unlocked.Player, unlocked.Skill.Id), Is.EqualTo((P1, "late")));
            Assert.That(sim.GetPlayer(P1).SkillSlots[Late].ChargeUnlocked, Is.True);

            var start = sim.Tick;
            var events = RunTo(sim, start + 2, t => t == start + 1 ? Press(Late) : Hold(Late));
            Assert.That(events.OfType<SkillChargeStarted>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ChargesAvailableFromTheStartAnnounceNothing()
        {
            var sim = World();

            sim.RaiseToLevel(P1, 3);
            sim.Step(NoInput);

            Assert.That(sim.Events.OfType<SkillChargeUnlocked>().Select(e => e.Skill.Id), Is.EqualTo(new[] { "late" }));
        }

        [Test]
        public void AChargeSetBelowTheSkillsOwnLevelComesWithTheSkill()
        {
            var early = new SkillDefinition
            {
                Id = "early", Name = "Early", Effect = SkillEffectKind.Area, Damage = 10, Radius = 2f, CooldownSeconds = 1f, Tags = new[] { "area" },
                UnlockLevel = 3, ChargeSeconds = 1f, ChargedSizePercent = 50, ChargeUnlockLevel = 2,
            };
            var sim = World(Config(SlashSkill, early));

            sim.RaiseToLevel(P1, 3);
            sim.Step(NoInput);

            Assert.That(sim.Events.OfType<SkillUnlocked>().Single().Skill.Id, Is.EqualTo("early"));
            Assert.That(sim.Events.OfType<SkillChargeUnlocked>(), Is.Empty, "nothing separate to announce");
            Assert.That(sim.GetPlayer(P1).SkillSlots[1].ChargeUnlocked, Is.True);
        }

        // --- Review fixes ---------------------------------------------------------------

        [Test]
        public void HoldingAnotherKeyAsWellKeepsTheChargeGoing()
        {
            var sim = World();

            var events = RunTo(sim, 20, t => t == 1 ? Press(Slam) : Cmd(new PlayerCommand(0f, 0f, heldSkillSlots: Keys(Slash, Slam))));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty, "key 1 being down does not release key 2's charge");
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(Slam));
        }

        [Test]
        public void ChargingStillStartsWhileAnotherKeyIsDown()
        {
            var sim = World();

            var events = RunTo(sim, 10, t => Cmd(new PlayerCommand(0f, 0f, useSkillSlot: t == 1 ? Slam : (int?)null, heldSkillSlots: Keys(Slash, Slam))));

            Assert.That(events.OfType<SkillChargeStarted>().Single().Slot, Is.EqualTo(Slam));
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(Slam));
        }

        [Test]
        public void AQuickTapWithinTheGraceFiresTheNormalSkill()
        {
            var config = Config();
            config.ChargeTapGraceSeconds = 0.15f; // 9 ticks
            var sim = World(config);

            var events = ChargeAndRelease(sim, Slam, lastHeldTick: 5); // a human tap: a few frames

            var used = events.OfType<SkillUsed>().Single();
            Assert.That((used.ChargeFraction, used.Radius), Is.EqualTo((0f, 3f)));
        }

        [Test]
        public void ChargeBuildsAfterTheGrace()
        {
            var config = Config();
            config.ChargeTapGraceSeconds = 0.15f; // 9 ticks
            var sim = World(config);

            var events = ChargeAndRelease(sim, Slam, lastHeldTick: 39); // 39 ticks held: 30 of them past the grace

            Assert.That(events.OfType<SkillUsed>().Single().ChargeFraction, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void TheBarCannotBeRearrangedMidCharge()
        {
            var sim = World();
            RunTo(sim, 2, t => t == 1 ? Press(Slam) : Hold(Slam));

            var events = RunTo(sim, 3, _ => Cmd(new PlayerCommand(0f, 0f, heldSkillSlots: Keys(Slam), assignSkill: new SkillAssignment(Slam, "bolt"))));

            Assert.That(events.OfType<SkillSlotChanged>(), Is.Empty);
            Assert.That(sim.GetPlayer(P1).SkillSlots[Slam].Skill.Id, Is.EqualTo("slam"));
        }

        // --- Validation -----------------------------------------------------------------

        private static IEnumerable<TestCaseData> InvalidCharges()
        {
            TestCaseData Case(string name, Action<SkillDefinition> breakIt) => new TestCaseData(breakIt).SetName(name);
            yield return Case("Negative charge time is rejected", s => s.ChargeSeconds = -1f);
            yield return Case("Negative charged size bonus is rejected", s => s.ChargedSizePercent = -10);
            yield return Case("Negative charged damage bonus is rejected", s => s.ChargedDamagePercent = -10);
            yield return Case("Charge unlocking above the level cap is rejected", s => s.ChargeUnlockLevel = 11);
            yield return Case("A charge with no size or damage bonus is rejected", s => { s.ChargedSizePercent = 0; s.ChargedDamagePercent = 0; });
        }

        [TestCaseSource(nameof(InvalidCharges))]
        public void InvalidChargeDataIsRejected(Action<SkillDefinition> breakIt)
        {
            var broken = SlamSkill;
            breakIt(broken);

            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(SlashSkill, broken)));
        }
    }
}
