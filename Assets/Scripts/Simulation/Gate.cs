using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>An enemy's place in a room: which enemy, and where from the room's centre.</summary>
    public readonly struct RoomSpawn
    {
        public readonly EnemyDefinition Enemy;
        public readonly Vec3 OffsetFromCentre;

        public RoomSpawn(EnemyDefinition enemy, Vec3 offsetFromCentre)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            OffsetFromCentre = offsetFromCentre;
        }
    }

    /// <summary>
    /// One piece of the room kit: a rectangular floor, the enemies that appear when the party
    /// reaches it, and optionally a checkpoint. The same piece can be used in many gates.
    /// </summary>
    public sealed class RoomDefinition
    {
        /// <summary>Across the gate's direction of travel, in metres.</summary>
        public float Width { get; set; } = 10f;

        /// <summary>Along the gate's direction of travel, in metres.</summary>
        public float Length { get; set; } = 10f;

        public List<RoomSpawn> Spawns { get; } = new List<RoomSpawn>();

        public bool HasCheckpoint { get; set; }

        /// <summary>Where the checkpoint stands, from the room's centre.</summary>
        public Vec3 CheckpointOffset { get; set; }
    }

    /// <summary>
    /// A gate: the way into a dungeon, as an ordered chain of rooms from the kit. Rooms run end to
    /// end along +Z from the entrance, each centred on the same line, so a narrower room between two
    /// wide ones reads as a corridor or a doorway. The way onward is never locked.
    /// </summary>
    public sealed class GateDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<RoomDefinition> Rooms { get; } = new List<RoomDefinition>();

        /// <summary>Solo: how many times a fallen player may revive at the latest checkpoint before the gate starts again.</summary>
        public int SoloRevives { get; set; } = 3;

        /// <summary>How close a living player must come to a checkpoint to reach it.</summary>
        public float CheckpointRadius { get; set; } = 2f;

        /// <summary>Co-op: how long after the whole party is down before everyone returns at the latest checkpoint.</summary>
        public float WipeDelaySeconds { get; set; } = 3f;

        /// <summary>The clear reward: each player present draws one they do not have yet (spec: a fixed pool, random draw, no repeats until it is all owned).</summary>
        public List<ModifierDefinition> RewardPool { get; } = new List<ModifierDefinition>();

        /// <summary>True when some room holds a boss: then defeating the bosses is what clears the gate.</summary>
        public bool HasBoss
        {
            get
            {
                foreach (var room in Rooms)
                foreach (var spawn in room.Spawns)
                    if (spawn.Enemy.Boss != null) return true;
                return false;
            }
        }
    }

    public enum RoomState : byte
    {
        /// <summary>Nobody has been in yet (or a wipe reset it): its enemies are not in the world.</summary>
        NotEntered,

        /// <summary>The party has reached it and its enemies are out.</summary>
        Entered,

        /// <summary>All its enemies are defeated (or it never had any).</summary>
        Cleared,
    }

    /// <summary>A room's floor in the world.</summary>
    public readonly struct RoomRect
    {
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public RoomRect(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public Vec3 Centre => new Vec3((MinX + MaxX) / 2f, 0f, (MinZ + MaxZ) / 2f);

        public bool Contains(Vec3 p) => p.X >= MinX && p.X <= MaxX && p.Z >= MinZ && p.Z <= MaxZ;

        /// <summary>The point of this floor nearest to <paramref name="p"/> (<paramref name="p"/> itself when inside).</summary>
        public Vec3 Nearest(Vec3 p) => new Vec3(Math.Min(Math.Max(p.X, MinX), MaxX), p.Y, Math.Min(Math.Max(p.Z, MinZ), MaxZ));
    }

    /// <summary>A checkpoint's place in the world, and the room it stands in.</summary>
    public readonly struct CheckpointPlace
    {
        public readonly int Room;
        public readonly Vec3 Position;

        public CheckpointPlace(int room, Vec3 position)
        {
            Room = room;
            Position = position;
        }
    }

    /// <summary>Where a gate's rooms, checkpoints and entrance are in the world: worked out once from its definition.</summary>
    public sealed class GateLayout
    {
        /// <summary>How far inside the first room the party appears.</summary>
        private const float EntranceInset = 2f;

        public IReadOnlyList<RoomRect> Rooms { get; }

        /// <summary>In order along the gate; a checkpoint's index here is its number everywhere else.</summary>
        public IReadOnlyList<CheckpointPlace> Checkpoints { get; }

        public Vec3 Entrance { get; }

        public GateLayout(GateDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Rooms.Count == 0) throw new ArgumentException("A gate needs at least one room.", nameof(definition));

            var rooms = new List<RoomRect>();
            var checkpoints = new List<CheckpointPlace>();
            var z = 0f;
            for (var i = 0; i < definition.Rooms.Count; i++)
            {
                var room = definition.Rooms[i];
                if (room.Width <= 0f || room.Length <= 0f) throw new ArgumentException($"Room {i} of gate '{definition.Id}' has no floor.", nameof(definition));
                var rect = new RoomRect(-room.Width / 2f, room.Width / 2f, z, z + room.Length);
                rooms.Add(rect);
                if (room.HasCheckpoint) checkpoints.Add(new CheckpointPlace(i, rect.Nearest(rect.Centre + room.CheckpointOffset)));
                z += room.Length;
            }
            Rooms = rooms;
            Checkpoints = checkpoints;
            Entrance = new Vec3(0f, 0f, Math.Min(EntranceInset, definition.Rooms[0].Length / 2f));
        }

        /// <summary>
        /// The nearest point that is on some room's floor: <paramref name="p"/> itself when it is on
        /// one. Rooms share edges, so this is what makes the outer walls and keeps the ways between
        /// rooms open.
        /// </summary>
        public Vec3 Confine(Vec3 p)
        {
            var best = p;
            var bestDistance = float.MaxValue;
            foreach (var room in Rooms)
            {
                if (room.Contains(p)) return p;
                var nearest = room.Nearest(p);
                var distance = (nearest - p).Length;
                if (distance >= bestDistance) continue;
                best = nearest;
                bestDistance = distance;
            }
            return best;
        }
    }

    /// <summary>
    /// A running gate as presentation and tests see it: which rooms are entered or cleared, which
    /// checkpoints are activated, and each player's revives. A live view of the world's gate.
    /// </summary>
    public sealed class GateState
    {
        private readonly SimulationWorld _world;

        internal GateState(SimulationWorld world, GateDefinition definition, GateLayout layout)
        {
            _world = world;
            Definition = definition;
            Layout = layout;
        }

        public GateDefinition Definition { get; }
        public GateLayout Layout { get; }

        public RoomState RoomState(int room) => _world.GateRoomState(room);

        public bool IsCheckpointActivated(int checkpoint) => _world.GateCheckpointActivated(checkpoint);

        /// <summary>The furthest checkpoint reached, or -1 when only the entrance has been.</summary>
        public int LatestCheckpoint => _world.GateLatestCheckpoint();

        /// <summary>Where the latest checkpoint is; the entrance when none has been reached.</summary>
        public Vec3 LatestCheckpointPosition => LatestCheckpoint >= 0 ? Layout.Checkpoints[LatestCheckpoint].Position : Layout.Entrance;

        /// <summary>
        /// The gate is done: its boss is defeated, or, in a gate without a boss, every room is
        /// cleared. Stays true for the rest of the attempt.
        /// </summary>
        public bool IsCleared => _world.GateIsCleared();

        /// <summary>True when the gate ends in a boss (see <see cref="GateDefinition.HasBoss"/>).</summary>
        public bool HasBoss => Definition.HasBoss;

        /// <summary>1 for the first attempt; one more each time the gate starts again from the entrance.</summary>
        public int Attempt => _world.GateAttempt();

        /// <summary>Solo revives the player has left this attempt. Kept per player, whoever comes and goes.</summary>
        public int RevivesLeft(PlayerId player) => _world.GateRevivesLeft(player);

        /// <summary>The party-size tier for the players present now: what the next enemies to spawn are scaled for.</summary>
        public PartyTierKind PartyTier => _world.GatePartyTier();

        /// <summary>True while exactly one player is present: the solo death rule applies.</summary>
        public bool IsSolo => _world.GateIsSolo();

        /// <summary>Co-op: seconds until a party that is all down returns at the latest checkpoint; null when no wipe is pending.</summary>
        public float? SecondsUntilWipe => _world.GateSecondsUntilWipe();

        /// <summary>After the clear, in a game with a hub: where the return portal stands. Null before the clear.</summary>
        public Vec3? ReturnPortal => _world.Hub != null && IsCleared ? _world.GatePortalPosition() : (Vec3?)null;

        /// <summary>After the clear, in a game with a hub: seconds until the party is brought home. Null otherwise.</summary>
        public float? SecondsUntilReturn => _world.GateSecondsUntilReturn();

        /// <summary>Where a player joining or rejoining the run appears: at the latest checkpoint (ADR 0003), a step to one side.</summary>
        public Vec3 JoinPosition(PlayerId player) => _world.GateStandingPlace(LatestCheckpointPosition, player);
    }
}
