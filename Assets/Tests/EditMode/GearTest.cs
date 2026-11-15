using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Gear (ticket 23). A character keeps every piece it finds in its bag and wears one per slot
    /// (weapon, head, body, hands, feet, trinket). A worn piece's modifiers work like the player's
    /// own, and its tags count for modifiers that scale with worn gear. That is where the broken
    /// builds come from: the Cinder set makes a level-1 Fireball tear through a pack.
    /// </summary>
    public class GearTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static readonly SkillDefinition Bolt = new SkillDefinition
        {
            Id = "fire-bolt", Name = "Fire Bolt", Effect = SkillEffectKind.Projectile, Damage = 20, Range = 12f, Radius = 0.5f,
            ProjectileSpeed = 15f, CooldownSeconds = 2f, Tags = new[] { "fire", "projectile", "magic" },
        };

        private static readonly SkillDefinition Slash = new SkillDefinition
        {
            Id = "slash", Name = "Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
            CooldownSeconds = 4f, Tags = new[] { "melee", "physical" },
        };

        private static GearDefinition Piece(string id, GearSlot slot, string[] tags, params ModifierDefinition[] modifiers)
        {
            var gear = new GearDefinition { Id = id, Name = id, Slot = slot, Tags = tags };
            gear.Modifiers.AddRange(modifiers);
            return gear;
        }

        /// <summary>+100% fire damage: easy to read in a hit.</summary>
        private static readonly GearDefinition Staff = Piece("staff", GearSlot.Weapon, new[] { "fire" },
            new ModifierDefinition { Id = "staff-heat", Name = "Heat", AppliesToTag = "fire", DamagePercent = 100 });

        private static readonly GearDefinition Cleaver = Piece("cleaver", GearSlot.Weapon, new[] { "melee", "physical" },
            new ModifierDefinition { Id = "cleaver-edge", Name = "Edge", AppliesToTag = "melee", DamagePercent = 50 });

        /// <summary>+10% fire damage for every worn fire piece.</summary>
        private static readonly GearDefinition Hood = Piece("hood", GearSlot.Head, new[] { "fire" },
            new ModifierDefinition { Id = "hood-kindling", Name = "Kindling", AppliesToTag = "fire", PerWornTag = "fire", DamagePercent = 10 });

        private static readonly GearDefinition Boots = Piece("boots", GearSlot.Feet, new[] { "fire" });
        private static readonly GearDefinition Ring = Piece("ring", GearSlot.Trinket, new[] { "fire" });
        private static readonly GearDefinition PlainRing = Piece("plain-ring", GearSlot.Trinket, new[] { "physical" });

        private static readonly ModifierDefinition Ember = new ModifierDefinition { Id = "ember", Name = "Ember", AppliesToTag = "fire", DamagePercent = 10 };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMaxHealth = 1000,
            SoftLockOn = new SoftLockOnDefinition { Enabled = false },
            Skills = new[] { Bolt, Slash },
            Gear = new[] { Staff, Cleaver, Hood, Boots, Ring, PlainRing },
            Gates = new[]
            {
                new GateDefinition
                {
                    Id = "den", Name = "Den",
                    Rooms =
                    {
                        new RoomDefinition { Width = 10f, Length = 10f },
                        new RoomDefinition
                        {
                            Width = 10f, Length = 10f,
                            Spawns = { new RoomSpawn(new EnemyDefinition { Name = "Chief", MaxHealth = 10, Boss = new BossDefinition { Attacks = { new BossAttackDefinition { Kind = BossAttackKind.Slam, Damage = 1, WindupSeconds = 1f, CooldownSeconds = 2f, Radius = 1f } } } }, Vec3.Zero) },
                        },
                    },
                    RewardPool = { Staff, Ember },
                },
            },
        };

        private static EnemyDefinition Dummy() => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 100000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, PlayerCommand? command = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                if (command.HasValue && i == 0) commands[P1] = command.Value;
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        private static List<SimulationEvent> Equip(SimulationWorld world, string gearId) => Step(world, 1, new PlayerCommand(0f, 0f, equipGearId: gearId));

        private static SimulationWorld WorldWith(params GearDefinition[] bag)
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero);
            foreach (var gear in bag) world.GiveGear(P1, gear.Id);
            Step(world);
            return world;
        }

        /// <summary>The first direct hit of the skill in <paramref name="slot"/> on a dummy 3 m ahead.</summary>
        private static int HitWith(SimulationWorld world, int slot)
        {
            world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 3f));
            var events = Step(world, 40, new PlayerCommand(0f, 0f, useSkillSlot: slot));
            return events.OfType<DamageDealt>().First(d => !d.IsSecondary).Amount;
        }

        // --- The bag and the slots ---------------------------------------------------------

        [Test]
        public void APieceFromTheBagIsWornInItsOwnSlot()
        {
            var world = WorldWith(Staff, Hood);

            var events = Equip(world, "hood");

            var me = world.GetPlayer(P1);
            Assert.That(me.Worn[(int)GearSlot.Head], Is.SameAs(Hood));
            Assert.That(me.Worn.Count(g => g != null), Is.EqualTo(1));
            Assert.That(events.OfType<GearEquipped>().Single().Slot, Is.EqualTo(GearSlot.Head));
            Assert.That(me.Bag, Is.EqualTo(new[] { Staff, Hood }), "worn pieces stay in the bag");
        }

        [Test]
        public void WearingAnotherPieceInASlotPutsTheFirstBackInTheBag()
        {
            var world = WorldWith(Staff, Cleaver);
            Equip(world, "staff");

            var events = Equip(world, "cleaver");

            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Cleaver));
            Assert.That(events.OfType<GearUnequipped>().Single().GearId, Is.EqualTo("staff"));
            Assert.That(world.GetPlayer(P1).Bag, Has.Member(Staff));
        }

        [Test]
        public void TakingAPieceOffEmptiesItsSlot()
        {
            var world = WorldWith(Staff);
            Equip(world, "staff");

            var events = Step(world, 1, new PlayerCommand(0f, 0f, unequipSlot: GearSlot.Weapon));

            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.Null);
            Assert.That(events.OfType<GearUnequipped>().Single().Slot, Is.EqualTo(GearSlot.Weapon));
        }

        [Test]
        public void GearNotInTheBagCannotBeWorn()
        {
            var world = WorldWith(Hood);

            var events = Equip(world, "staff");

            Assert.That(world.GetPlayer(P1).Worn, Is.All.Null);
            Assert.That(events.OfType<GearEquipped>(), Is.Empty);
        }

        [Test]
        public void ADefeatedPlayerCannotChangeGear()
        {
            var world = WorldWith(Staff);
            world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Brute", MaxHealth = 100, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 100000, AttackRange = 3f,
                AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 1f,
            }, new Vec3(0f, 0f, 1f));
            Step(world, 60);
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.True);

            Equip(world, "staff");

            Assert.That(world.GetPlayer(P1).Worn, Is.All.Null);
        }

        // --- Gear and skills -----------------------------------------------------------------

        [Test]
        public void WornGearChangesSkillsWithItsTagsAndOnlyThose()
        {
            var bare = WorldWith(Staff);
            Assert.That(HitWith(bare, 0), Is.EqualTo(20), "in the bag, not worn: no effect");

            var worn = WorldWith(Staff);
            Equip(worn, "staff");
            Assert.That(HitWith(worn, 0), Is.EqualTo(40), "worn: Fire Bolt +100%");

            var melee = WorldWith(Staff);
            Equip(melee, "staff");
            Assert.That(HitWith(melee, 1), Is.EqualTo(30), "Slash is not fire");
        }

        [Test]
        public void TakingGearOffTakesItsEffectAway()
        {
            var world = WorldWith(Staff);
            Equip(world, "staff");
            Step(world, 1, new PlayerCommand(0f, 0f, unequipSlot: GearSlot.Weapon));

            Assert.That(HitWith(world, 0), Is.EqualTo(20));
        }

        [Test]
        public void AModifierThatCountsWornGearGrowsWithEveryMatchingPiece()
        {
            var one = WorldWith(Hood);
            Equip(one, "hood");
            Assert.That(HitWith(one, 0), Is.EqualTo(22), "the hood itself is fire: +10%");

            var three = WorldWith(Hood, Boots, Ring);
            foreach (var id in new[] { "hood", "boots", "ring" }) Equip(three, id);
            Assert.That(HitWith(three, 0), Is.EqualTo(26), "three fire pieces: +30%");

            var mixed = WorldWith(Hood, Boots, PlainRing);
            foreach (var id in new[] { "hood", "boots", "plain-ring" }) Equip(mixed, id);
            Assert.That(HitWith(mixed, 0), Is.EqualTo(24), "the plain ring is not fire: +20%");
        }

        [Test]
        public void APoolModifierCanCountWornGearToo()
        {
            var world = WorldWith(Hood, Boots);
            foreach (var id in new[] { "hood", "boots" }) Equip(world, id);
            world.GrantModifier(P1, new ModifierDefinition { Id = "pyre", Name = "Pyre", AppliesToTag = "fire", PerWornTag = "fire", DamagePercent = 25 });
            Step(world);

            Assert.That(HitWith(world, 0), Is.EqualTo(34), "hood +20%, pyre +50%: 20 at +70%");
        }

        [Test]
        public void AModifierCountingAnUnknownTagIsRejected()
        {
            var world = WorldWith();
            Assert.Throws<ArgumentException>(() =>
                world.GrantModifier(P1, new ModifierDefinition { Id = "x", Name = "X", AppliesToTag = "fire", PerWornTag = "fyre" }));
        }

        [Test]
        public void GearWithAnUnknownTagOrABrokenModifierIsRejected()
        {
            var badTag = Config();
            badTag.Gear = new[] { Piece("bad", GearSlot.Head, new[] { "fyre" }) };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(badTag));

            var badModifier = Config();
            badModifier.Gear = new[] { Piece("bad", GearSlot.Head, new[] { "fire" }, new ModifierDefinition { Id = "m", Name = "M", AppliesToSkillId = "nope" }) };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(badModifier));
        }

        [Test]
        public void AGateCanOnlyDropTheGamesOwnGear()
        {
            var config = Config();
            config.Gates[0].RewardPool.Add(Piece("stray", GearSlot.Feet, new[] { "fire" }));

            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
        }

        // --- Gear from a gate ------------------------------------------------------------------

        /// <summary>Clears the den with the given player once and returns that tick's events.</summary>
        private static List<SimulationEvent> Clear(SimulationWorld world)
        {
            world.StartGate("den");
            world.PlacePlayer(P1, new Vec3(0f, 0f, 10f)); // into the boss room
            Step(world);
            world.DamageEnemy(world.EnemyIds.Single(), P1, 100);
            return Step(world);
        }

        [Test]
        public void GearFromAClearGoesInTheBagAndOnIfItsSlotIsEmpty()
        {
            var world = WorldWith();
            world.GrantModifier(P1, Ember); // owns the rest of the pool, so the staff is the draw
            Step(world);

            var events = Clear(world);

            var reward = events.OfType<GateRewardGranted>().Single();
            Assert.That(reward.Gear, Is.SameAs(Staff));
            Assert.That(reward.IsDuplicate, Is.False);
            Assert.That(world.GetPlayer(P1).Bag, Is.EqualTo(new[] { Staff }));
            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Staff), "the weapon slot was empty");
            Assert.That(events.OfType<GearEquipped>().Single().GearId, Is.EqualTo("staff"));
        }

        [Test]
        public void GearFromAClearWaitsInTheBagWhenItsSlotIsTaken()
        {
            var world = WorldWith(Cleaver);
            Equip(world, "cleaver");
            world.GrantModifier(P1, Ember);
            Step(world);

            Clear(world);

            Assert.That(world.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Cleaver));
            Assert.That(world.GetPlayer(P1).Bag, Has.Member(Staff));
        }

        [Test]
        public void GearOwnedCountsForTheNoRepeatsRuleAndThenDuplicates()
        {
            var hasStaff = WorldWith(Staff);
            var first = Clear(hasStaff).OfType<GateRewardGranted>().Single();
            Assert.That(first.Modifier, Is.SameAs(Ember), "already has the staff: the only thing left is the modifier");

            var hasBoth = WorldWith(Staff);
            hasBoth.GrantModifier(P1, Ember);
            Step(hasBoth);
            var second = Clear(hasBoth).OfType<GateRewardGranted>().Single();
            Assert.That(second.IsDuplicate, Is.True);
            Assert.That(hasBoth.GetPlayer(P1).Bag.Count(g => g == Staff), Is.EqualTo(1), "a duplicate is kept to sell, not put in the bag");
            if (second.Gear != null) Assert.That(hasBoth.GetPlayer(P1).Duplicates, Is.EqualTo(new LootDefinition[] { Staff }));
        }

        // --- Saves, snapshots, the wire ----------------------------------------------------

        [Test]
        public void GearIsSavedWithTheCharacterAndComesBackWorn()
        {
            var world = WorldWith(Staff, Hood, Cleaver);
            Equip(world, "staff");
            Equip(world, "hood");

            var bytes = CharacterCodec.Encode(world.CaptureCharacter(P1));
            var back = CharacterCodec.Decode(bytes);
            var next = new SimulationWorld(Config());
            next.AddPlayer(P1, Vec3.Zero, back);

            Assert.That(back.Gear, Is.EqualTo(new[] { "staff", "hood", "cleaver" }));
            Assert.That(next.GetPlayer(P1).Bag, Is.EqualTo(new[] { Staff, Hood, Cleaver }));
            Assert.That(next.GetPlayer(P1).Worn[(int)GearSlot.Weapon], Is.SameAs(Staff));
            Assert.That(next.GetPlayer(P1).Worn[(int)GearSlot.Head], Is.SameAs(Hood));
        }

        [Test]
        public void ModifiersThatCountWornGearKeepThatInTheSave()
        {
            var record = new CharacterRecord();
            record.Modifiers.Add(new ModifierDefinition { Id = "pyre", Name = "Pyre", AppliesToTag = "fire", PerWornTag = "fire", DamagePercent = 25 });

            var back = CharacterCodec.Decode(CharacterCodec.Encode(record));

            Assert.That(back.Modifiers.Single().PerWornTag, Is.EqualTo("fire"));
        }

        [Test]
        public void ASaveFromBeforeGearLoadsWithAnEmptyBag()
        {
            var old = new CharacterRecord { Level = 3 };
            old.Modifiers.Add(Ember);
            old.Duplicates.Add("ember");

            var back = CharacterCodec.Decode(CharacterCodec.EncodeVersion3ForTests(old));

            Assert.That(back.Level, Is.EqualTo(3));
            Assert.That(back.Modifiers.Single().Id, Is.EqualTo("ember"));
            Assert.That(back.Modifiers.Single().PerWornTag, Is.Null);
            Assert.That(back.Duplicates, Is.EqualTo(new[] { "ember" }));
            Assert.That(back.Gear, Is.Empty);
            Assert.That(back.Worn, Is.All.Null);
        }

        [Test]
        public void GearTheGameNoLongerHasIsDroppedAndNothingIsWornInTheWrongSlot()
        {
            var record = new CharacterRecord();
            record.Gear.AddRange(new[] { "staff", "gone", "hood" });
            record.Worn[(int)GearSlot.Head] = "staff"; // a weapon on the head
            record.Worn[(int)GearSlot.Feet] = "gone";
            record.Worn[(int)GearSlot.Weapon] = "cleaver"; // not in the bag

            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, record);

            Assert.That(world.GetPlayer(P1).Bag, Is.EqualTo(new[] { Staff, Hood }));
            Assert.That(world.GetPlayer(P1).Worn, Is.All.Null);
        }

        [Test]
        public void SnapshotsCarryTheBagAndWhatIsWorn()
        {
            var world = WorldWith(Staff, Hood, Boots);
            Equip(world, "hood");
            Equip(world, "boots");

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(copy.GetPlayer(P1).Bag.Select(g => g.Id), Is.EqualTo(new[] { "staff", "hood", "boots" }));
            Assert.That(copy.GetPlayer(P1).Worn.Select(g => g?.Id), Is.EqualTo(world.GetPlayer(P1).Worn.Select(g => g?.Id)));
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()), "the same bytes after a round trip");
            Assert.That(HitWith(copy, 0), Is.EqualTo(HitWith(world, 0)), "and the same hits");
        }

        [Test]
        public void GearChangesCrossTheWire()
        {
            var (_, equip) = Wire.ReadCommand(Wire.Command(3, new PlayerCommand(0f, 0f, equipGearId: "staff")));
            var (_, unequip) = Wire.ReadCommand(Wire.Command(4, new PlayerCommand(0f, 0f, unequipSlot: GearSlot.Trinket)));
            var (_, none) = Wire.ReadCommand(Wire.Command(5, new PlayerCommand(0f, 0f)));

            Assert.That(equip.EquipGearId, Is.EqualTo("staff"));
            Assert.That(equip.UnequipSlot, Is.Null);
            Assert.That(unequip.UnequipSlot, Is.EqualTo(GearSlot.Trinket));
            Assert.That(none.EquipGearId, Is.Null);
            Assert.That(none.UnequipSlot, Is.Null);
        }

        // --- Bot teammates -----------------------------------------------------------------

        [Test]
        public void TeammatesWearWhatSuitsTheirRole()
        {
            var world = WorldWith(Staff, Cleaver, Ring, PlainRing);
            var me = world.GetPlayer(P1);

            Assert.That(CompanionRoles.GearToWear(CompanionRole.Tank, me), Is.EqualTo("cleaver"));
            Assert.That(CompanionRoles.GearToWear(CompanionRole.Caster, me), Is.EqualTo("staff"));
            Assert.That(CompanionRoles.GearToWear(CompanionRole.Marksman, me), Is.EqualTo("cleaver"), "physical suits it more than fire");
        }

        [Test]
        public void ATeammateSwapsOnlyForSomethingBetterAndThenSettles()
        {
            var world = WorldWith(Staff, Cleaver, Ring, PlainRing);
            var brain = new CompanionBrain(new CompanionRecord { Name = "Garrick", Character = new CharacterRecord { ClassId = "warrior" } }, seed: 1);

            for (var i = 0; i < 6; i++)
            {
                var command = brain.Next(world, P1, world.Events);
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = command });
            }

            var worn = world.GetPlayer(P1).Worn;
            Assert.That(worn[(int)GearSlot.Weapon], Is.SameAs(Cleaver), "the tank's melee weapon");
            Assert.That(worn[(int)GearSlot.Trinket], Is.SameAs(PlainRing), "physical over fire");
            Assert.That(CompanionRoles.GearToWear(CompanionRole.Tank, world.GetPlayer(P1)), Is.Null, "nothing left to change");
        }

        // --- The broken build ---------------------------------------------------------------

        /// <summary>
        /// The documented broken build, from the game's own content: a level-1 Wizard's Fireball with
        /// the six-piece Cinder set (Staff, Hood, Robe, Gloves, Boots and the Ember Heart). The staff
        /// fans three bolts, the robe and boots make every hit burst, and the hood, gloves and boots
        /// each count every fire piece worn. Against a pack, it does well over ten times the damage
        /// of the bare skill, and far more than the six pieces' separate gains added together.
        /// </summary>
        [Test]
        public void TheCinderSetMakesFireballTearThroughAPack()
        {
            var content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid))).Single();
            string[] cinder = { "cinder-staff", "cinder-hood", "cinder-robe", "cinder-gloves", "cinder-boots", "ember-heart" };

            int PackDamage(params string[] worn)
            {
                var config = new SimulationConfig();
                content.ApplyTo(config);
                config.Hub = null; // straight into a fight, as the arena does
                config.StoryScenes = Array.Empty<StorySceneDefinition>();
                var world = new SimulationWorld(config);
                world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "wizard" });
                foreach (var id in worn) world.GiveGear(P1, id, equip: true);
                Step(world);
                Assert.That(worn.All(id => world.GetPlayer(P1).Worn.Any(g => g?.Id == id)), Is.True, "every piece asked for is worn");

                // Six dummies packed 6 m ahead, the way goblins crowd a tank.
                foreach (var x in new[] { -1f, 0f, 1f })
                foreach (var z in new[] { 6f, 7f })
                    world.SpawnEnemy(Dummy(), new Vec3(x, 0f, z));

                var slot = Enumerable.Range(0, SimulationWorld.SkillSlotCount)
                    .Single(s => world.GetPlayer(P1).SkillSlots[s].Skill?.Id == "fire-bolt");
                var total = 0;
                for (var tick = 0; tick < 10 * 60; tick++) // ten seconds, pressing Fireball whenever it is ready
                {
                    world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, useSkillSlot: slot, aimYawDegrees: 0f) });
                    total += world.Events.OfType<DamageDealt>().Sum(d => d.Amount);
                }
                return total;
            }

            var bare = PackDamage();
            var full = PackDamage(cinder);
            var separateGains = cinder.Sum(piece => PackDamage(piece) - bare);
            TestContext.WriteLine($"Fireball into a pack of six for 10 s: bare {bare}, full Cinder set {full} ({(double)full / bare:0.0}x); " +
                                  $"the six pieces worn one at a time add {separateGains} between them, together {full - bare}.");

            Assert.That(full, Is.GreaterThan(bare * 10), "the set is broken: over ten times the bare skill");
            Assert.That(full - bare, Is.GreaterThan(separateGains * 3), "the pieces multiply each other, not just add up");
        }
    }
}
