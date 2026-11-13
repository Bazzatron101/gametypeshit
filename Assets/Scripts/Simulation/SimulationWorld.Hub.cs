using System;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The hub (ticket 19): a small shared space that is also the lobby. While no gate is running
    /// the party is in the hub, inside its walls. Players mark themselves ready; the host takes
    /// the party into a gate once everyone present is ready. After a clear a return portal opens
    /// in the boss's room: a living player walking into it, or a countdown, brings the party home.
    /// The host can also leave a run at any time.
    /// </summary>
    public sealed class HubDefinition
    {
        public float Width { get; set; } = 24f;
        public float Length { get; set; } = 20f;

        /// <summary>Where the hub is in the world. Gates are laid out from the origin along +Z, so the hub sits elsewhere.</summary>
        public Vec3 Centre { get; set; } = new Vec3(0f, 0f, -60f);

        /// <summary>The gate board, from the hub's centre.</summary>
        public Vec3 BoardOffset { get; set; } = new Vec3(0f, 0f, 7f);

        /// <summary>How close a player must stand to read the board.</summary>
        public float BoardRadius { get; set; } = 3f;

        /// <summary>After a clear, how long before the party is brought home if nobody walks into the portal.</summary>
        public float ReturnCountdownSeconds { get; set; } = 30f;

        /// <summary>How close a living player must come to the return portal to go through.</summary>
        public float PortalRadius { get; set; } = 1.5f;

        public Vec3 Board => Centre + BoardOffset;

        public RoomRect Floor => new RoomRect(Centre.X - Width / 2f, Centre.X + Width / 2f, Centre.Z - Length / 2f, Centre.Z + Length / 2f);
    }

    public sealed partial class SimulationWorld
    {
        private const float HubStandingRadius = 1.5f;

        /// <summary>True while the party is in the hub: the game has one and no gate is running.</summary>
        public bool InHub => _config.Hub != null && _gate == null;

        public HubDefinition Hub => _config.Hub;

        /// <summary>Where a player arriving in the hub stands: round its centre, each at their own place.</summary>
        public Vec3 HubJoinPosition(PlayerId player)
        {
            var hub = _config.Hub ?? throw new InvalidOperationException("This game has no hub.");
            var angle = player.Value * GoldenAngleRadians;
            var place = hub.Centre + new Vec3((float)Math.Cos(angle) * HubStandingRadius, 0f, (float)Math.Sin(angle) * HubStandingRadius);
            return hub.Floor.Nearest(place);
        }

        /// <summary>Where a joining or returning player appears: the latest checkpoint of a running gate (ADR 0003), else the hub; null in an open field.</summary>
        public Vec3? JoinPosition(PlayerId player) =>
            _gate != null ? _gate.View.JoinPosition(player) : _config.Hub != null ? HubJoinPosition(player) : (Vec3?)null;

        /// <summary>
        /// The gates the host's world has cleared (ADR 0005: host-world progress), oldest first. The
        /// host loads them from its world save; clears add to them; snapshots carry them to guests.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> ClearedGates => _clearedGates;

        private readonly System.Collections.Generic.List<string> _clearedGates = new System.Collections.Generic.List<string>();

        /// <summary>Host only: the clears the host's world already holds (from its save), replacing any here.</summary>
        public void SetClearedGates(System.Collections.Generic.IEnumerable<string> gateIds)
        {
            _clearedGates.Clear();
            foreach (var id in gateIds ?? Enumerable.Empty<string>())
                if (id != null && !_clearedGates.Contains(id)) _clearedGates.Add(id);
        }

        /// <summary>True when the player stands close enough to the gate board to read it.</summary>
        public bool IsNearBoard(PlayerId player)
        {
            if (!InHub) return false;
            var at = FindPlayer(player).Position;
            return Flat(at - _config.Hub.Board).Length <= _config.Hub.BoardRadius;
        }

        /// <summary>Whether the host may take the party into a gate now; if not, <paramref name="reason"/> says why.</summary>
        public bool CanStartGate(out string reason)
        {
            if (!InHub)
            {
                reason = _config.Hub == null ? "There is no hub." : "The party is already in a gate.";
                return false;
            }
            var waiting = _players.Where(p => !p.Away && !p.Ready).Select(p => $"Player {p.Id.Value}").ToList();
            reason = waiting.Count == 0 ? null : string.Join(", ", waiting) + (waiting.Count == 1 ? " is not ready." : " are not ready.");
            return waiting.Count == 0;
        }

        /// <summary>The gate board's start: takes the party into <paramref name="gateId"/> if everyone present is ready. Host only; immediate.</summary>
        public bool TryStartGateFromHub(string gateId, out string reason)
        {
            if (!CanStartGate(out reason)) return false;
            StartGate(gateId);
            return true;
        }

        /// <summary>The host leaves the run: the party is brought home at the start of the next tick, cleared or not. Nothing is lost but the run.</summary>
        public void LeaveGate()
        {
            if (_config.Hub == null) throw new InvalidOperationException("There is no hub to go back to.");
            Enqueue(tick =>
            {
                if (_gate != null) ReturnToHub(tick);
            });
        }

        private void TrySetReady(Player player, bool ready, int tick)
        {
            if (!InHub || player.Ready == ready) return;
            player.Ready = ready;
            _events.Add(new ReadyChanged(tick, player.Id, ready));
        }

        /// <summary>Walls: in the hub, nobody leaves its floor.</summary>
        private void ConfineToHub()
        {
            if (!InHub) return;
            var floor = _config.Hub.Floor;
            foreach (var player in _players) player.Position = floor.Nearest(player.Position);
            foreach (var enemy in _enemies) enemy.Position = floor.Nearest(enemy.Position);
        }

        /// <summary>Where the return portal stands once the gate is cleared: the middle of the boss's room, or of the last room.</summary>
        internal Vec3 GatePortalPosition()
        {
            var rooms = _gate.Definition.Rooms;
            for (var room = rooms.Count - 1; room >= 0; room--)
                if (rooms[room].Spawns.Any(s => s.Enemy.Boss != null)) return _gate.Layout.Rooms[room].Centre;
            return _gate.Layout.Rooms[rooms.Count - 1].Centre;
        }

        /// <summary>After the clear: a living player in the portal, or the countdown running out, brings the party home.</summary>
        private bool UpdateReturn(int tick)
        {
            if (_config.Hub == null || !_gate.ClearAnnounced) return false;
            var opening = _gate.ReturnAtTick == 0;
            if (opening) _gate.ReturnAtTick = ReturnTick(tick);

            // Only walking in counts: anyone already standing on the spot when it opens must step out first
            // (the same rule as checkpoints), so the clear and its reward are seen before anyone goes home.
            var portal = GatePortalPosition();
            var through = false;
            foreach (var player in _players)
            {
                var key = (player.Id.Value, PortalMarker);
                var inside = player.IsAlive && !player.Away && Flat(player.Position - portal).Length <= _config.Hub.PortalRadius;
                if (!inside) _gate.Inside.Remove(key);
                else if (_gate.Inside.Add(key) && !opening) through = true;
            }
            if (!through && tick < _gate.ReturnAtTick) return false;
            ReturnToHub(tick);
            return true;
        }

        /// <summary>The portal's entry in the gate's who-is-standing-where set (checkpoints use their numbers, from 0).</summary>
        private const int PortalMarker = -1;

        /// <summary>When the party goes home if the countdown starts at <paramref name="tick"/>.</summary>
        private int ReturnTick(int tick) => tick + Math.Max(1, SecondsToTicks(_config.Hub.ReturnCountdownSeconds));

        /// <summary>The party comes home: the gate and everything in it is gone, and everyone stands in the hub, on their feet.</summary>
        private void ReturnToHub(int tick)
        {
            var gateId = _gate.Definition.Id;
            var cleared = _gate.ClearAnnounced;
            foreach (var id in _enemyIds.ToList()) DespawnEnemy(id);
            _projectiles.Clear();
            _gate = null;
            foreach (var player in _players)
            {
                GetUp(player);
                player.Position = HubJoinPosition(player.Id);
                player.Ready = false;
            }
            _events.Add(new ReturnedToHub(tick, gateId, cleared));
        }
    }
}
