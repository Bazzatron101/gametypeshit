using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The world operations the test arena drives (and that later systems such as quests, gates and
    /// a loadout screen reuse): granting XP, teaching skills, rearranging the bar, clearing
    /// cooldowns, healing and despawning. Operations queue and apply at the start of the next tick.
    /// </summary>
    public class SimulationArenaControlsTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static SkillDefinition Skill(string id, int unlockLevel = 1, float cooldown = 4f) => new SkillDefinition
        {
            Id = id, Name = id, Effect = SkillEffectKind.Area, Damage = 10, Radius = 3f, CooldownSeconds = cooldown, UnlockLevel = unlockLevel,
            Tags = new[] { "area" },
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Skills = new[] { Skill("a"), Skill("b"), Skill("c"), Skill("late", unlockLevel: 5), Skill("later", unlockLevel: 8) },
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerCommand cmd) => new Dictionary<PlayerId, PlayerCommand> { [P1] = cmd };

        private static SimulationWorld World()
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);
            return sim;
        }

        private static string[] Bar(SimulationWorld sim) => sim.GetPlayer(P1).SkillSlots.Select(s => s.Skill?.Id).ToArray();

        // --- XP and level ---------------------------------------------------------------

        [Test]
        public void GrantedXpLevelsThroughTheNormalPathOnTheNextTick()
        {
            var sim = World();

            sim.GrantXp(P1, 500); // 50 + 100 + 150 + 200 = level 5
            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(1), "queued");
            sim.Step(NoInput);

            var state = sim.GetPlayer(P1);
            Assert.That(state.Level, Is.EqualTo(5));
            Assert.That(state.UnspentStatPoints, Is.EqualTo(12));
            Assert.That(sim.Events.OfType<PlayerLeveledUp>().Select(e => e.NewLevel), Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(sim.Events.OfType<SkillUnlocked>().Single().Skill.Id, Is.EqualTo("late"), "level 5 unlock fired");
        }

        [Test]
        public void XpToReachALevelIsTheSumOfTheCurve()
        {
            var sim = World();

            Assert.That(sim.XpToReachLevel(P1, 5), Is.EqualTo(500));
            Assert.That(sim.XpToReachLevel(P1, 1), Is.EqualTo(0), "already there");
            Assert.That(sim.XpToReachLevel(P1, 99), Is.EqualTo(sim.XpToReachLevel(P1, 10)), "clamped to the cap");
        }

        // --- Skills and the bar ---------------------------------------------------------

        [Test]
        public void TaughtSkillIsLearnedAndSlottedOnTheNextTick()
        {
            var sim = World();

            sim.TeachSkill(P1, "later");
            sim.Step(NoInput);

            Assert.That(Bar(sim), Is.EqualTo(new[] { "a", "b", "c", "later", null, null }));
            Assert.That(sim.Events.OfType<SkillUnlocked>().Single().Skill.Id, Is.EqualTo("later"));
        }

        [Test]
        public void TeachingAKnownSkillDoesNothing()
        {
            var sim = World();

            sim.TeachSkill(P1, "a");
            sim.Step(NoInput);

            Assert.That(sim.Events.OfType<SkillUnlocked>(), Is.Empty);
            Assert.That(sim.GetPlayer(P1).KnownSkills.Count, Is.EqualTo(3));
        }

        [Test]
        public void TeachingAnUnknownSkillIdIsRejected()
        {
            Assert.Throws<ArgumentException>(() => World().TeachSkill(P1, "nope"));
        }

        [Test]
        public void AssignCommandPutsAKnownSkillInAnEmptySlot()
        {
            var sim = World();
            sim.TeachSkill(P1, "later");
            sim.Step(NoInput); // later goes to slot 3

            sim.Step(Cmd(new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(5, "later"))));

            Assert.That(Bar(sim), Is.EqualTo(new[] { "a", "b", "c", null, null, "later" }), "moved, not copied");
            var changes = sim.Events.OfType<SkillSlotChanged>().Select(e => (e.Slot, e.Skill?.Id)).ToArray();
            Assert.That(changes, Is.EquivalentTo(new[] { (5, "later"), (3, (string)null) }), "both slots are reported");
        }

        [Test]
        public void AssigningOntoAnOccupiedSlotSwapsTheTwo()
        {
            var sim = World();

            sim.Step(Cmd(new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(0, "c"))));

            Assert.That(Bar(sim), Is.EqualTo(new[] { "c", "b", "a", null, null, null }));
            Assert.That(sim.Events.OfType<SkillSlotChanged>().Select(e => (e.Slot, e.Skill.Id)),
                Is.EquivalentTo(new[] { (0, "c"), (2, "a") }));
        }

        [Test]
        public void AssigningNothingClearsASlot()
        {
            var sim = World();

            sim.Step(Cmd(new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(1, null))));

            Assert.That(Bar(sim), Is.EqualTo(new[] { "a", null, "c", null, null, null }));
            Assert.That(sim.GetPlayer(P1).KnownSkills.Select(s => s.Id), Has.Some.EqualTo("b"), "still known");
        }

        [TestCase(6, "a", TestName = "Assigning past the bar is ignored")]
        [TestCase(0, "late", TestName = "Assigning a skill the player does not know is ignored")]
        [TestCase(0, "nope", TestName = "Assigning an unknown skill id is ignored")]
        public void InvalidAssignmentsAreIgnored(int slot, string skillId)
        {
            var sim = World();

            sim.Step(Cmd(new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(slot, skillId))));

            Assert.That(Bar(sim), Is.EqualTo(new[] { "a", "b", "c", null, null, null }));
            Assert.That(sim.Events.OfType<SkillSlotChanged>(), Is.Empty);
        }

        [Test]
        public void MovingASkillKeepsItsCooldown()
        {
            var sim = World();
            sim.Step(Cmd(new PlayerCommand(0f, 0f, useSkillSlot: 0))); // "a" on cooldown

            sim.Step(Cmd(new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(4, "a"))));

            Assert.That(sim.GetPlayer(P1).SkillSlots[4].ReadyAtTick, Is.EqualTo(1 + 240));
        }

        [Test]
        public void RaiseToLevelIsWorkedOutWhenItAppliesSoRepeatsDoNotOvershoot()
        {
            var sim = World();

            sim.RaiseToLevel(P1, 5);
            sim.RaiseToLevel(P1, 5); // a double click before the tick
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(5));
            Assert.That(sim.GetPlayer(P1).Xp, Is.EqualTo(0));
        }

        [Test]
        public void RaiseToLevelNeverLowersALevel()
        {
            var sim = World();
            sim.RaiseToLevel(P1, 5);
            sim.Step(NoInput);

            sim.RaiseToLevel(P1, 3);
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(5));
            Assert.That(sim.Events.OfType<XpGained>(), Is.Empty);
        }

        // --- Tuning helpers -------------------------------------------------------------

        [Test]
        public void ResetCooldownsMakesEverySkillReadyOnTheNextTick()
        {
            var sim = World();
            sim.Step(Cmd(new PlayerCommand(0f, 0f, useSkillSlot: 0)));

            sim.ResetCooldowns(P1);
            sim.Step(Cmd(new PlayerCommand(0f, 0f, useSkillSlot: 0)));

            Assert.That(sim.Events.OfType<SkillUsed>().Count(), Is.EqualTo(1), "usable again straight away");
        }

        [Test]
        public void RestoreHealthFillsHealthAndRevivesTheDefeated()
        {
            var sim = World();
            sim.SpawnEnemy(new EnemyDefinition { Name = "Killer", MaxHealth = 1000, MoveSpeed = 0f, AttackDamage = 1000, AttackRange = 2f, AttackWindupSeconds = 0.1f },
                new Vec3(0f, 0f, 1.5f));
            for (var i = 0; i < 10; i++) sim.Step(NoInput);
            Assert.That(sim.GetPlayer(P1).IsDefeated, Is.True);

            sim.RestoreHealth(P1);
            sim.Step(NoInput);

            var state = sim.GetPlayer(P1);
            Assert.That((state.Health, state.LifeState), Is.EqualTo((state.MaxHealth, PlayerLifeState.Alive)));
        }

        [Test]
        public void RestoreHealthClearsAPendingWindupAndDodge()
        {
            var sim = World();
            sim.Step(Cmd(new PlayerCommand(0f, 0f, heavyAttack: true)));
            Assert.That(sim.GetPlayer(P1).IsWindingUp, Is.True);

            sim.RestoreHealth(P1);
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).IsWindingUp, Is.False);
            Assert.That(sim.GetPlayer(P1).IsDodging, Is.False);
        }

        [Test]
        public void HasModifierReportsWhatIsInEffect()
        {
            var sim = World();
            var modifier = new ModifierDefinition { Id = "wide", Name = "Wide", AppliesToTag = "area", RadiusAdd = 1f };

            sim.GrantModifier(P1, modifier);
            Assert.That(sim.HasModifier(P1, "wide"), Is.False, "not until the next tick");
            sim.Step(NoInput);

            Assert.That(sim.HasModifier(P1, "wide"), Is.True);
            Assert.That(sim.HasModifier(P1, "other"), Is.False);
        }

        [Test]
        public void DespawnedEnemyIsGoneForGood()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(new EnemyDefinition { Name = "Bag", MaxHealth = 10 }, new Vec3(0f, 0f, 2f));

            sim.DespawnEnemy(enemy);

            Assert.That(sim.HasEnemy(enemy), Is.False);
            Assert.That(sim.EnemyIds, Has.No.Member(enemy));
            Assert.Throws<ArgumentException>(() => sim.GetEnemy(enemy));
            Assert.DoesNotThrow(() => sim.Step(NoInput));
        }
    }
}
