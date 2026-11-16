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
    /// Gear that changes stats (ticket 51). A worn piece's stat bonuses count wherever the stat has an
    /// effect (damage scaling, max health, move speed) and stop when it comes off. They never touch the
    /// points the player spent or the saved stats, putting a piece on is never a free heal, and a minus
    /// always comes with an upside.
    /// </summary>
    public class GearStatsTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static readonly SkillDefinition Smash = new SkillDefinition
        {
            Id = "smash", Name = "Smash", Effect = SkillEffectKind.Cone, Damage = 100, Range = 4f, ArcDegrees = 160f,
            CooldownSeconds = 4f, Tags = new[] { "melee", "physical" }, ScalingStat = Stat.Strength,
        };

        private static GearDefinition Piece(string id, GearSlot slot, params (Stat stat, int amount)[] bonuses)
        {
            var gear = new GearDefinition { Id = id, Name = id, Slot = slot, Tags = Array.Empty<string>() };
            foreach (var (stat, amount) in bonuses) gear.StatBonuses.Add(new StatBonus(stat, amount));
            return gear;
        }

        private static GearDefinition WithUpside(GearDefinition gear)
        {
            gear.Modifiers.Add(new ModifierDefinition { Id = gear.Id + "-edge", Name = "Edge", AppliesToTag = "fire", DamagePercent = 10 });
            return gear;
        }

        /// <summary>+4 Vitality, -2 Agility, with a fire modifier as its upside.</summary>
        private static readonly GearDefinition Plate = WithUpside(Piece("plate", GearSlot.Body, (Stat.Vitality, 4), (Stat.Agility, -2)));

        private static readonly GearDefinition Helm = Piece("helm", GearSlot.Head, (Stat.Vitality, 2));
        private static readonly GearDefinition PlainHelm = Piece("plain-helm", GearSlot.Head);
        private static readonly GearDefinition Boots = Piece("boots", GearSlot.Feet, (Stat.Agility, 5));
        private static readonly GearDefinition Gauntlets = Piece("gauntlets", GearSlot.Hands, (Stat.Strength, 2));
        private static readonly GearDefinition Charm = Piece("charm", GearSlot.Trinket, (Stat.Sense, 2), (Stat.Luck, 1));

        /// <summary>Far below the start: the effects keep their floors.</summary>
        private static readonly GearDefinition Anchor = WithUpside(Piece("anchor", GearSlot.Feet, (Stat.Agility, -100), (Stat.Vitality, -500)));

        private static GearDefinition[] AllGear() => new[] { Plate, Helm, PlainHelm, Boots, Gauntlets, Charm, Anchor };

        private static SimulationConfig Config(GearDefinition[] gear = null) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMaxHealth = 1000,
            SoftLockOn = new SoftLockOnDefinition { Enabled = false },
            Skills = new[] { Smash },
            Gear = gear ?? AllGear(),
        };

        private static void Step(SimulationWorld world, int ticks = 1, PlayerCommand? command = null, bool everyTick = false)
        {
            for (var i = 0; i < ticks; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                if (command.HasValue && (everyTick || i == 0)) commands[P1] = command.Value;
                world.Step(commands);
            }
        }

        private static void Equip(SimulationWorld world, string gearId) => Step(world, 1, new PlayerCommand(0f, 0f, equipGearId: gearId));
        private static void Unequip(SimulationWorld world, GearSlot slot) => Step(world, 1, new PlayerCommand(0f, 0f, unequipSlot: slot));

        private static SimulationWorld World()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero);
            foreach (var gear in AllGear()) world.GiveGear(P1, gear.Id);
            Step(world);
            return world;
        }

        private static PlayerState Me(SimulationWorld world) => world.GetPlayer(P1);

        private static float DistanceWalked(SimulationWorld world)
        {
            var from = Me(world).Position;
            Step(world, 60, new PlayerCommand(1f, 0f), everyTick: true);
            return (Me(world).Position - from).Length;
        }

        private static int SmashDamage(SimulationWorld world)
        {
            world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Dummy", MaxHealth = 100000, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
            }, Me(world).Position + new Vec3(0f, 0f, 2f));
            var hits = new List<DamageDealt>();
            for (var i = 0; i < 40; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                if (i == 0) commands[P1] = new PlayerCommand(0f, 0f, useSkillSlot: 0);
                world.Step(commands);
                hits.AddRange(world.Events.OfType<DamageDealt>());
            }
            return hits.First(d => !d.IsSecondary).Amount;
        }

        // --- The effective stat ------------------------------------------------------------

        [Test]
        public void WearingAPieceAddsItsBonusesAndTakingItOffRemovesThem()
        {
            var world = World();
            var start = Me(world).Stats[Stat.Vitality];

            Equip(world, "plate");
            Assert.That(Me(world).GearStats[Stat.Vitality], Is.EqualTo(4));
            Assert.That(Me(world).GearStats[Stat.Agility], Is.EqualTo(-2));
            Assert.That(Me(world).TotalStat(Stat.Vitality), Is.EqualTo(start + 4));

            Unequip(world, GearSlot.Body);
            Assert.That(Me(world).GearStats[Stat.Vitality], Is.EqualTo(0));
            Assert.That(Me(world).TotalStat(Stat.Vitality), Is.EqualTo(start));
        }

        [Test]
        public void BonusesFromSeveralPiecesAddUp()
        {
            var world = World();
            Equip(world, "plate");
            Equip(world, "helm");

            Assert.That(Me(world).GearStats[Stat.Vitality], Is.EqualTo(6));
        }

        [Test]
        public void VitalityFromGearRaisesMaxHealthAndTakingItOffLowersIt()
        {
            var world = World();
            Assert.That(Me(world).MaxHealth, Is.EqualTo(1000));

            Equip(world, "plate");
            Assert.That(Me(world).MaxHealth, Is.EqualTo(1040), "+4 Vitality at 10 health each");

            Unequip(world, GearSlot.Body);
            Assert.That(Me(world).MaxHealth, Is.EqualTo(1000));
        }

        [Test]
        public void AgilityFromGearMakesThePlayerFaster()
        {
            var bare = DistanceWalked(World());
            var fast = World();
            Equip(fast, "boots");

            Assert.That(DistanceWalked(fast) / bare, Is.EqualTo(1.10f).Within(0.01f), "+5 Agility at 2% each");
        }

        [Test]
        public void StrengthFromGearRaisesStrengthDamage()
        {
            var bare = SmashDamage(World());
            var strong = World();
            Equip(strong, "gauntlets");

            Assert.That(bare, Is.EqualTo(100));
            Assert.That(SmashDamage(strong), Is.EqualTo(110), "+2 Strength at 5% each");
        }

        [Test]
        public void AStatFarBelowItsStartKeepsSensibleFloors()
        {
            var world = World();
            Equip(world, "anchor");

            Assert.That(Me(world).MaxHealth, Is.EqualTo(1), "max health never below 1");
            Assert.That(DistanceWalked(world), Is.GreaterThan(0.1f), "still moves");
        }

        [Test]
        public void SenseAndLuckBonusesShowThoughTheyDoNothingYet()
        {
            var world = World();
            Equip(world, "charm");

            Assert.That(Me(world).GearStats[Stat.Sense], Is.EqualTo(2));
            Assert.That(Me(world).GearStats[Stat.Luck], Is.EqualTo(1));
            Assert.That(GearText.Effects(Charm), Does.Contain("+2 Sense (no effect yet)"));
        }

        // --- Never a free heal ---------------------------------------------------------------

        [Test]
        public void PuttingOnVitalityKeepsCurrentHealth()
        {
            var world = World();

            Equip(world, "plate");

            Assert.That(Me(world).Health, Is.EqualTo(1000), "the new max arrives empty");
            Assert.That(Me(world).MaxHealth, Is.EqualTo(1040));
        }

        [Test]
        public void TakingOffVitalityCutsHealthToTheNewMax()
        {
            var world = World();
            Equip(world, "plate");
            world.ReplaceCharacter(P1, world.CaptureCharacter(P1)); // arrives full, at 1040, wearing the plate
            Assert.That(Me(world).Health, Is.EqualTo(1040));

            Unequip(world, GearSlot.Body);

            Assert.That(Me(world).Health, Is.EqualTo(1000));
            Assert.That(Me(world).MaxHealth, Is.EqualTo(1000));
        }

        [Test]
        public void SwappingAVitalityPieceOnAndOffNeverHeals()
        {
            var world = World();
            Equip(world, "plate");
            world.ReplaceCharacter(P1, world.CaptureCharacter(P1));
            Unequip(world, GearSlot.Body); // down to 1000 of 1000

            for (var i = 0; i < 3; i++)
            {
                Equip(world, "plate");
                Assert.That(Me(world).Health, Is.EqualTo(1000));
                Unequip(world, GearSlot.Body);
                Assert.That(Me(world).Health, Is.EqualTo(1000));
            }
        }

        // --- Spent points and the save --------------------------------------------------------

        [Test]
        public void GearNeverChangesSpentPointsOrTheSavedStats()
        {
            var world = World();
            var before = world.CaptureCharacter(P1);
            var free = Me(world).UnspentStatPoints;

            Equip(world, "plate");
            Equip(world, "boots");

            Assert.That(Me(world).UnspentStatPoints, Is.EqualTo(free));
            Assert.That(Me(world).Stats[Stat.Vitality], Is.EqualTo(before.Stats[(int)Stat.Vitality]), "own stats untouched");
            Assert.That(world.CaptureCharacter(P1).Stats, Is.EqualTo(before.Stats), "the save holds only the character's own stats");
        }

        [Test]
        public void ALoadedCharacterWearingAVitalityPieceHasItsMaxHealth()
        {
            var world = World();
            Equip(world, "plate");
            var saved = world.CaptureCharacter(P1);

            var other = new SimulationWorld(Config());
            other.AddPlayer(P1, Vec3.Zero, saved);
            Step(other);

            Assert.That(other.GetPlayer(P1).MaxHealth, Is.EqualTo(1040));
            Assert.That(other.GetPlayer(P1).GearStats[Stat.Vitality], Is.EqualTo(4));
        }

        // --- Host and guest --------------------------------------------------------------------

        [Test]
        public void ASnapshotRoundTripGivesTheSameStatsOnBothSides()
        {
            var world = World();
            Equip(world, "plate");
            Equip(world, "boots");

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            var host = Me(world);
            var guest = copy.GetPlayer(P1);
            foreach (Stat stat in Enum.GetValues(typeof(Stat)))
                Assert.That(guest.TotalStat(stat), Is.EqualTo(host.TotalStat(stat)), stat.ToString());
            Assert.That(guest.MaxHealth, Is.EqualTo(host.MaxHealth));
            Assert.That(guest.Health, Is.EqualTo(host.Health));
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()), "the same bytes after a round trip");
        }

        // --- Content rules -----------------------------------------------------------------------

        [Test]
        public void APieceWhoseStatsAddUpToALossWithNothingElseIsRefused()
        {
            var bad = Piece("millstone", GearSlot.Body, (Stat.Agility, -3), (Stat.Vitality, 1));

            var e = Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(new[] { bad })));
            Assert.That(e.Message, Does.Contain("millstone").And.Contain("loss"));
        }

        [Test]
        public void APieceWithAMinusAndAnUpsideIsAllowed()
        {
            Assert.DoesNotThrow(() => new SimulationWorld(Config(new[] { Plate })));
        }

        [Test]
        public void TwoBonusesToOneStatOrAZeroBonusAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(new[] { Piece("twice", GearSlot.Head, (Stat.Luck, 1), (Stat.Luck, 2)) })));
            Assert.Throws<ArgumentException>(() => new SimulationWorld(Config(new[] { Piece("zero", GearSlot.Head, (Stat.Luck, 0)) })));
        }

        [Test]
        public void SomeArmourInTheGameHasStatBonuses()
        {
            var plate = AssetDatabase.LoadAssetAtPath<GearDefinitionAsset>("Assets/Data/Gear/WarlordsPlate.asset");
            var robe = AssetDatabase.LoadAssetAtPath<GearDefinitionAsset>("Assets/Data/Gear/CinderRobe.asset");

            Assert.That(plate.ToDefinition(null).BonusTo(Stat.Vitality), Is.GreaterThan(0));
            Assert.That(robe.ToDefinition(null).BonusTo(Stat.Intelligence), Is.GreaterThan(0));
        }

        // --- The gear screen ----------------------------------------------------------------------

        [Test]
        public void TheGearScreenListsStatBonusesInPlainWords()
        {
            var lines = GearText.Effects(Plate);

            Assert.That(lines, Does.Contain("+4 Vitality"));
            Assert.That(lines, Does.Contain("-2 Agility"));
        }

        [Test]
        public void TheCompareViewShowsStatGainsAndLosses()
        {
            var lines = GearText.Compare(Plate, WithUpside(Piece("robe", GearSlot.Body, (Stat.Vitality, 1))));

            Assert.That(lines, Does.Contain("+ 3 Vitality"));
            Assert.That(lines, Does.Contain("− 2 Agility"));
        }

        [Test]
        public void AnEmptySlotWarnsOfAStatLoss()
        {
            var lines = GearText.Compare(Plate, null);

            Assert.That(lines[0], Does.Contain("empty").And.Not.Contain("only gain"));
            Assert.That(lines, Does.Contain("− 2 Agility"));
            Assert.That(GearText.Compare(Helm, null)[0], Does.Contain("only gain"));
        }

        // --- Bots ----------------------------------------------------------------------------------

        [Test]
        public void ATankPrefersAPieceWithVitality()
        {
            var world = World();
            Equip(world, "plain-helm");

            Assert.That(CompanionRoles.GearToWear(CompanionRole.Tank, Me(world)), Is.EqualTo("helm"));
        }

        [Test]
        public void ABotLeavesASlotEmptyRatherThanWearAPieceThatCostsItsRole()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero);
            world.GiveGear(P1, "plate"); // -2 Agility: the Rogue's stat; its upside is fire, not the Rogue's
            Step(world);

            Assert.That(CompanionRoles.GearToWear(CompanionRole.Skirmisher, Me(world)), Is.Null);
            Assert.That(CompanionRoles.GearToWear(CompanionRole.Tank, Me(world)), Is.EqualTo("plate"), "+4 Vitality outweighs it for the tank");
        }
    }
}
