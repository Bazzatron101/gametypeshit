using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// What makes a gate a raid (ticket 29, ADR 0023): its boss fight has phases built for up to ten players.
    /// <list type="number">
    /// <item><b>Shielded.</b> When the boss's room fills, the boss is shielded and crystals stand far apart in
    /// the room, one for every <see cref="PlayersPerCrystal"/> players present. The shield falls only when every
    /// crystal is broken within <see cref="CrystalRegrowSeconds"/> of the first: the party has to split up and
    /// strike together. Too slow, and the broken ones grow back.</item>
    /// <item><b>Open.</b> The boss fights as any boss does.</item>
    /// <item><b>Final.</b> From <see cref="ShelterAtHealthPercent"/> of its health it also stops, every so
    /// often, to wind up a blast that fills the room. Only a shelter circle, marked somewhere new each time,
    /// is safe, so everyone has to get into it together.</item>
    /// </list>
    /// A raid's numbers live in the game's content, on the gate, so they are named by the gate's id in snapshots.
    /// </summary>
    public sealed class RaidDefinition
    {
        /// <summary>What a crystal is: its health (scaled for the party, like any enemy). It never moves or attacks.</summary>
        public EnemyDefinition Crystal { get; set; }

        /// <summary>Where crystals stand, from the boss room's centre, in the order they are used. At most this many appear.</summary>
        public List<Vec3> CrystalPlaces { get; } = new List<Vec3>();

        /// <summary>One crystal for every this many players present when the room fills (rounded up; always at least one).</summary>
        public int PlayersPerCrystal { get; set; } = 3;

        /// <summary>Once the first crystal breaks, how long the rest have before the broken ones grow back.</summary>
        public float CrystalRegrowSeconds { get; set; } = 8f;

        /// <summary>The final phase starts when the boss's health falls to this percentage of its maximum or below.</summary>
        public int ShelterAtHealthPercent { get; set; } = 35;

        /// <summary>The first blast is wound up this long after the final phase begins.</summary>
        public float FirstShelterAfterSeconds { get; set; } = 2f;

        /// <summary>From one blast landing to the next one starting.</summary>
        public float ShelterEverySeconds { get; set; } = 12f;

        /// <summary>How long from the shelter being marked to the blast.</summary>
        public float ShelterWindupSeconds { get; set; } = 4f;

        /// <summary>The shelter circle's radius: it must hold the whole party.</summary>
        public float ShelterRadius { get; set; } = 3.5f;

        /// <summary>A player outside the shelter loses this percentage of their maximum health. A dodge or a jump does not help.</summary>
        public int ShelterDamagePercent { get; set; } = 80;

        /// <summary>Where the shelter is marked, from the boss room's centre: the first place, then the next, round and round.</summary>
        public List<Vec3> ShelterPlaces { get; } = new List<Vec3>();

        /// <summary>How many crystals a party of <paramref name="present"/> players meets.</summary>
        public int CrystalsFor(int present)
        {
            var perCrystal = Math.Max(1, PlayersPerCrystal);
            var wanted = (Math.Max(1, present) + perCrystal - 1) / perCrystal;
            return Math.Max(1, Math.Min(wanted, CrystalPlaces.Count));
        }

        /// <summary>Whether <paramref name="point"/> is inside a shelter at <paramref name="shelter"/>. The simulation, the view and bots all use it.</summary>
        public bool InShelter(Vec3 shelter, Vec3 point)
        {
            var dx = point.X - shelter.X;
            var dz = point.Z - shelter.Z;
            return dx * dx + dz * dz <= ShelterRadius * ShelterRadius;
        }
    }

    public enum RaidPhase : byte
    {
        /// <summary>Nobody has reached the boss's room yet (or a wipe or a solo fall reset it).</summary>
        Waiting,

        /// <summary>The boss is shielded until every crystal is broken together.</summary>
        Shielded,

        /// <summary>The shield is down: the boss can be hurt.</summary>
        Open,

        /// <summary>The boss is low: it also winds up room-wide blasts that only the shelter is safe from.</summary>
        Final,
    }

    /// <summary>A raid as presentation, bots and tests see it: a live view of the world's gate.</summary>
    public sealed class RaidState
    {
        private readonly SimulationWorld _world;

        internal RaidState(SimulationWorld world, RaidDefinition definition, int room)
        {
            _world = world;
            Definition = definition;
            Room = room;
        }

        public RaidDefinition Definition { get; }

        /// <summary>The room the raid's fight is in: the boss's.</summary>
        public int Room { get; }

        public RaidPhase Phase => _world.RaidPhaseNow();

        /// <summary>This fight's crystals, in place order. Broken ones stay listed (they may grow back) until the shield falls.</summary>
        public IReadOnlyList<EnemyId> Crystals => _world.RaidCrystals();

        public bool IsCrystal(EnemyId id) => _world.IsRaidCrystal(id);

        /// <summary>While some crystals are broken and some are not: seconds before the broken ones grow back. Null otherwise.</summary>
        public float? SecondsUntilRegrow => _world.RaidSecondsUntilRegrow();

        /// <summary>The boss is shielded: it takes no damage.</summary>
        public bool IsBossShielded => Phase == RaidPhase.Shielded;

        /// <summary>A blast being wound up: where the shelter is. Null when none is coming.</summary>
        public Vec3? Shelter => _world.RaidShelter();

        /// <summary>The tick the blast lands, while one is being wound up; 0 otherwise.</summary>
        public int ShelterLandsAtTick => _world.RaidShelterLandsAtTick();

        /// <summary>Seconds until the blast lands; null when none is coming.</summary>
        public float? SecondsUntilBlast => ShelterLandsAtTick == 0 ? (float?)null : Math.Max(0, ShelterLandsAtTick - _world.Tick) * _world.TickDelta;
    }
}
