using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Loot (ticket 22). Each gate has a fixed pool. A clear gives every player present their own
    /// reward from it, with no repeats until they own the whole pool; after that the reward is a
    /// duplicate, which they keep to sell. Stores around the world buy duplicates for gold, each
    /// paying its own share of an item's value: the guild's store is the easiest to reach and pays
    /// the least (the developer's rule).
    /// </summary>
    public class LootTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static ModifierDefinition Item(string id, int value = 100) => new ModifierDefinition
        {
            Id = id, Name = id, AppliesToTag = "fire", DamagePercent = 10, SellValue = value,
        };

        private static readonly Vec3 HubCentre = new Vec3(0f, 0f, -60f);
        private static readonly Vec3 GuildStore = HubCentre + new Vec3(6f, 0f, 0f);
        private static readonly Vec3 TownStore = HubCentre + new Vec3(-6f, 0f, 0f);

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMaxHealth = 1000,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Hub = new HubDefinition { Centre = HubCentre, Width = 30f, Length = 20f },
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
                    RewardPool = { Item("ember", 100), Item("frost", 300) },
                },
            },
            Shops = new[]
            {
                new ShopDefinition { Id = "guild", Name = "Guild Exchange", Position = GuildStore, Radius = 2f, SellPercent = 50 },
                new ShopDefinition { Id = "town", Name = "Market Stall", Position = TownStore, Radius = 2f, SellPercent = 100 },
            },
        };

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, Dictionary<PlayerId, PlayerCommand> commands = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(commands ?? new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        private static List<SimulationEvent> Sell(SimulationWorld world, string itemId) =>
            Step(world, 1, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, sellItemId: itemId) });

        /// <summary>A world with players who already own the whole pool, cleared once more.</summary>
        private static List<SimulationEvent> ClearOwningTheWholePool(SimulationWorld world, params PlayerId[] players)
        {
            var pool = Config().Gates[0].RewardPool;
            foreach (var p in players)
            {
                world.AddPlayer(p, HubCentre);
                foreach (var item in pool) world.GrantModifier(p, item);
            }
            Step(world);
            world.StartGate("den");
            world.PlacePlayer(players[0], new Vec3(0f, 0f, 10f)); // into the boss room
            Step(world);
            world.DamageEnemy(world.EnemyIds.Single(), players[0], 100);
            return Step(world);
        }

        private static CharacterRecord Carrying(params string[] duplicates)
        {
            var record = new CharacterRecord();
            record.Duplicates.AddRange(duplicates);
            return record;
        }

        [Test]
        public void OnceThePoolIsOwnedAClearGivesADuplicateToKeep()
        {
            var world = new SimulationWorld(Config());

            var events = ClearOwningTheWholePool(world, P1);

            var reward = events.OfType<GateRewardGranted>().Single();
            Assert.That(reward.IsDuplicate, Is.True);
            Assert.That(new[] { "ember", "frost" }, Has.Member(reward.Modifier.Id));
            Assert.That(world.GetPlayer(P1).Duplicates.Select(d => d.Id), Is.EqualTo(new[] { reward.Modifier.Id }));
            Assert.That(world.GetPlayer(P1).ActiveModifiers.Count, Is.EqualTo(2), "a duplicate is not a second copy of the modifier");
            Assert.That(events.OfType<ModifierGranted>(), Is.Empty);
        }

        [Test]
        public void EveryPlayerGetsTheirOwnDrop()
        {
            var world = new SimulationWorld(Config());

            var events = ClearOwningTheWholePool(world, P1, P2);

            Assert.That(events.OfType<GateRewardGranted>().Select(r => r.Player), Is.EquivalentTo(new[] { P1, P2 }));
            Assert.That(world.GetPlayer(P1).Duplicates.Count, Is.EqualTo(1));
            Assert.That(world.GetPlayer(P2).Duplicates.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARewardIsNewUntilThePoolIsOwned()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, HubCentre);
            world.GrantModifier(P1, Item("ember"));
            Step(world);
            world.StartGate("den");
            world.PlacePlayer(P1, new Vec3(0f, 0f, 10f));
            Step(world);
            world.DamageEnemy(world.EnemyIds.Single(), P1, 100);

            var reward = Step(world).OfType<GateRewardGranted>().Single();

            Assert.That(reward.IsDuplicate, Is.False);
            Assert.That(reward.Modifier.Id, Is.EqualTo("frost"), "the one still missing");
            Assert.That(world.GetPlayer(P1).Duplicates, Is.Empty);
        }

        [Test]
        public void TheGuildsStorePaysHalfAnItemsValue()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, GuildStore, Carrying("frost", "frost"));

            var sold = Sell(world, "frost").OfType<ItemSold>().Single();

            Assert.That(sold.Gold, Is.EqualTo(150));
            Assert.That(sold.ShopId, Is.EqualTo("guild"));
            Assert.That(world.GetPlayer(P1).Gold, Is.EqualTo(150));
            Assert.That(world.GetPlayer(P1).Duplicates.Select(d => d.Id), Is.EqualTo(new[] { "frost" }), "one copy sold, one kept");
        }

        [Test]
        public void ATownStorePaysMoreForTheSameItem()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, TownStore, Carrying("frost"));

            Sell(world, "frost");

            Assert.That(world.GetPlayer(P1).Gold, Is.EqualTo(300));
            Assert.That(world.ShopInReach(P1).Id, Is.EqualTo("town"));
            Assert.That(world.SellPrice(world.ShopInReach(P1), "frost"), Is.EqualTo(300));
        }

        [Test]
        public void NothingSellsAwayFromAStoreOrThatYouDoNotHave()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, HubCentre, Carrying("ember"));

            Assert.That(world.ShopInReach(P1), Is.Null);
            Assert.That(Sell(world, "ember").OfType<ItemSold>(), Is.Empty, "no store here");

            world.PlacePlayer(P1, GuildStore);
            Assert.That(Sell(world, "frost").OfType<ItemSold>(), Is.Empty, "no frost to sell");
            Assert.That(Sell(world, "ember").OfType<ItemSold>().Count(), Is.EqualTo(1));
            Assert.That(Sell(world, "ember").OfType<ItemSold>(), Is.Empty, "and only once");
            Assert.That(world.GetPlayer(P1).Gold, Is.EqualTo(50));
        }

        [Test]
        public void AStoreCannotBeReachedFromInsideAGate()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, GuildStore, Carrying("ember"));
            world.StartGate("den");
            Step(world);
            world.PlacePlayer(P1, GuildStore); // the same spot, but the party is in the gate

            Assert.That(Sell(world, "ember").OfType<ItemSold>(), Is.Empty);
        }

        [Test]
        public void GoldAndDuplicatesTravelWithTheCharacter()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, TownStore, Carrying("ember", "frost"));
            Sell(world, "ember");

            var record = world.CaptureCharacter(P1);
            var back = CharacterCodec.Decode(CharacterCodec.Encode(record));
            var next = new SimulationWorld(Config());
            next.AddPlayer(P1, HubCentre, back);

            Assert.That(back.Gold, Is.EqualTo(100));
            Assert.That(back.Duplicates, Is.EqualTo(new[] { "frost" }));
            Assert.That(next.GetPlayer(P1).Gold, Is.EqualTo(100));
            Assert.That(next.GetPlayer(P1).Duplicates.Select(d => d.Id), Is.EqualTo(new[] { "frost" }));
        }

        [Test]
        public void AnOlderSaveHasNoGoldAndNothingToSell()
        {
            var old = CharacterCodec.Decode(CharacterCodec.EncodeVersion2ForTests(new CharacterRecord { Level = 2, ClassId = "rogue" }));

            Assert.That(old.Gold, Is.Zero);
            Assert.That(old.Duplicates, Is.Empty);
            Assert.That(old.ClassId, Is.EqualTo("rogue"));
        }

        [Test]
        public void ADuplicateTheGameNoLongerHasIsDroppedAndGoldCannotBeNegative()
        {
            var world = new SimulationWorld(Config());
            var record = Carrying("ember", "gone-item");
            record.Gold = -5;
            world.AddPlayer(P1, HubCentre, record);

            Assert.That(world.GetPlayer(P1).Duplicates.Select(d => d.Id), Is.EqualTo(new[] { "ember" }));
            Assert.That(world.GetPlayer(P1).Gold, Is.Zero);
        }

        [Test]
        public void GoldAndDuplicatesSurviveASnapshot()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, GuildStore, Carrying("ember", "frost"));
            Sell(world, "frost");

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(copy.GetPlayer(P1).Gold, Is.EqualTo(150));
            Assert.That(copy.GetPlayer(P1).Duplicates.Select(d => d.Id), Is.EqualTo(new[] { "ember" }));
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }

        [Test]
        public void ASaleCommandIsTheSameAfterTheWire()
        {
            var (_, received) = Wire.ReadCommand(Wire.Command(3, new PlayerCommand(0f, 0f, sellItemId: "frost")));

            Assert.That(received.SellItemId, Is.EqualTo("frost"));
            Assert.That(Wire.ReadCommand(Wire.Command(4, PlayerCommand.None)).command.SellItemId, Is.Null);
        }

        [Test]
        public void StoresAreCheckedWhenTheWorldIsMade()
        {
            var badPercent = Config();
            badPercent.Shops = new[] { new ShopDefinition { Id = "greedy", Name = "Greedy", Position = GuildStore, Radius = 2f, SellPercent = 150 } };
            Assert.That(Assert.Throws<ArgumentException>(() => new SimulationWorld(badPercent)).Message, Does.Contain("greedy"));

            var clash = Config();
            clash.Gates[0].RewardPool.Add(Item("ember", 999)); // the same id, priced differently
            Assert.That(Assert.Throws<ArgumentException>(() => new SimulationWorld(clash)).Message, Does.Contain("ember"));

            var twice = Config();
            twice.Shops = new[] { twice.Shops[0], twice.Shops[0] };
            Assert.That(Assert.Throws<ArgumentException>(() => new SimulationWorld(twice)).Message, Does.Contain("guild"));
        }
    }
}
