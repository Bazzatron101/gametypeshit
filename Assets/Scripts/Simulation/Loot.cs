using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Anything a gate's pool can hold: a modifier (kept for good) or a piece of gear (kept in the
    /// bag and worn in its slot, ticket 23). One id means one item everywhere, because saves,
    /// snapshots and duplicates name items by id.
    /// </summary>
    public abstract class LootDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>
        /// Gold for a duplicate at a store paying full value, at least 1; each store pays its own share
        /// (ticket 22). Not carried in snapshots or events: prices are looked up by the item's id in the gates' pools.
        /// </summary>
        public int SellValue { get; set; } = 100;

        public override string ToString() => Id;
    }

    /// <summary>
    /// A store that buys duplicates (ticket 22). Stores stand around the world, and each pays its
    /// own share of an item's value: the guild's, in the hub, is the easiest to reach and pays the
    /// least (the developer's rule, ADR 0016).
    /// </summary>
    public sealed class ShopDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Where the store stands in the world.</summary>
        public Vec3 Position { get; set; }

        /// <summary>How close a player stands to trade.</summary>
        public float Radius { get; set; } = 2.5f;

        /// <summary>The share of an item's <see cref="ModifierDefinition.SellValue"/> it pays, 1 to 100.</summary>
        public int SellPercent { get; set; } = 100;
    }

    /// <summary>A player sold a duplicate at a store.</summary>
    public sealed class ItemSold : SimulationEvent
    {
        public PlayerId Player { get; }
        public string ItemId { get; }
        public string ShopId { get; }
        public int Gold { get; }

        public ItemSold(int tick, PlayerId player, string itemId, string shopId, int gold) : base(tick)
        {
            Player = player;
            ItemId = itemId;
            ShopId = shopId;
            Gold = gold;
        }
    }

    public sealed partial class SimulationWorld
    {
        /// <summary>Every item a gate can drop, by id (duplicates are kept and saved by id).</summary>
        private Dictionary<string, LootDefinition> _lootById;

        private void IndexLootAndValidateShops()
        {
            _lootById = new Dictionary<string, LootDefinition>(StringComparer.Ordinal);
            foreach (var gate in _config.Gates ?? Array.Empty<GateDefinition>())
            foreach (var item in gate.RewardPool)
            {
                if (item == null || string.IsNullOrEmpty(item.Id)) continue; // the gate checks its own pool
                if (item.SellValue < 1) throw new ArgumentException($"Item '{item.Id}': SellValue must be at least 1.", nameof(_config));
                // Saves and snapshots name a duplicate by id, so one id must mean one item everywhere.
                if (_lootById.TryGetValue(item.Id, out var seen) && (seen.SellValue != item.SellValue || seen.Name != item.Name))
                    throw new ArgumentException($"Item '{item.Id}' is in two gates' pools with different names or sell values.", nameof(_config));
                if (seen == null) _lootById[item.Id] = item;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shop in Shops)
            {
                if (shop == null || string.IsNullOrWhiteSpace(shop.Id)) throw new ArgumentException("A shop has no Id.", nameof(_config));
                if (!ids.Add(shop.Id)) throw new ArgumentException($"Shop '{shop.Id}' is defined twice.", nameof(_config));
                if (shop.SellPercent < 1 || shop.SellPercent > 100)
                    throw new ArgumentException($"Shop '{shop.Id}': SellPercent must be 1 to 100 (it is {shop.SellPercent}).", nameof(_config));
                if (!(shop.Radius > 0f)) throw new ArgumentException($"Shop '{shop.Id}': Radius must be positive.", nameof(_config));
            }
        }

        /// <summary>Every store in the world.</summary>
        public IReadOnlyList<ShopDefinition> Shops => _config.Shops ?? Array.Empty<ShopDefinition>();

        /// <summary>A droppable item by id, or null if no gate drops it.</summary>
        public LootDefinition LootById(string id) => id != null && _lootById.TryGetValue(id, out var item) ? item : null;

        /// <summary>The store this player stands at, or null. Never inside a gate. The first listed wins if two overlap.</summary>
        public ShopDefinition ShopInReach(PlayerId id)
        {
            if (_gate != null) return null;
            var at = FindPlayer(id).Position;
            foreach (var shop in Shops)
                if (Flat(at - shop.Position).Length <= shop.Radius) return shop;
            return null;
        }

        /// <summary>The gold <paramref name="shop"/> pays for one <paramref name="itemId"/>: its share of the value, rounded down, at least 1.</summary>
        public int SellPrice(ShopDefinition shop, string itemId)
        {
            if (shop == null) throw new ArgumentNullException(nameof(shop));
            var item = LootById(itemId) ?? throw new ArgumentException($"No gate drops '{itemId}'.", nameof(itemId));
            return Math.Max(1, (int)((long)item.SellValue * shop.SellPercent / 100));
        }

        private void TrySell(Player player, string itemId, int tick)
        {
            var shop = ShopInReach(player.Id);
            if (shop == null) return;
            var index = player.Duplicates.FindIndex(d => d.Id == itemId);
            if (index < 0) return;
            var gold = SellPrice(shop, itemId);
            player.Duplicates.RemoveAt(index);
            player.Gold = (int)Math.Min(int.MaxValue, (long)player.Gold + gold);
            _events.Add(new ItemSold(tick, player.Id, itemId, shop.Id, gold));
        }
    }
}
