using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Weapons (ticket 46). The weapon a player wears decides what their light and heavy attacks do:
    /// the sword slashes wide and thrusts far, the bow shoots arrows and a drawn shot that pierces a
    /// line, the staff throws bolts and blasts all round. Without one, the default attacks stay.
    /// </summary>
    public class WeaponTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static WeaponAttack Attack(string id, SkillEffectKind effect, int damage, float cooldown, float range = 2.5f, float arc = 120f,
            float radius = 0.5f, float speed = 20f, bool pierces = false, float windup = 0f, Stat stat = Stat.Strength, params string[] tags) => new WeaponAttack
        {
            WindupSeconds = windup,
            Hit = new SkillDefinition
            {
                Id = id, Name = id, Effect = effect, Damage = damage, CooldownSeconds = cooldown, Range = range, ArcDegrees = arc,
                Radius = radius, ProjectileSpeed = speed, Pierces = pierces, ScalingStat = stat, Tags = tags,
            },
        };

        private static readonly GearDefinition Sword = new GearDefinition
        {
            Id = "sword", Name = "Sword", Slot = GearSlot.Weapon, Tags = new[] { "melee" },
            LightAttack = Attack("sword-slash", SkillEffectKind.Cone, 12, 0.35f, range: 2.4f, arc: 150f, tags: new[] { "melee", "physical" }),
            HeavyAttack = Attack("sword-thrust", SkillEffectKind.Cone, 30, 0.9f, range: 3.6f, arc: 35f, windup: 0.3f, tags: new[] { "melee", "physical" }),
        };

        private static readonly GearDefinition Bow = new GearDefinition
        {
            Id = "bow", Name = "Bow", Slot = GearSlot.Weapon, Tags = new[] { "projectile" },
            LightAttack = Attack("bow-shot", SkillEffectKind.Projectile, 9, 0.45f, range: 16f, radius: 0.35f, speed: 30f, stat: Stat.Agility, tags: new[] { "projectile", "physical" }),
            HeavyAttack = Attack("bow-drawn-shot", SkillEffectKind.Projectile, 26, 1.2f, range: 22f, radius: 0.45f, speed: 40f, pierces: true, windup: 0.5f, stat: Stat.Agility, tags: new[] { "projectile", "physical" }),
        };

        private static readonly GearDefinition Staff = new GearDefinition
        {
            Id = "staff", Name = "Staff", Slot = GearSlot.Weapon, Tags = new[] { "magic" },
            LightAttack = Attack("staff-bolt", SkillEffectKind.Projectile, 11, 0.55f, range: 11f, radius: 0.5f, speed: 16f, stat: Stat.Intelligence, tags: new[] { "magic", "projectile" }),
            HeavyAttack = Attack("staff-blast", SkillEffectKind.Area, 22, 1.6f, radius: 3.2f, windup: 0.4f, stat: Stat.Intelligence, tags: new[] { "magic", "area" }),
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMaxHealth = 1000,
            SoftLockOn = new SoftLockOnDefinition { Enabled = false }, // exact aim, so geometry is predictable
            Gear = new[] { Sword, Bow, Staff },
            Classes = new[]
            {
                new ClassDefinition { Id = "warrior", Name = "Warrior", StartingWeaponId = "sword" },
                new ClassDefinition { Id = "archer", Name = "Archer", StartingWeaponId = "bow" },
                new ClassDefinition { Id = "rogue", Name = "Rogue" },
            },
        };

        private static EnemyDefinition Dummy() => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 1000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static SimulationWorld Wielding(GearDefinition weapon)
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero);
            if (weapon != null) world.GiveGear(P1, weapon.Id, equip: true);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            return world;
        }

        /// <summary>Presses the attack once (facing +Z), then lets two seconds pass. Returns everything that happened.</summary>
        private static List<SimulationEvent> Press(SimulationWorld world, bool heavy = false, PlayerCommand? press = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < 120; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                if (i == 0) commands[P1] = press ?? new PlayerCommand(0f, 0f, lightAttack: !heavy, heavyAttack: heavy);
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        private static int Damage(SimulationWorld world, EnemyId enemy) => 1000 - world.GetEnemy(enemy).Health;

        // --- The sword --------------------------------------------------------------------

        [Test]
        public void TheSwordSlashesWideButShort()
        {
            var world = Wielding(Sword);
            var ahead = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
            var beside = world.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 0.8f)); // well off to the side, inside 150 degrees
            var far = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 3.2f));

            var events = Press(world);

            Assert.That(Damage(world, ahead), Is.EqualTo(12));
            Assert.That(Damage(world, beside), Is.EqualTo(12));
            Assert.That(Damage(world, far), Is.Zero, "past the slash's 2.4 m");
            Assert.That(events.OfType<AttackPerformed>().Single().Weapon.Id, Is.EqualTo("sword-slash"));
        }

        [Test]
        public void TheSwordThrustReachesFarButNarrowAfterAWindup()
        {
            var world = Wielding(Sword);
            var far = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 3.2f));
            var beside = world.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 0.8f));

            var events = Press(world, heavy: true);

            Assert.That(Damage(world, far), Is.EqualTo(30));
            Assert.That(Damage(world, beside), Is.Zero, "outside the thrust's narrow line");
            var windup = events.OfType<AttackWindupStarted>().Single();
            Assert.That(events.OfType<AttackPerformed>().Single().Tick, Is.EqualTo(windup.LandsAtTick));
            Assert.That(windup.LandsAtTick - windup.Tick, Is.EqualTo(18), "0.3 s at 60 ticks a second");
        }

        // --- The bow --------------------------------------------------------------------

        [Test]
        public void TheBowShootsAnArrowThatHitsTheFirstEnemyFarAway()
        {
            var world = Wielding(Bow);
            var near = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 10f));
            var behind = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 12f));

            var events = Press(world);

            Assert.That(Damage(world, near), Is.EqualTo(9));
            Assert.That(Damage(world, behind), Is.Zero, "an arrow stops at the first enemy");
            Assert.That(events.OfType<ProjectileEnded>().Single().HitEnemy, Is.True);
        }

        [Test]
        public void TheDrawnShotPiercesAWholeLine()
        {
            var world = Wielding(Bow);
            var line = new[] { 4f, 8f, 12f, 16f }.Select(z => world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, z))).ToList();
            var aside = world.SpawnEnemy(Dummy(), new Vec3(3f, 0f, 8f));

            Press(world, heavy: true);

            Assert.That(line.Select(e => Damage(world, e)), Is.All.EqualTo(26), "each one in the line, once");
            Assert.That(Damage(world, aside), Is.Zero);
            Assert.That(world.Projectiles, Is.Empty, "it flies on to its full range and ends there");
        }

        // --- The staff ------------------------------------------------------------------

        [Test]
        public void TheStaffThrowsABoltAndBlastsAllRound()
        {
            var bolt = Wielding(Staff);
            var ahead = bolt.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 8f));
            Press(bolt);
            Assert.That(Damage(bolt, ahead), Is.EqualTo(11));

            var blast = Wielding(Staff);
            var around = new[] { new Vec3(0f, 0f, 2f), new Vec3(0f, 0f, -2f), new Vec3(2.5f, 0f, 0f) }.Select(at => blast.SpawnEnemy(Dummy(), at)).ToList();
            var outside = blast.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 4f));
            Press(blast, heavy: true);
            Assert.That(around.Select(e => Damage(blast, e)), Is.All.EqualTo(22), "in front, behind and beside");
            Assert.That(Damage(blast, outside), Is.Zero);
        }

        // --- All weapons ------------------------------------------------------------------

        [Test]
        public void WithoutAWeaponTheDefaultAttacksStay()
        {
            var world = Wielding(null);
            var ahead = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 1.5f));

            var events = Press(world);

            Assert.That(Damage(world, ahead), Is.EqualTo(10), "the default light attack");
            Assert.That(events.OfType<AttackPerformed>().Single().Weapon, Is.Null);
        }

        [Test]
        public void EachWeaponScalesWithItsOwnStat()
        {
            int Hit(GearDefinition weapon, Stat raised)
            {
                var world = Wielding(weapon);
                world.RaiseToLevel(P1, 2);
                world.Step(new Dictionary<PlayerId, PlayerCommand>());
                var points = world.GetPlayer(P1).UnspentStatPoints;
                for (var i = 0; i < points; i++)
                    world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, allocateStat: raised) });
                var enemy = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
                Press(world);
                return Damage(world, enemy);
            }

            Assert.That(Hit(Sword, Stat.Strength), Is.GreaterThan(Hit(Sword, Stat.Agility)), "the sword with Strength");
            Assert.That(Hit(Bow, Stat.Agility), Is.GreaterThan(Hit(Bow, Stat.Strength)), "the bow with Agility");
            Assert.That(Hit(Staff, Stat.Intelligence), Is.GreaterThan(Hit(Staff, Stat.Strength)), "the staff with Intelligence");
        }

        [Test]
        public void ModifiersReachWeaponAttacksByTag()
        {
            var world = Wielding(Sword);
            world.GrantModifier(P1, new ModifierDefinition { Id = "edge", Name = "Edge", AppliesToTag = "melee", DamagePercent = 50 });
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            var ahead = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));

            Press(world);

            Assert.That(Damage(world, ahead), Is.EqualTo(18), "12 at +50%");
        }

        [Test]
        public void ExtraProjectilesFanAWeaponsArrows()
        {
            var world = Wielding(Bow);
            world.GrantModifier(P1, new ModifierDefinition { Id = "split", Name = "Split", AppliesToTag = "projectile", ExtraProjectiles = 2, ProjectileSpreadDegrees = 20f });
            world.Step(new Dictionary<PlayerId, PlayerCommand>());

            world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, lightAttack: true) });

            Assert.That(world.Projectiles.Count, Is.EqualTo(3));
            Assert.That(world.Projectiles.Select(p => p.Skill.Id), Is.All.EqualTo("bow-shot"));
        }

        [Test]
        public void ADodgeCancelsAWeaponsWindup()
        {
            var world = Wielding(Sword);
            var far = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 3.2f));
            world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, heavyAttack: true) });

            var events = Press(world, press: new PlayerCommand(1f, 0f, dodge: true));

            Assert.That(events.OfType<AttackPerformed>(), Is.Empty);
            Assert.That(Damage(world, far), Is.Zero);
        }

        [Test]
        public void AnArrowInFlightSurvivesASnapshot()
        {
            var world = Wielding(Bow);
            var target = world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 12f));
            world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, lightAttack: true) });
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());
            Press(copy, press: PlayerCommand.None);

            Assert.That(1000 - copy.GetEnemy(target).Health, Is.EqualTo(9));
            Assert.That(copy.CaptureSnapshot(), Is.Not.Empty);
        }

        [Test]
        public void AWeaponAttackCrossesTheWireAsAnEvent()
        {
            var config = Config();
            var events = new SimulationEvent[] { new AttackPerformed(5, P1, AttackKind.Heavy, Bow.HeavyAttack.Hit, 22f, 0.45f) };

            var back = (AttackPerformed)SimulationEventCodec.Decode(SimulationEventCodec.Encode(events), config).Single();

            Assert.That(back.Weapon, Is.SameAs(Bow.HeavyAttack.Hit));
            Assert.That((back.Range, back.Radius), Is.EqualTo((22f, 0.45f)));
        }

        // --- Starting weapons -------------------------------------------------------------

        [Test]
        public void EachClassIsIssuedItsWeapon()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(new PlayerId(1), Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "warrior" });
            world.AddPlayer(new PlayerId(2), Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "archer" });
            world.AddPlayer(new PlayerId(3), Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "rogue" });

            Assert.That(world.GetPlayer(new PlayerId(1)).Worn[(int)GearSlot.Weapon], Is.SameAs(Sword));
            Assert.That(world.GetPlayer(new PlayerId(2)).Worn[(int)GearSlot.Weapon], Is.SameAs(Bow));
            Assert.That(world.GetPlayer(new PlayerId(3)).Worn[(int)GearSlot.Weapon], Is.Null, "the Rogue keeps the default attack for now");
        }

        [Test]
        public void AnExistingCharacterWithNoWeaponIsIssuedOneButAWeaponInTheBagStays()
        {
            var empty = new CharacterRecord { Level = 3, ClassId = "archer" };
            var choseNone = new CharacterRecord { Level = 3, ClassId = "archer" };
            choseNone.Gear.Add("staff"); // took its weapon off on purpose

            var world = new SimulationWorld(Config());
            world.AddPlayer(new PlayerId(1), Vec3.Zero, empty);
            world.AddPlayer(new PlayerId(2), Vec3.Zero, choseNone);

            Assert.That(world.GetPlayer(new PlayerId(1)).Worn[(int)GearSlot.Weapon], Is.SameAs(Bow));
            Assert.That(world.GetPlayer(new PlayerId(2)).Worn[(int)GearSlot.Weapon], Is.Null);
            Assert.That(world.GetPlayer(new PlayerId(2)).Bag, Is.EqualTo(new[] { Staff }));
        }

        [Test]
        public void ChangingClassInTheTestArenaWearsTheNewClassesWeapon()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "warrior" });

            world.ChangeClassForTesting(P1, "archer");

            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Bow));
            Assert.That(world.GetPlayer(P1).Bag, Is.EquivalentTo(new[] { Sword, Bow }), "the sword stays in the bag");

            world.ChangeClassForTesting(P1, "warrior");

            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Sword));
            Assert.That(world.GetPlayer(P1).Bag, Is.EquivalentTo(new[] { Sword, Bow }), "no second sword");
        }

        // --- Validation -------------------------------------------------------------------

        [Test]
        public void OnlyWeaponsHaveAttacksAndTheirIdsAreTheirOwn()
        {
            var helmet = Config();
            helmet.Gear = new[] { new GearDefinition { Id = "helm", Name = "Helm", Slot = GearSlot.Head, LightAttack = Sword.LightAttack } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(helmet));

            var clash = Config();
            clash.Skills = new[] { new SkillDefinition { Id = "sword-slash", Name = "Slash" } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(clash));

            var badStarter = Config();
            badStarter.Classes = new[] { new ClassDefinition { Id = "warrior", Name = "Warrior", StartingWeaponId = "nothing" } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(badStarter));
        }

        // --- Bot teammates ----------------------------------------------------------------

        [Test]
        public void AnArcherTeammateShootsFromRange()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "archer" });
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 9f));
            var brain = new CompanionBrain(new CompanionRecord { Name = "Wren", Character = new CharacterRecord { ClassId = "archer" } }, seed: 3);

            var arrows = 0;
            var closest = float.MaxValue;
            for (var i = 0; i < 600; i++)
            {
                var command = brain.Next(world, P1, world.Events);
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = command });
                arrows += world.Events.OfType<AttackPerformed>().Count(a => a.Weapon?.Id == "bow-shot");
                closest = Math.Min(closest, (world.GetEnemy(world.EnemyIds.Single()).Position - world.GetPlayer(P1).Position).Length);
            }

            Assert.That(arrows, Is.GreaterThan(3), "it shoots");
            Assert.That(closest, Is.GreaterThan(4f), "without walking up to melee range");
        }
    }
}
