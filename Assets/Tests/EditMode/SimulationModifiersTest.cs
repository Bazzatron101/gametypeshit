using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationModifiersTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static SkillDefinition Slash => new SkillDefinition
        {
            Id = "rending-slash", Name = "Rending Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
            CooldownSeconds = 4f, Tags = new[] { "melee", "physical" },
        };

        private static SkillDefinition Slam => new SkillDefinition
        {
            Id = "ground-slam", Name = "Ground Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3.5f,
            CooldownSeconds = 6f, Tags = new[] { "melee", "area", "physical" },
        };

        private static SkillDefinition Bolt => new SkillDefinition
        {
            Id = "fire-bolt", Name = "Fire Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 12f, Radius = 0.5f,
            ProjectileSpeed = 15f, CooldownSeconds = 2f, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "projectile", "magic" },
        };

        private static SkillDefinition Burst => new SkillDefinition
        {
            Id = "flame-burst", Name = "Flame Burst", Effect = SkillEffectKind.Area, Damage = 35, Radius = 4f,
            CooldownSeconds = 8f, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "area", "magic" },
        };

        private static readonly ModifierDefinition Searing = new ModifierDefinition
        {
            Id = "searing", Name = "Searing", AppliesToTag = "fire", DamagePercent = 50,
        };

        private static readonly ModifierDefinition SplitShot = new ModifierDefinition
        {
            Id = "split-shot", Name = "Split Shot", AppliesToTag = "projectile", ExtraProjectiles = 2, ProjectileSpreadDegrees = 30f,
        };

        private static readonly ModifierDefinition EmberCore = new ModifierDefinition
        {
            Id = "ember-core", Name = "Ember Core", AppliesToTag = "fire", ExplosionRadius = 2f, ExplosionDamagePercent = 50,
        };

        private static readonly ModifierDefinition Brutal = new ModifierDefinition
        {
            Id = "brutal", Name = "Brutal", AppliesToTag = "melee", DamagePercent = 20,
        };

        /// <summary>Hand-authored for one signature skill.</summary>
        private static readonly ModifierDefinition DeepRend = new ModifierDefinition
        {
            Id = "deep-rend", Name = "Deep Rend", AppliesToSkillId = "rending-slash", DamagePercent = 50, CooldownPercent = -25, RangeAdd = 1f,
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            SoftLockOn = new SoftLockOnDefinition { Enabled = false }, // exact aim, so geometry is predictable
            Skills = new[] { Slash, Slam, Bolt, Burst },
        };

        private static EnemyDefinition Dummy() => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 1000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> UseSlot(int slot) =>
            new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, useSkillSlot: slot) };

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

        /// <summary>Player at the origin facing +Z with every test skill on the bar: slots 0 Slash, 1 Slam, 2 Bolt, 3 Burst.</summary>
        private static SimulationWorld World(params ModifierDefinition[] modifiers)
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);
            foreach (var m in modifiers) sim.GrantModifier(P1, m);
            return sim;
        }

        /// <summary>Uses one slot on the next tick and returns its first hit's damage.</summary>
        private static int HitWith(SimulationWorld sim, int slot, Vec3 enemyAt)
        {
            sim.SpawnEnemy(Dummy(), enemyAt);
            var start = sim.Tick;
            var events = RunTo(sim, start + 40, t => t == start + 1 ? UseSlot(slot) : NoInput);
            return events.OfType<DamageDealt>().First(d => !d.IsSecondary).Amount;
        }

        // --- Tag modifiers ----------------------------------------------------------------

        [Test]
        public void TagModifierChangesEverySkillWithThatTag()
        {
            Assert.That(HitWith(World(Searing), 2, new Vec3(0f, 0f, 3f)), Is.EqualTo(38), "Fire Bolt: 25 at +50% is 37.5, rounded up");
            Assert.That(HitWith(World(Searing), 3, new Vec3(0f, 0f, 3f)), Is.EqualTo(53), "Flame Burst: 35 at +50% is 52.5, rounded up");
        }

        [Test]
        public void SkillsWithoutTheTagAreUnaffected()
        {
            Assert.That(HitWith(World(Searing), 0, new Vec3(0f, 0f, 2f)), Is.EqualTo(30), "Rending Slash is not fire");
            Assert.That(HitWith(World(Searing), 1, new Vec3(0f, 0f, 2f)), Is.EqualTo(20), "Ground Slam is not fire");
        }

        [Test]
        public void ProjectileModifierFiresASpreadOfBolts()
        {
            var sim = World(SplitShot);
            var ahead = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 5f));
            var left = sim.SpawnEnemy(Dummy(), RotatedAhead(-30f, 5f));
            var right = sim.SpawnEnemy(Dummy(), RotatedAhead(30f, 5f));

            RunTo(sim, 1, _ => UseSlot(2));
            Assert.That(sim.Projectiles.Count, Is.EqualTo(3));
            RunTo(sim, 40);

            Assert.That(new[] { ahead, left, right }.Select(e => sim.GetEnemy(e).Health), Is.All.EqualTo(975));
        }

        [Test]
        public void ExplosionModifierMakesHitsBurstOntoNearbyEnemies()
        {
            var sim = World(EmberCore);
            var target = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 5f));
            var neighbour = sim.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 5.5f)); // off the bolt's path, within 2 m of the target
            var distant = sim.SpawnEnemy(Dummy(), new Vec3(4f, 0f, 5f));

            var events = RunTo(sim, 40, t => t == 1 ? UseSlot(2) : NoInput);

            Assert.That(sim.GetEnemy(target).Health, Is.EqualTo(975), "the direct hit only, not caught in its own burst");
            Assert.That(sim.GetEnemy(neighbour).Health, Is.EqualTo(987), "half of 25 is 12.5, rounded up to 13");
            Assert.That(sim.GetEnemy(distant).Health, Is.EqualTo(1000));
            Assert.That(events.OfType<Explosion>().Single().Radius, Is.EqualTo(2f));
            Assert.That(events.OfType<DamageDealt>().Single(d => d.Target == neighbour).IsSecondary, Is.True);
        }

        [Test]
        public void ExplosionsDoNotSetOffMoreExplosions()
        {
            var sim = World(EmberCore);
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
            sim.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 2f));
            sim.SpawnEnemy(Dummy(), new Vec3(3f, 0f, 1f)); // all within the 4 m burst

            var events = RunTo(sim, 1, _ => UseSlot(3)); // Flame Burst hits all three

            Assert.That(events.OfType<Explosion>().Count(), Is.EqualTo(3), "one burst per direct hit, none from the bursts themselves");
        }

        [Test]
        public void SynergyThreeBoltsThatEachExplode()
        {
            var sim = World(SplitShot, EmberCore);
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 5f));
            sim.SpawnEnemy(Dummy(), RotatedAhead(-30f, 5f));
            sim.SpawnEnemy(Dummy(), RotatedAhead(30f, 5f));

            var events = RunTo(sim, 40, t => t == 1 ? UseSlot(2) : NoInput);

            Assert.That(events.OfType<Explosion>().Count(), Is.EqualTo(3));
            Assert.That(events.OfType<DamageDealt>().Count(d => !d.IsSecondary), Is.EqualTo(3));
        }

        // --- Hand-authored modifiers ------------------------------------------------------

        [Test]
        public void HandAuthoredModifierChangesOnlyItsSkill()
        {
            Assert.That(HitWith(World(DeepRend), 0, new Vec3(0f, 0f, 3.8f)), Is.EqualTo(45), "+50% damage and +1 m reach on Rending Slash");
            Assert.That(HitWith(World(DeepRend), 1, new Vec3(0f, 0f, 2f)), Is.EqualTo(20), "Ground Slam is also melee but is not Rending Slash");
        }

        [Test]
        public void HandAuthoredAndTagModifiersStack()
        {
            var sim = World(DeepRend, Brutal);
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            var events = RunTo(sim, 1, _ => UseSlot(0));

            Assert.That(events.OfType<DamageDealt>().Single().Amount, Is.EqualTo(51), "30 at +50% and +20%, added: +70%");
            Assert.That(sim.GetPlayer(P1).SkillSlots[0].ReadyAtTick, Is.EqualTo(1 + 180), "4 s at 25% faster is 3 s");
        }

        [Test]
        public void TheSharedSkillDataIsNeverChanged()
        {
            var slash = Slash;
            var config = Config();
            config.Skills = new[] { slash, Slam, Bolt, Burst };
            var sim = new SimulationWorld(config);
            sim.AddPlayer(P1, Vec3.Zero);
            sim.GrantModifier(P1, DeepRend);

            RunTo(sim, 1, _ => UseSlot(0));

            Assert.That((slash.Damage, slash.Range, slash.CooldownSeconds), Is.EqualTo((30, 3f, 4f)));
        }

        // --- Granting and revoking --------------------------------------------------------

        [Test]
        public void RevokingAModifierRestoresTheSkill()
        {
            var sim = World(Searing);

            sim.RevokeModifier(P1, "searing");

            Assert.That(sim.GetPlayer(P1).ActiveModifiers, Is.Empty);
            Assert.That(HitWith(sim, 2, new Vec3(0f, 0f, 3f)), Is.EqualTo(25));
        }

        [Test]
        public void ActiveModifiersAreVisibleOnThePlayer()
        {
            var sim = World(Searing, DeepRend);
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).ActiveModifiers.Select(m => m.Id), Is.EqualTo(new[] { "searing", "deep-rend" }));
        }

        [Test]
        public void GrantingTheSameModifierTwiceHasNoExtraEffect()
        {
            var sim = World(Searing, Searing);
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).ActiveModifiers.Count, Is.EqualTo(1));
            Assert.That(HitWith(sim, 2, new Vec3(0f, 0f, 3f)), Is.EqualTo(38));
        }

        private static IEnumerable<TestCaseData> InvalidModifiers()
        {
            TestCaseData Case(string name, ModifierDefinition m) => new TestCaseData(m).SetName(name);
            yield return Case("Modifier with no target is rejected", new ModifierDefinition { Id = "x", Name = "X" });
            yield return Case("Modifier with two targets is rejected",
                new ModifierDefinition { Id = "x", Name = "X", AppliesToTag = "fire", AppliesToSkillId = "fire-bolt" });
            yield return Case("Modifier for a misspelt tag is rejected", new ModifierDefinition { Id = "x", Name = "X", AppliesToTag = "fiery" });
            yield return Case("Modifier for an unknown skill is rejected", new ModifierDefinition { Id = "x", Name = "X", AppliesToSkillId = "nope" });
            yield return Case("Modifier with no id is rejected", new ModifierDefinition { Id = "", Name = "X", AppliesToTag = "fire" });
            yield return Case("Negative extra projectiles are rejected",
                new ModifierDefinition { Id = "x", Name = "X", AppliesToTag = "fire", ExtraProjectiles = -1 });
        }

        [TestCaseSource(nameof(InvalidModifiers))]
        public void InvalidModifiersAreRejectedWhenGranted(ModifierDefinition modifier)
        {
            var sim = World();

            Assert.Throws<ArgumentException>(() => sim.GrantModifier(P1, modifier));
        }

        [Test]
        public void SkillWithAMisspeltTagIsRejected()
        {
            var config = Config();
            var typo = Slash;
            typo.Tags = new[] { "mele" };
            config.Skills = new[] { typo };

            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
        }

        // --- Review fixes -----------------------------------------------------------------

        [Test]
        public void GrantsTakeEffectAtTheNextTickAndAreAnnounced()
        {
            var sim = World();

            sim.GrantModifier(P1, Searing);
            Assert.That(sim.GetPlayer(P1).ActiveModifiers, Is.Empty, "queued, not applied between ticks");
            sim.Step(NoInput);

            Assert.That(sim.GetPlayer(P1).ActiveModifiers.Select(m => m.Id), Is.EqualTo(new[] { "searing" }));
            var granted = sim.Events.OfType<ModifierGranted>().Single();
            Assert.That((granted.Player, granted.Modifier.Id, granted.Tick), Is.EqualTo((P1, "searing", 1)));

            sim.RevokeModifier(P1, "searing");
            sim.Step(NoInput);
            Assert.That(sim.GetPlayer(P1).ActiveModifiers, Is.Empty);
            Assert.That(sim.Events.OfType<ModifierRevoked>().Single().ModifierId, Is.EqualTo("searing"));
        }

        [Test]
        public void BurstsSkipEnemiesTheSkillAlreadyHitDirectly()
        {
            var sim = World(EmberCore);
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
            sim.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 2f));
            sim.SpawnEnemy(Dummy(), new Vec3(3f, 0f, 1f));

            var events = RunTo(sim, 1, _ => UseSlot(3)); // Flame Burst hits all three directly

            Assert.That(events.OfType<DamageDealt>().Count(d => d.IsSecondary), Is.EqualTo(0));
            Assert.That(events.OfType<DamageDealt>().Count(d => !d.IsSecondary), Is.EqualTo(3));
        }

        [Test]
        public void AnEnemyTakesAtMostOneBurstPerSkillUse()
        {
            var sim = World(SplitShot, EmberCore);
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 5f));
            sim.SpawnEnemy(Dummy(), RotatedAhead(30f, 5f));
            var between = sim.SpawnEnemy(Dummy(), new Vec3(1.25f, 0f, 4.67f)); // within 2 m of both, off every bolt's path

            var events = RunTo(sim, 40, t => t == 1 ? UseSlot(2) : NoInput);

            Assert.That(events.OfType<DamageDealt>().Count(d => d.Target == between), Is.EqualTo(1));
            Assert.That(sim.GetEnemy(between).Health, Is.EqualTo(987));
        }

        [Test]
        public void BurstUsesTheFullHitEvenOnAKillingBlow()
        {
            var sim = World(EmberCore);
            sim.SpawnEnemy(new EnemyDefinition { Name = "Frail", MaxHealth = 5, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f }, new Vec3(0f, 0f, 5f));
            var neighbour = sim.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 5.5f));

            RunTo(sim, 40, t => t == 1 ? UseSlot(2) : NoInput);

            Assert.That(sim.GetEnemy(neighbour).Health, Is.EqualTo(987), "half of the 25 hit, not of the 5 health it removed");
        }

        [Test]
        public void SkillUsedReportsTheModifiedShape()
        {
            var wide = new ModifierDefinition { Id = "wide", Name = "Wide", AppliesToTag = "area", RadiusAdd = 1.5f };
            var sim = World(wide);

            var events = RunTo(sim, 1, _ => UseSlot(1));

            Assert.That(events.OfType<SkillUsed>().Single().Radius, Is.EqualTo(5f), "Ground Slam 3.5 m + 1.5 m");
        }

        private static Vec3 RotatedAhead(float degrees, float distance)
        {
            var radians = degrees * MathF.PI / 180f;
            return new Vec3(MathF.Sin(radians) * distance, 0f, MathF.Cos(radians) * distance);
        }
    }
}
