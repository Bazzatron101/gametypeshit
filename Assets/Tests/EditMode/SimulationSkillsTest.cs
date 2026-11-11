using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationSkillsTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static SkillDefinition Slash => new SkillDefinition
        {
            Id = "rending-slash", Name = "Rending Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
            CooldownSeconds = 4f, UnlockLevel = 1, Tags = new[] { "melee", "physical" },
        };

        private static SkillDefinition Slam => new SkillDefinition
        {
            Id = "ground-slam", Name = "Ground Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3.5f,
            CooldownSeconds = 6f, UnlockLevel = 1, Tags = new[] { "melee", "area" },
        };

        private static SkillDefinition Bolt => new SkillDefinition
        {
            Id = "fire-bolt", Name = "Fire Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 12f, Radius = 0.5f,
            ProjectileSpeed = 15f, CooldownSeconds = 2f, UnlockLevel = 1, ScalingStat = Stat.Intelligence,
            Tags = new[] { "fire", "projectile", "magic" },
        };

        private static SkillDefinition Burst => new SkillDefinition
        {
            Id = "flame-burst", Name = "Flame Burst", Effect = SkillEffectKind.Area, Damage = 35, Radius = 4f,
            CooldownSeconds = 8f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "area", "magic" },
        };

        private static SimulationConfig Config(params SkillDefinition[] skills) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            LightAttack = new AttackDefinition { Damage = 10, Range = 2f, ArcDegrees = 120f, CooldownSeconds = 0.4f },
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            StatEffects = new StatEffectsDefinition { DamagePerStrength = 0.05f, DamagePerIntelligence = 0.05f },
            Skills = skills.Length > 0 ? skills : new[] { Slash, Slam, Bolt, Burst },
        };

        /// <summary>Never moves or fights back.</summary>
        private static EnemyDefinition Dummy(int health = 1000, int xp = 0) => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = xp,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerCommand cmd) =>
            new Dictionary<PlayerId, PlayerCommand> { [P1] = cmd };

        private static Dictionary<PlayerId, PlayerCommand> UseSlot(int slot) => Cmd(new PlayerCommand(0f, 0f, useSkillSlot: slot));

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

        private static SimulationWorld World(SimulationConfig config = null)
        {
            var sim = new SimulationWorld(config ?? Config());
            sim.AddPlayer(P1, Vec3.Zero); // faces +Z
            return sim;
        }

        /// <summary>Gains XP by light-attacking a one-hit enemy worth it, standing in front of the player.</summary>
        private static List<SimulationEvent> GainXp(SimulationWorld sim, int xp)
        {
            sim.SpawnEnemy(Dummy(health: 1, xp: xp), sim.GetPlayer(P1).Position + new Vec3(0f, 0f, 1f));
            var start = sim.Tick;
            return RunTo(sim, start + 30, t => t == start + 1 ? Cmd(new PlayerCommand(0f, 0f, lightAttack: true)) : NoInput);
        }

        // --- Loading and the bar ----------------------------------------------------------

        [Test]
        public void StarterSkillsFillTheFirstSlotsInDataOrder()
        {
            var slots = World().GetPlayer(P1).SkillSlots;

            Assert.That(slots.Count, Is.EqualTo(6));
            Assert.That(slots.Select(s => s.Skill?.Id), Is.EqualTo(new[] { "rending-slash", "ground-slam", "fire-bolt", null, null, null }));
        }

        [Test]
        public void SkillsCarryTheirTagsFromData()
        {
            var sim = World();
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            var events = RunTo(sim, 1, _ => UseSlot(0));

            var hit = events.OfType<DamageDealt>().Single();
            Assert.That(hit.Skill.Id, Is.EqualTo("rending-slash"));
            Assert.That(hit.Skill.HasTag("melee"), Is.True);
            Assert.That(hit.Skill.HasTag("fire"), Is.False);
        }

        [Test]
        public void DuplicateSkillIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(Slash, Slash)));
        }

        // --- Effects ----------------------------------------------------------------------

        [Test]
        public void ConeSkillHitsInFrontOnly()
        {
            var sim = World();
            var ahead = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2.5f));
            var behind = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, -1.5f));

            var events = RunTo(sim, 1, _ => UseSlot(0));

            Assert.That(sim.GetEnemy(ahead).Health, Is.EqualTo(970));
            Assert.That(sim.GetEnemy(behind).Health, Is.EqualTo(1000));
            var used = events.OfType<SkillUsed>().Single();
            Assert.That((used.Player, used.Skill.Id, used.Slot), Is.EqualTo((P1, "rending-slash", 0)));
        }

        [Test]
        public void AreaSkillHitsAllAroundWithinItsRadius()
        {
            var sim = World();
            var front = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 3f));
            var back = sim.SpawnEnemy(Dummy(), new Vec3(-2f, 0f, -2f));
            var far = sim.SpawnEnemy(Dummy(), new Vec3(4f, 0f, 0f));

            RunTo(sim, 1, _ => UseSlot(1));

            Assert.That(sim.GetEnemy(front).Health, Is.EqualTo(980));
            Assert.That(sim.GetEnemy(back).Health, Is.EqualTo(980));
            Assert.That(sim.GetEnemy(far).Health, Is.EqualTo(1000));
        }

        [Test]
        public void ProjectileTravelsAndHitsOnlyTheFirstEnemyInItsPath()
        {
            var sim = World();
            var first = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 6f));
            var second = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 9f));

            var events = RunTo(sim, 1, _ => UseSlot(2));
            Assert.That(events.OfType<DamageDealt>(), Is.Empty, "a bolt is not instant");
            Assert.That(sim.Projectiles.Count, Is.EqualTo(1));

            events = RunTo(sim, 60);

            var hit = events.OfType<DamageDealt>().Single();
            Assert.That(hit.Target, Is.EqualTo(first));
            // 5.5 m to the edge of the hit radius at 0.25 m per tick: tick 23 at the earliest.
            Assert.That(hit.Tick, Is.InRange(22, 24));
            Assert.That(sim.GetEnemy(second).Health, Is.EqualTo(1000));
            Assert.That(sim.Projectiles, Is.Empty);
            Assert.That(events.OfType<ProjectileEnded>().Single().HitEnemy, Is.True);
        }

        [Test]
        public void ProjectileExpiresAtItsRange()
        {
            var sim = World();

            RunTo(sim, 1, _ => UseSlot(2));
            var events = RunTo(sim, 60);

            Assert.That(sim.Projectiles, Is.Empty);
            var ended = events.OfType<ProjectileEnded>().Single();
            Assert.That(ended.HitEnemy, Is.False);
            Assert.That(ended.Tick, Is.InRange(48, 50), "12 m at 15 m/s is 48 ticks");
        }

        [Test]
        public void DefeatingAnEnemyWithASkillGrantsXp()
        {
            var sim = World();
            sim.SpawnEnemy(Dummy(health: 5, xp: 20), new Vec3(0f, 0f, 2f));

            RunTo(sim, 1, _ => UseSlot(0));

            Assert.That(sim.GetPlayer(P1).Xp, Is.EqualTo(20));
        }

        [Test]
        public void SkillDamageScalesWithItsOwnStat()
        {
            var sim = World();
            GainXp(sim, 50);
            RunTo(sim, sim.Tick + 2, _ => Cmd(new PlayerCommand(0f, 0f, allocateStat: Stat.Intelligence))); // +10%
            var target = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            var start = sim.Tick;
            var events = RunTo(sim, start + 30, t => t == start + 1 ? UseSlot(0) : t == start + 2 ? UseSlot(2) : NoInput);

            var damage = events.OfType<DamageDealt>().ToList();
            Assert.That(damage.Single(d => d.Skill.Id == "rending-slash").Amount, Is.EqualTo(30), "a Strength skill: Intelligence does not help");
            Assert.That(damage.Single(d => d.Skill.Id == "fire-bolt").Amount, Is.EqualTo(28), "25 at +10% is 27.5, rounded up");
            Assert.That(sim.GetEnemy(target).Health, Is.EqualTo(942));
        }

        // --- Cooldowns --------------------------------------------------------------------

        [Test]
        public void SkillCannotBeReusedDuringItsCooldown()
        {
            var sim = World();

            var events = RunTo(sim, 240, _ => UseSlot(0)); // 4 s cooldown
            Assert.That(events.OfType<SkillUsed>().Count(), Is.EqualTo(1));
            Assert.That(sim.GetPlayer(P1).SkillSlots[0].ReadyAtTick, Is.EqualTo(241));

            events = RunTo(sim, 241, _ => UseSlot(0));
            Assert.That(events.OfType<SkillUsed>().Count(), Is.EqualTo(1), "ready again once the cooldown has passed");
        }

        [Test]
        public void EachSkillHasItsOwnCooldownAndNothingElseLimitsUse()
        {
            var sim = World();

            var events = RunTo(sim, 3, t => UseSlot(t - 1));

            Assert.That(events.OfType<SkillUsed>().Select(e => e.Slot), Is.EqualTo(new[] { 0, 1, 2 }), "no mana, no stamina, no shared cooldown");
        }

        [TestCase(3, TestName = "Empty slot does nothing")]
        [TestCase(6, TestName = "Slot past the bar does nothing")]
        [TestCase(-1, TestName = "Negative slot does nothing")]
        public void UnusableSlotsDoNothing(int slot)
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => UseSlot(slot));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
        }

        [Test]
        public void SkillsCannotBeUsedMidDodge()
        {
            var sim = World();

            var events = RunTo(sim, 5, t => t == 1 ? Cmd(new PlayerCommand(0f, 0f, dodge: true)) : UseSlot(1));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
        }

        // --- Unlocks ----------------------------------------------------------------------

        [Test]
        public void SkillUnlocksAtItsLevelIntoTheFirstEmptySlot()
        {
            var sim = World();

            var events = GainXp(sim, 50); // level 2
            Assert.That(events.OfType<SkillUnlocked>(), Is.Empty);
            Assert.That(sim.GetPlayer(P1).SkillSlots[3].Skill, Is.Null);

            events = GainXp(sim, 100); // level 3

            var unlocked = events.OfType<SkillUnlocked>().Single();
            Assert.That((unlocked.Player, unlocked.Skill.Id, unlocked.Slot), Is.EqualTo((P1, "flame-burst", (int?)3)));
            Assert.That(sim.GetPlayer(P1).SkillSlots[3].Skill.Id, Is.EqualTo("flame-burst"));
        }

        [Test]
        public void JumpingPastAnUnlockLevelStillUnlocksTheSkill()
        {
            var sim = World();

            var events = GainXp(sim, 400); // level 1 to 4 in one go

            Assert.That(sim.GetPlayer(P1).Level, Is.EqualTo(4));
            Assert.That(events.OfType<SkillUnlocked>().Select(e => e.Skill.Id), Is.EqualTo(new[] { "flame-burst" }));
        }

        [Test]
        public void UnlockedSkillIsUsableRightAway()
        {
            var sim = World();
            GainXp(sim, 150);
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, -3f)); // behind: the area reaches it anyway

            RunTo(sim, sim.Tick + 1, _ => UseSlot(3));

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(965));
        }

        [Test]
        public void UnlockWithAFullBarIsOwnedButUnslotted()
        {
            var extra = Enumerable.Range(0, 6).Select(i => new SkillDefinition
            {
                Id = $"starter-{i}", Name = $"Starter {i}", Effect = SkillEffectKind.Area, Damage = 1, Radius = 1f, CooldownSeconds = 1f, UnlockLevel = 1,
            }).ToList();
            extra.Add(Burst);
            var sim = World(Config(extra.ToArray()));

            var events = GainXp(sim, 150);

            var unlocked = events.OfType<SkillUnlocked>().Single();
            Assert.That(unlocked.Slot, Is.Null, "no free slot on the bar");
            Assert.That(sim.GetPlayer(P1).SkillSlots.Select(s => s.Skill.Id), Has.None.EqualTo("flame-burst"));
            Assert.That(sim.GetPlayer(P1).KnownSkills.Select(s => s.Id), Has.Some.EqualTo("flame-burst"), "owned, ready for a loadout screen");
        }

        // --- Review fixes -----------------------------------------------------------------

        [Test]
        public void ASkillTakesTheTickSoNoAttackFiresWithIt()
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => Cmd(new PlayerCommand(0f, 0f, lightAttack: true, useSkillSlot: 1)));

            Assert.That(events.OfType<SkillUsed>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<AttackPerformed>(), Is.Empty);
        }

        [Test]
        public void SkillsCannotBeUsedDuringAHeavyWindup()
        {
            var sim = World();

            var events = RunTo(sim, 10, t => t == 1 ? Cmd(new PlayerCommand(0f, 0f, heavyAttack: true)) : UseSlot(1));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
        }

        [Test]
        public void SkillsCannotBeUsedWhileDefeated()
        {
            var sim = World();
            sim.SpawnEnemy(new EnemyDefinition
            {
                Name = "Killer", MaxHealth = 1000, MoveSpeed = 0f, AttackDamage = 1000, AttackRange = 2f, AttackWindupSeconds = 0.1f,
            }, new Vec3(0f, 0f, 1.5f));
            RunTo(sim, 10);
            Assert.That(sim.GetPlayer(P1).IsDefeated, Is.True);

            var events = RunTo(sim, 20, _ => UseSlot(1));

            Assert.That(events.OfType<SkillUsed>(), Is.Empty);
        }

        private static IEnumerable<TestCaseData> InvalidSkills()
        {
            TestCaseData Case(string name, Action<SkillDefinition> breakIt) => new TestCaseData(breakIt).SetName(name);
            yield return Case("Skill unlocking above the level cap is rejected", s => s.UnlockLevel = 11);
            yield return Case("Skill with no name is rejected", s => s.Name = " ");
            yield return Case("Negative cooldown is rejected", s => s.CooldownSeconds = -1f);
            yield return Case("Negative damage is rejected", s => s.Damage = -5);
            yield return Case("Negative range is rejected", s => s.Range = -1f);
            yield return Case("Negative radius is rejected", s => s.Radius = -1f);
            yield return Case("Cone arc of zero is rejected", s => s.ArcDegrees = 0f);
            yield return Case("Cone arc over 360 is rejected", s => s.ArcDegrees = 400f);
            yield return Case("Projectile with no range is rejected", s => { s.Effect = SkillEffectKind.Projectile; s.Range = 0f; });
            yield return Case("Null tags are rejected", s => s.Tags = null);
        }

        [TestCaseSource(nameof(InvalidSkills))]
        public void InvalidSkillDataIsRejected(Action<SkillDefinition> breakIt)
        {
            var broken = Slash;
            breakIt(broken);

            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(broken, Slam)));
        }

        [Test]
        public void MoreStartersThanSlotsIsRejected()
        {
            var starters = Enumerable.Range(0, SimulationWorld.SkillSlotCount + 1).Select(i => new SkillDefinition
            {
                Id = $"starter-{i}", Name = $"Starter {i}", Effect = SkillEffectKind.Area, Damage = 1, Radius = 1f, CooldownSeconds = 1f, UnlockLevel = 1,
            }).ToArray();

            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(starters)));
        }

        [Test]
        public void ProjectileKeepsTheNumbersItWasLaunchedWith()
        {
            var bolt = Bolt;
            var sim = World(Config(Slash, Slam, bolt));
            RunTo(sim, 1, _ => UseSlot(2));

            bolt.Range = 1f; // editing shared data mid-flight must not cut the bolt short
            var events = RunTo(sim, 30);

            Assert.That(events.OfType<ProjectileEnded>(), Is.Empty, "still flying after 7 m");
        }
    }
}
