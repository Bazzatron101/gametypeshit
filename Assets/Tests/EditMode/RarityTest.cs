using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// More gear (ticket 61): rarities that weigh the clear draw, gate rank and Luck that tip it
    /// toward rarer pieces, and weapons that belong to their class while any class wears any armour.
    /// </summary>
    public class RarityTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        private static GearDefinition Piece(string id, GearSlot slot, Rarity rarity, params string[] classIds) =>
            new GearDefinition { Id = id, Name = id, Slot = slot, Rarity = rarity, Tags = new[] { "physical" }, ClassIds = classIds };

        private static readonly GearDefinition Sword = Piece("sword", GearSlot.Weapon, Rarity.Rare, "warrior");
        private static readonly GearDefinition Staff = Piece("staff", GearSlot.Weapon, Rarity.Rare, "wizard");
        private static readonly GearDefinition Cap = Piece("cap", GearSlot.Head, Rarity.Common);
        private static readonly GearDefinition Crown = Piece("crown", GearSlot.Head, Rarity.Legendary);

        private static SimulationConfig Config(string gateRank, params GearDefinition[] pool)
        {
            var gate = new GateDefinition
            {
                Id = "den", Name = "Den", Rank = gateRank,
                Rooms =
                {
                    new RoomDefinition { Width = 10f, Length = 10f },
                    new RoomDefinition
                    {
                        Width = 10f, Length = 10f,
                        Spawns = { new RoomSpawn(new EnemyDefinition { Name = "Chief", MaxHealth = 10, Boss = new BossDefinition { Attacks = { new BossAttackDefinition { Kind = BossAttackKind.Slam, Damage = 1, WindupSeconds = 1f, CooldownSeconds = 2f, Radius = 1f } } } }, Vec3.Zero) },
                    },
                },
            };
            foreach (var piece in pool) gate.RewardPool.Add(piece);
            return new SimulationConfig
            {
                TickRate = 60,
                PlayerMaxHealth = 1000,
                SoftLockOn = new SoftLockOnDefinition { Enabled = false },
                Gear = new[] { Sword, Staff, Cap, Crown },
                Classes = new[] { new ClassDefinition { Id = "warrior", Name = "Warrior" }, new ClassDefinition { Id = "wizard", Name = "Wizard" } },
                Progression = new ProgressionDefinition
                {
                    LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
                    Ranks = new[] { new RankDefinition { Id = "E", FirstLevel = 1 }, new RankDefinition { Id = "D", FirstLevel = 3 }, new RankDefinition { Id = "C", FirstLevel = 5 }, new RankDefinition { Id = "B", FirstLevel = 7 } },
                },
                Gates = new[] { gate },
            };
        }

        private static SimulationWorld World(SimulationConfig config, string classId, params string[] bag)
        {
            var world = new SimulationWorld(config);
            var character = new CharacterRecord { ClassId = classId };
            character.Gear.AddRange(bag);
            world.AddPlayer(P1, Vec3.Zero, character);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            return world;
        }

        private static GateRewardGranted Clear(SimulationWorld world)
        {
            world.StartGate("den");
            world.PlacePlayer(P1, new Vec3(0f, 0f, 10f));
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            world.DamageEnemy(world.EnemyIds.Single(), P1, 100);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            return world.Events.OfType<GateRewardGranted>().Single();
        }

        // --- The odds ------------------------------------------------------------------------

        [Test]
        public void RankAndLuckTipTheOddsTowardRarerTiersAndNeverMoveCommon()
        {
            var odds = new RarityOddsDefinition();
            Assert.That(odds.WeightOf(Rarity.Common, 0, 0), Is.EqualTo(60));
            Assert.That(odds.WeightOf(Rarity.Legendary, 0, 0), Is.EqualTo(2));
            Assert.That(odds.WeightOf(Rarity.Common, 3, 20), Is.EqualTo(60), "Common never changes");
            Assert.That(odds.WeightOf(Rarity.Legendary, 3, 0), Is.EqualTo(2 * (100 + 3 * 3 * 25) / 100), "three ranks up: +225% per tier, three tiers");
            Assert.That(odds.WeightOf(Rarity.Rare, 0, 10), Is.EqualTo(28 * (100 + 10 * 2) / 100), "ten Luck: +20% for one tier");
            Assert.That(odds.WeightOf(Rarity.Epic, 2, 0), Is.GreaterThan(odds.WeightOf(Rarity.Epic, 1, 0)));
            Assert.That(odds.WeightOf(Rarity.Epic, -1, -5), Is.EqualTo(odds.WeightOf(Rarity.Epic, 0, 0)), "below E or below the start counts as none");
        }

        [Test]
        public void OddsThatCannotBeUsedAreRefused()
        {
            var config = Config("E", Cap);
            config.RarityOdds = new RarityOddsDefinition { BaseWeights = new[] { 60, 28, 10 } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
            config.RarityOdds = new RarityOddsDefinition { BaseWeights = new[] { 60, 0, 10, 2 } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
        }

        /// <summary>How many of <paramref name="clears"/> clears of the den gave the Legendary crown, with both pieces already owned (each draw is a duplicate, so the pool never shrinks).</summary>
        private static int CrownsIn(int clears, string gateRank)
        {
            var world = World(Config(gateRank, Cap, Crown), "warrior", "cap", "crown");
            var crowns = 0;
            for (var i = 0; i < clears; i++)
                if (Clear(world).Reward == Crown) crowns++;
            return crowns;
        }

        [Test]
        public void ALegendaryIsRareAtEAndCommonerAtAHigherRankGate()
        {
            var atE = CrownsIn(300, "E");
            var atB = CrownsIn(300, "B");

            // At E the crown weighs 2 against the cap's 60 (about 3%); at B, 2 × (100 + 3 × 3 × 25)% = 6 (about 9%).
            Assert.That(atE, Is.LessThan(30), "a Legendary is rare at an E gate");
            Assert.That(atB, Is.GreaterThan(atE), "a B gate tips the draw toward the Legendary");
        }

        [Test]
        public void PiecesOfOneTierDrawAsTheyDidBeforeRarities()
        {
            var a = Piece("a", GearSlot.Feet, Rarity.Epic);
            var b = Piece("b", GearSlot.Hands, Rarity.Epic);
            var config = Config("E", a, b);
            config.Gear = config.Gear.Concat(new[] { a, b }).ToArray();
            var world = World(config, "warrior");

            var first = Clear(world).Reward;
            var second = Clear(world).Reward;

            Assert.That(new[] { first, second }, Is.EquivalentTo(new LootDefinition[] { a, b }), "the first draw takes one, the second the other");
        }

        // --- Weapons belong to their class ------------------------------------------------------

        [Test]
        public void AClearNeverDrawsAnotherClasssWeapon()
        {
            for (var run = 0; run < 20; run++)
            {
                var world = World(Config("E", Sword, Staff), "wizard");
                for (var i = 0; i < run % 3; i++) world.Step(new Dictionary<PlayerId, PlayerCommand>()); // other ticks, other rolls
                Assert.That(Clear(world).Reward, Is.SameAs(Staff), "a Wizard draws only the staff");
            }
        }

        [Test]
        public void APoolWithNothingForTheClassGivesADuplicateToSell()
        {
            var world = World(Config("E", Sword), "wizard");

            var reward = Clear(world);

            Assert.That(reward.Reward, Is.SameAs(Sword));
            Assert.That(reward.IsDuplicate, Is.True, "nothing in the pool is for a Wizard, so it is one to sell");
            Assert.That(world.GetPlayer(P1).Bag, Has.No.Member(Sword));
        }

        [Test]
        public void AWeaponForAnotherClassCannotBeWornButAnyArmourCan()
        {
            var world = World(Config("E"), "wizard", "sword", "crown");
            world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, equipGearId: "sword") });
            world.Step(new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, equipGearId: "crown") });

            var me = world.GetPlayer(P1);
            Assert.That(me.Worn[(int)GearSlot.Weapon], Is.Null, "the Warrior's sword stays in the bag");
            Assert.That(me.Worn[(int)GearSlot.Head], Is.SameAs(Crown), "any class wears any armour");
        }

        [Test]
        public void OnlyWeaponsAreLockedToClassesAndOnlyToRealOnes()
        {
            var config = Config("E");
            config.Gear = config.Gear.Concat(new[] { Piece("warrior-helm", GearSlot.Head, Rarity.Rare, "warrior") }).ToArray();
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config), "armour is for every class");

            config = Config("E");
            config.Gear = config.Gear.Concat(new[] { Piece("ghost-blade", GearSlot.Weapon, Rarity.Rare, "ghost") }).ToArray();
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config), "no such class");

            config = Config("E");
            config.Classes = new[] { new ClassDefinition { Id = "warrior", Name = "Warrior", StartingWeaponId = "staff" }, new ClassDefinition { Id = "wizard", Name = "Wizard" } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config), "a class's starting weapon must be its own");
        }

        // --- The game's content ------------------------------------------------------------------

        private static GameContentAsset Content() => AssetDatabase.LoadAssetAtPath<GameContentAsset>("Assets/Data/GameContent.asset");

        [Test]
        public void TheGameHasFourNewSetsAndTwoNewWeaponsForEveryClass()
        {
            var gear = Content().Gear.Where(g => g != null).ToList();
            foreach (var set in new[] { "ironwall", "wayfarer", "nightweave", "starweave" })
            {
                var pieces = gear.Where(g => g.Id.StartsWith(set + "-")).ToList();
                Assert.That(pieces.Select(p => p.Slot), Is.EquivalentTo(new[] { GearSlot.Head, GearSlot.Body, GearSlot.Hands, GearSlot.Feet }), $"the {set} set");
                Assert.That(pieces.All(p => p.ClassIds.Count == 0), $"any class wears the {set} set");
            }
            foreach (var c in Content().Classes.Where(c => c != null))
            {
                var own = gear.Where(g => g.Slot == GearSlot.Weapon && g.ClassIds.Contains(c.Id) && (c.StartingWeapon == null || g != c.StartingWeapon)).ToList();
                Assert.That(own.Count(g => g.Rarity == Rarity.Rare), Is.GreaterThanOrEqualTo(1), $"{c.DisplayName} has a Rare weapon");
                Assert.That(own.Count(g => g.Rarity == Rarity.Legendary), Is.GreaterThanOrEqualTo(1), $"{c.DisplayName} has a Legendary weapon");
            }
            Assert.That(gear.Where(g => g.Slot == GearSlot.Weapon).All(g => g.ClassIds.Count > 0), "every weapon belongs to a class");
        }

        [Test]
        public void EveryNewPieceDropsFromAGateAndHasALabelledLook()
        {
            var content = Content();
            var dropped = new HashSet<GearDefinitionAsset>(content.Gates.Where(g => g != null).SelectMany(g => g.GearPool));
            var newIds = new[] { "ironwall-", "wayfarer-", "nightweave-", "starweave-" };
            foreach (var piece in content.Gear.Where(g => g != null && (newIds.Any(p => g.Id.StartsWith(p)) || g.Rarity == Rarity.Legendary)))
            {
                Assert.That(dropped, Does.Contain(piece), $"{piece.DisplayName} drops from a gate");
                Assert.That(piece.Look, Is.Not.Null, $"{piece.DisplayName} has a look");
                Assert.That(AssetDatabase.GetLabels(piece.Look), Does.Contain("AI"), $"{piece.DisplayName}'s look is a placeholder labelled AI");
            }
        }
    }
}
