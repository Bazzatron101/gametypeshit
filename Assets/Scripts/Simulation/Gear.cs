using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>Where a piece of gear is worn. One piece per slot (ticket 23, the developer's six).</summary>
    public enum GearSlot : byte
    {
        Weapon,
        Head,
        Body,
        Hands,
        Feet,
        Trinket,
    }

    /// <summary>
    /// A piece of gear (ticket 23). It is kept in the character's bag once found and works only while
    /// worn in its slot. While worn, its modifiers apply as if the player had them, and its tags count
    /// for modifiers that scale with worn gear (<see cref="ModifierDefinition.PerWornTag"/>): that is
    /// how pieces combine with each other and with skills.
    /// </summary>
    public sealed class GearDefinition : LootDefinition
    {
        public const int SlotCount = 6;

        public GearSlot Slot { get; set; }

        /// <summary>Known tags (fire, melee...) that describe the piece for modifiers that count worn gear.</summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>What the piece does while worn. Each is an ordinary modifier, aimed at a tag or one skill.</summary>
        public List<ModifierDefinition> Modifiers { get; } = new List<ModifierDefinition>();

        public bool HasTag(string tag) => Tags != null && Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A player put on a piece of gear. Anything it replaced went back to the bag (<see cref="GearUnequipped"/> first).</summary>
    public sealed class GearEquipped : SimulationEvent
    {
        public PlayerId Player { get; }
        public GearSlot Slot { get; }
        public string GearId { get; }

        public GearEquipped(int tick, PlayerId player, GearSlot slot, string gearId) : base(tick)
        {
            Player = player;
            Slot = slot;
            GearId = gearId;
        }
    }

    /// <summary>A player took a piece of gear off; it stays in their bag.</summary>
    public sealed class GearUnequipped : SimulationEvent
    {
        public PlayerId Player { get; }
        public GearSlot Slot { get; }
        public string GearId { get; }

        public GearUnequipped(int tick, PlayerId player, GearSlot slot, string gearId) : base(tick)
        {
            Player = player;
            Slot = slot;
            GearId = gearId;
        }
    }
}
