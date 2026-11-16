using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The walkable world (ADR 0015, ticket 38): where each gate's entrance stands, which region it is
    /// in, and the rules for finding and entering gates out in the world. The ground itself is
    /// <see cref="SimulationConfig.Ground"/>. With an overworld the guild town (the hub) is part of
    /// the world, not a walled room: the party walks out of it, and the board and the ready check
    /// work anywhere outside a gate.
    /// </summary>
    public sealed class OverworldDefinition
    {
        /// <summary>Every gate placed in the world. A gate with no site here is not on the map, and the board offers it as before.</summary>
        public List<GateSite> Gates { get; } = new List<GateSite>();

        /// <summary>Regions open from the start (the Greenmarch). Others open by rank and story (ticket 25), kept in the host's world.</summary>
        public List<string> StartingRegions { get; } = new List<string>();

        /// <summary>How far the guild town reaches from the hub's centre: nobody rides inside it (ticket 42).</summary>
        public float TownRadius { get; set; } = 40f;

        /// <summary>How close the host must stand to a gate's entrance to take the party in.</summary>
        public float EntranceRadius { get; set; } = 3f;

        /// <summary>How close a player must come to a hidden gate to find it.</summary>
        public float FindRadius { get; set; } = 20f;

        /// <summary>A gate in a region that is not open: XP from its enemies, as a percentage (ADR 0015). Loot is not cut.</summary>
        public int UnopenedRegionXpPercent { get; set; } = 25;

        /// <summary>The site of <paramref name="gateId"/>, or null when it is not on the map.</summary>
        public GateSite SiteOf(string gateId)
        {
            foreach (var site in Gates) if (site != null && site.GateId == gateId) return site;
            return null;
        }
    }

    /// <summary>A gate's place in the world.</summary>
    public sealed class GateSite
    {
        /// <summary>The gate, by its id in <see cref="SimulationConfig.Gates"/>.</summary>
        public string GateId { get; set; }

        /// <summary>Where its entrance stands.</summary>
        public Vec3 Entrance { get; set; }

        public string Region { get; set; }

        /// <summary>Known to the guild from the start (story gates, each region's first gate): never hidden.</summary>
        public bool KnownFromStart { get; set; }
    }
}
