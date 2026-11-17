using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// How a species travels when its hunter "mounts" (ticket 90): birds and gliders fly, fast land animals run on
    /// their own legs, slow ones pedal a bicycle. Every style goes at <see cref="SimulationConfig.MountSpeed"/>, so
    /// a co-op party stays together. Running and riding keep ticket 42's ground rules. Flying keeps a height above
    /// the ground and crosses cliffs, water and steep ground; how high a flyer may go grows with their rank.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>How a hunter with no species (or one from before species) travels: on a mount that runs.</summary>
        private static TravelStyle TravelOf(Player p) => p.Species?.Travel ?? TravelStyle.Run;

        private static bool IsFlying(Player p) => p.Mounted && TravelOf(p) == TravelStyle.Fly;

        /// <summary>The lowest a flyer goes: where they skim at first, and where a dive stops.</summary>
        private float FlightFloor => Math.Max(0.5f, _config.FlightSkimHeight);

        /// <summary>How high above the ground <paramref name="p"/> may fly at their rank: never below the skimming height.</summary>
        private float FlightCeilingOf(Player p)
        {
            var ranks = _config.Progression.Ranks;
            var index = _config.Progression.RankIndexAt(p.Level);
            var ceiling = ranks != null && index >= 0 && index < ranks.Length ? ranks[index]?.FlightCeiling ?? 0f : 0f;
            return Math.Max(FlightFloor, ceiling);
        }

        /// <summary>How high above the ground <paramref name="id"/> may fly at their rank (for the HUD).</summary>
        public float FlightCeiling(PlayerId id) => FlightCeilingOf(FindPlayer(id));

        /// <summary>A flyer climbs or dives within their rank's reach, at <see cref="SimulationConfig.FlightClimbSpeed"/>.</summary>
        private void Fly(Player p, int climb)
        {
            var height = p.FlightHeight + Math.Sign(climb) * _config.FlightClimbSpeed * TickDelta;
            p.FlightHeight = Math.Min(Math.Max(height, FlightFloor), FlightCeilingOf(p));
        }

        /// <summary>Taking off: up to the skimming height.</summary>
        private void TakeOff(Player p) => p.FlightHeight = IsFlying(p) ? FlightFloor : 0f;

        /// <summary>Getting off, or knocked off: a flyer drops to the ground below; anyone else is already on it.</summary>
        private void Touchdown(Player p)
        {
            if (p.FlightHeight <= 0f) return;
            p.FlightHeight = 0f;
            p.Position = OnGround(p.Position);
        }

        /// <summary>
        /// Whether a flyer can choose to land here: on ground they could stand on. A hit knocks them down wherever
        /// they are; over water or a cliff they then walk off it.
        /// </summary>
        private bool CanLandAt(Player p) => !IsFlying(p) || Ground.IsWalkable(p.Position);

        /// <summary>Whether <paramref name="id"/> could get off here (for saying why R did nothing): a flyer needs ground below to stand on.</summary>
        public bool CanLand(PlayerId id) => CanLandAt(FindPlayer(id));

        private FlyingGround _flyingGround;

        /// <summary>The ground as a flyer sees it: anything on the map that is loaded (ticket 90).</summary>
        private IGround FlyingGroundOver(IGround ground)
        {
            if (_flyingGround == null || !ReferenceEquals(_flyingGround.Inner, ground)) _flyingGround = new FlyingGround(ground);
            return _flyingGround;
        }

        /// <summary>The ground a player moves over this tick, by how they travel.</summary>
        private IGround GroundFor(Player p, IGround ground) =>
            !p.Mounted ? ground : IsFlying(p) ? FlyingGroundOver(ground) : RidingGround(ground);
    }

    /// <summary>
    /// The ground as a flyer sees it (ticket 90): cliffs, water and steep faces are all flown over. Only the edge of
    /// the map and ground not loaded yet stop them. The height is still the ground's: a flyer keeps a height above it.
    /// </summary>
    public sealed class FlyingGround : IGround
    {
        public FlyingGround(IGround inner) => Inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public IGround Inner { get; }

        public float HeightAt(Vec3 p) => Inner.HeightAt(p);

        public GroundKind KindAt(Vec3 p)
        {
            var kind = Inner.KindAt(p);
            if (kind == GroundKind.Unloaded) return kind;
            if (Inner is IMapBounds bounds && !bounds.IsOnMap(p)) return GroundKind.Blocked;
            return GroundKind.Walkable;
        }

        public Vec3 Confine(Vec3 from, Vec3 to) => GroundExtensions.Slide(this, from, to);

        public float ReachAlong(Vec3 origin, Vec3 direction, float range) => GroundExtensions.StepAlong(this, origin, direction, range, 0.5f);
    }

    /// <summary>Ground with an edge (the overworld's map): where the map ends, nobody goes, not even a flyer.</summary>
    public interface IMapBounds
    {
        bool IsOnMap(Vec3 p);
    }

    /// <summary>How a species travels when its hunter mounts up (ticket 90). Every style goes at the same speed.</summary>
    public enum TravelStyle : byte
    {
        /// <summary>On its own legs: bounding or galloping, no mount. Follows the ground, and does not climb.</summary>
        Run = 0,

        /// <summary>Pedals a bicycle. Follows the ground, and does not climb.</summary>
        Bicycle = 1,

        /// <summary>Flies at a height above the ground, over cliffs and water. Freer with each rank.</summary>
        Fly = 2,
    }
}
