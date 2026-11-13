using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The gate runtime (ticket 17): rooms that fill with enemies as the party reaches them,
    /// checkpoints, and the death rules.
    /// <list type="bullet">
    /// <item>Solo (one player present): a fallen player may revive at the latest checkpoint
    /// <see cref="GateDefinition.SoloRevives"/> times; after that the gate starts again from the
    /// entrance, every room refilled.</item>
    /// <item>Co-op: a fallen player comes back when a living teammate reaches a checkpoint (any
    /// activated one, new or earlier), and appears there. There is no respawning by yourself.</item>
    /// <item>Co-op wipe (everyone present down): after a short delay the party returns at the latest
    /// checkpoint and rooms that were not cleared start over.</item>
    /// </list>
    /// None of this ever touches a character's level, XP or skills. Players who are away (a
    /// disconnected friend's character waiting in the run) are not teammates for these rules and
    /// enemies leave them alone.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private const float StandingRingRadius = 1f;

        /// <summary>How long a cleared room's defeated enemies stay in the world before they are removed.</summary>
        private const float TidySeconds = 3f;
        private const float GoldenAngleRadians = 2.39996323f; // spreads players round a point so they never stack

        private sealed class GateRun
        {
            public GateDefinition Definition;
            public GateLayout Layout;
            public GateState View;
            public RoomState[] Rooms;
            public List<EnemyId>[] RoomEnemies;

            /// <summary>Per room: the tick its defeated enemies are removed from the world (0: nothing to remove).</summary>
            public int[] RoomTidyAtTick;
            public bool[] CheckpointActivated;
            public int Attempt = 1;
            public readonly Dictionary<PlayerId, int> RevivesUsed = new Dictionary<PlayerId, int>();

            /// <summary>Who was inside which checkpoint's circle last tick, so reaching one is an arrival, not standing on it.</summary>
            public readonly HashSet<(int player, int checkpoint)> Inside = new HashSet<(int, int)>();

            /// <summary>0 when no wipe is pending.</summary>
            public int WipeAtTick;

            public bool ClearAnnounced;
        }

        private GateRun _gate;

        public bool HasGate => _gate != null;

        /// <summary>The running gate, or null outside one.</summary>
        public GateState Gate => _gate?.View;

        /// <summary>
        /// Opens a gate from the game's content and takes the whole party in: every enemy already
        /// in the world is removed, and every player appears at the entrance on their feet at full
        /// health. Immediate, like <see cref="SpawnEnemy"/>. Host only.
        /// </summary>
        public void StartGate(string gateId)
        {
            var definition = _config.Gates.FirstOrDefault(g => g.Id == gateId)
                             ?? throw new ArgumentException($"There is no gate '{gateId}' in the game's content.", nameof(gateId));
            foreach (var id in _enemyIds.ToList()) DespawnEnemy(id);
            _gate = NewRun(definition);
            foreach (var player in _players) StandUp(player, _gate.Layout.Entrance);
        }

        private GateRun NewRun(GateDefinition definition)
        {
            var layout = new GateLayout(definition);
            var run = new GateRun
            {
                Definition = definition,
                Layout = layout,
                Rooms = new RoomState[definition.Rooms.Count],
                RoomEnemies = definition.Rooms.Select(_ => new List<EnemyId>()).ToArray(),
                RoomTidyAtTick = new int[definition.Rooms.Count],
                CheckpointActivated = new bool[layout.Checkpoints.Count],
            };
            run.View = new GateState(this, definition, layout);
            ResetRooms(run, onlyUnfinished: false);
            return run;
        }

        /// <summary>
        /// Marks a player as away (their connection dropped) or back. An away player stays in the
        /// run where they stood, but is no teammate for the death rules, reaches no rooms or
        /// checkpoints, and is ignored by enemies. Immediate; host only.
        /// </summary>
        public void SetAway(PlayerId id, bool away)
        {
            var player = FindPlayer(id);
            player.Away = away;
            if (!away) NoteWhereTheyStand(player); // coming back onto a checkpoint is not reaching it
        }

        /// <summary>
        /// A player has been put somewhere (revived, joined, back from away) without walking
        /// there. If that is on a checkpoint, they count as already on it: only walking onto a
        /// checkpoint reaches it and brings back the fallen.
        /// </summary>
        private void NoteWhereTheyStand(Player player)
        {
            if (_gate == null) return;
            var checkpoints = _gate.Layout.Checkpoints;
            for (var c = 0; c < checkpoints.Count; c++)
            {
                if (Flat(player.Position - checkpoints[c].Position).Length <= _gate.Definition.CheckpointRadius)
                    _gate.Inside.Add((player.Id.Value, c));
            }
        }

        // --- What GateState reads ---------------------------------------------------------------

        internal RoomState GateRoomState(int room) => _gate.Rooms[room];
        internal bool GateCheckpointActivated(int checkpoint) => _gate.CheckpointActivated[checkpoint];
        internal int GateLatestCheckpoint() => Array.LastIndexOf(_gate.CheckpointActivated, true);
        internal bool GateIsCleared() => _gate.Rooms.All(r => r == RoomState.Cleared);
        internal int GateAttempt() => _gate.Attempt;
        internal bool GateIsSolo() => PresentCount() == 1;

        internal int GateRevivesLeft(PlayerId player) =>
            Math.Max(0, _gate.Definition.SoloRevives - (_gate.RevivesUsed.TryGetValue(player, out var used) ? used : 0));

        internal float? GateSecondsUntilWipe() => _gate.WipeAtTick == 0 ? (float?)null : Math.Max(0, _gate.WipeAtTick - Tick) * TickDelta;

        /// <summary>A place to stand beside <paramref name="point"/>, different for each player and always on the gate's floor.</summary>
        internal Vec3 GateStandingPlace(Vec3 point, PlayerId player)
        {
            var angle = player.Value * GoldenAngleRadians;
            var place = point + new Vec3((float)Math.Cos(angle) * StandingRingRadius, 0f, (float)Math.Sin(angle) * StandingRingRadius);
            return _gate.Layout.Confine(place);
        }

        // --- Each tick ----------------------------------------------------------------------------

        /// <summary>A defeated player asking to come back. Only the solo rule lets a player bring themselves back.</summary>
        private void TryRespawn(Player player, int tick)
        {
            if (_gate == null || player.IsAlive || player.Away || PresentCount() != 1) return;

            _gate.RevivesUsed.TryGetValue(player.Id, out var used);
            if (used >= _gate.Definition.SoloRevives)
            {
                RestartGate(tick);
                return;
            }
            _gate.RevivesUsed[player.Id] = used + 1;
            var checkpoint = GateLatestCheckpoint();
            StandUp(player, _gate.View.LatestCheckpointPosition);
            _events.Add(new PlayerRevived(tick, player.Id, checkpoint, _gate.Definition.SoloRevives - used - 1));
        }

        /// <summary>The gate starts again from the entrance: every room refilled, checkpoints lost, revives given back. Characters keep everything.</summary>
        private void RestartGate(int tick)
        {
            _gate.Attempt++;
            ResetRooms(_gate, onlyUnfinished: false);
            Array.Clear(_gate.CheckpointActivated, 0, _gate.CheckpointActivated.Length);
            _gate.RevivesUsed.Clear();
            _gate.Inside.Clear();
            _gate.WipeAtTick = 0;
            _gate.ClearAnnounced = false;
            foreach (var player in _players)
                if (!player.Away) StandUp(player, _gate.Layout.Entrance); // away players stay where they stood
            _events.Add(new GateRestarted(tick, _gate.Attempt));
        }

        /// <summary>Takes rooms' enemies out of the world and marks the rooms as not yet reached. Rooms with no enemies count as cleared.</summary>
        private void ResetRooms(GateRun run, bool onlyUnfinished)
        {
            for (var room = 0; room < run.Rooms.Length; room++)
            {
                if (onlyUnfinished && run.Rooms[room] == RoomState.Cleared) continue;
                foreach (var enemy in run.RoomEnemies[room])
                    if (HasEnemy(enemy)) DespawnEnemy(enemy);
                run.RoomEnemies[room].Clear();
                run.RoomTidyAtTick[room] = 0;
                run.Rooms[room] = run.Definition.Rooms[room].Spawns.Count == 0 ? RoomState.Cleared : RoomState.NotEntered;
            }
        }

        /// <summary>Back on their feet at full health beside <paramref name="point"/>.</summary>
        private void StandUp(Player player, Vec3 point)
        {
            GetUp(player);
            player.Position = GateStandingPlace(point, player.Id);
            NoteWhereTheyStand(player);
        }

        private int PresentCount()
        {
            var count = 0;
            foreach (var player in _players) if (!player.Away) count++;
            return count;
        }

        /// <summary>Walls: nobody leaves the gate's floor.</summary>
        private void ConfineToGate()
        {
            if (_gate == null) return;
            foreach (var player in _players) player.Position = _gate.Layout.Confine(player.Position);
            foreach (var enemy in _enemies) enemy.Position = _gate.Layout.Confine(enemy.Position);
        }

        /// <summary>After everyone has moved and fought: rooms reached and cleared, checkpoints reached, teammates brought back, wipes.</summary>
        private void UpdateGate(int tick)
        {
            if (_gate == null) return;
            var layout = _gate.Layout;

            for (var room = 0; room < _gate.Rooms.Length; room++)
            {
                if (_gate.Rooms[room] == RoomState.NotEntered && AnyoneStandingIn(layout.Rooms[room]))
                {
                    var centre = layout.Rooms[room].Centre;
                    foreach (var spawn in _gate.Definition.Rooms[room].Spawns)
                        _gate.RoomEnemies[room].Add(SpawnEnemy(spawn.Enemy, layout.Confine(centre + spawn.OffsetFromCentre)));
                    _gate.Rooms[room] = RoomState.Entered;
                    _events.Add(new RoomEntered(tick, room));
                }
                else if (_gate.Rooms[room] == RoomState.Entered && _gate.RoomEnemies[room].All(IsGone))
                {
                    _gate.Rooms[room] = RoomState.Cleared;
                    _gate.RoomTidyAtTick[room] = tick + SecondsToTicks(TidySeconds);
                    _events.Add(new RoomCleared(tick, room));
                }
                else if (_gate.RoomTidyAtTick[room] != 0 && tick >= _gate.RoomTidyAtTick[room])
                {
                    // The bodies have been seen; take them out of the world so snapshots stay small.
                    foreach (var enemy in _gate.RoomEnemies[room])
                        if (HasEnemy(enemy)) DespawnEnemy(enemy);
                    _gate.RoomEnemies[room].Clear();
                    _gate.RoomTidyAtTick[room] = 0;
                }
            }

            if (!_gate.ClearAnnounced && GateIsCleared())
            {
                _gate.ClearAnnounced = true;
                _events.Add(new GateCleared(tick));
            }

            ReachCheckpoints(tick);
            CheckForWipe(tick);
        }

        private bool IsGone(EnemyId id) => !_enemiesById.TryGetValue(id, out var enemy) || enemy.Health <= 0;

        private bool AnyoneStandingIn(RoomRect room)
        {
            foreach (var player in _players)
                if (player.IsAlive && !player.Away && room.Contains(player.Position)) return true;
            return false;
        }

        private void ReachCheckpoints(int tick)
        {
            var checkpoints = _gate.Layout.Checkpoints;
            var radius = _gate.Definition.CheckpointRadius;
            for (var c = 0; c < checkpoints.Count; c++)
            {
                foreach (var player in _players)
                {
                    var key = (player.Id.Value, c);
                    var inside = player.IsAlive && !player.Away && Flat(player.Position - checkpoints[c].Position).Length <= radius;
                    if (!inside)
                    {
                        _gate.Inside.Remove(key);
                        continue;
                    }
                    if (!_gate.Inside.Add(key)) continue; // already here: only arriving counts

                    if (!_gate.CheckpointActivated[c])
                    {
                        _gate.CheckpointActivated[c] = true;
                        _events.Add(new CheckpointActivated(tick, c, player.Id));
                    }
                    if (PresentCount() >= 2) BringBackTheFallen(c, tick);
                }
            }
        }

        /// <summary>Co-op: a living teammate has reached checkpoint <paramref name="checkpoint"/>; everyone down returns there.</summary>
        private void BringBackTheFallen(int checkpoint, int tick)
        {
            foreach (var fallen in _players)
            {
                if (fallen.IsAlive || fallen.Away) continue;
                StandUp(fallen, _gate.Layout.Checkpoints[checkpoint].Position);
                _events.Add(new PlayerRevived(tick, fallen.Id, checkpoint, PlayerRevived.NoLimit));
            }
        }

        private void CheckForWipe(int tick)
        {
            var present = 0;
            var standing = 0;
            foreach (var player in _players)
            {
                if (player.Away) continue;
                present++;
                if (player.IsAlive) standing++;
            }
            if (standing > 0 || present == 0)
            {
                _gate.WipeAtTick = 0; // anyone standing means no wipe
                return;
            }
            if (_gate.WipeAtTick == 0)
            {
                if (present < 2) return; // a solo death is the player's to answer
                // Once a wipe is pending it happens, even if a teammate's connection drops meanwhile.
                _gate.WipeAtTick = tick + Math.Max(1, SecondsToTicks(_gate.Definition.WipeDelaySeconds));
                return;
            }
            if (tick < _gate.WipeAtTick) return;

            _gate.WipeAtTick = 0;
            ResetRooms(_gate, onlyUnfinished: true);
            var checkpoint = GateLatestCheckpoint();
            foreach (var player in _players)
                if (!player.Away) StandUp(player, _gate.View.LatestCheckpointPosition);
            _events.Add(new GateWiped(tick, checkpoint));
        }

        // --- Snapshots ----------------------------------------------------------------------------

        private void WriteGate(BinaryWriter w)
        {
            w.Write(_gate != null);
            if (_gate == null) return;
            w.Write(_gate.Definition.Id);
            w.Write(_gate.Attempt);
            w.Write(_gate.WipeAtTick);
            w.Write(_gate.ClearAnnounced);
            w.Write(_gate.Rooms.Length);
            for (var room = 0; room < _gate.Rooms.Length; room++)
            {
                w.Write((byte)_gate.Rooms[room]);
                w.Write(_gate.RoomTidyAtTick[room]);
                WriteIds(w, _gate.RoomEnemies[room].Select(e => e.Value));
            }
            w.Write(_gate.CheckpointActivated.Length);
            foreach (var activated in _gate.CheckpointActivated) w.Write(activated);
            w.Write(_gate.RevivesUsed.Count);
            foreach (var pair in _gate.RevivesUsed.OrderBy(p => p.Key.Value))
            {
                w.Write(pair.Key.Value);
                w.Write(pair.Value);
            }
            w.Write(_gate.Inside.Count);
            foreach (var (player, checkpoint) in _gate.Inside.OrderBy(k => k.player).ThenBy(k => k.checkpoint))
            {
                w.Write(player);
                w.Write(checkpoint);
            }
        }

        /// <summary>Reads a gate run without touching this world's state; the caller swaps it in once the whole snapshot has parsed.</summary>
        private GateRun ReadGate(BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var id = r.ReadString();
            var definition = _config.Gates.FirstOrDefault(g => g.Id == id)
                             ?? throw new FormatException($"The snapshot names gate '{id}', which this world does not have.");
            var layout = new GateLayout(definition);
            var run = new GateRun
            {
                Definition = definition,
                Layout = layout,
                Attempt = r.ReadInt32(),
                WipeAtTick = r.ReadInt32(),
                ClearAnnounced = r.ReadBoolean(),
            };
            run.View = new GateState(this, definition, layout);

            var rooms = ReadCount(r);
            if (rooms != definition.Rooms.Count) throw new FormatException("The gate has a different number of rooms here.");
            run.Rooms = new RoomState[rooms];
            run.RoomEnemies = new List<EnemyId>[rooms];
            run.RoomTidyAtTick = new int[rooms];
            for (var room = 0; room < rooms; room++)
            {
                var state = r.ReadByte();
                if (state > (byte)RoomState.Cleared) throw new FormatException("Unknown room state.");
                run.Rooms[room] = (RoomState)state;
                run.RoomTidyAtTick[room] = r.ReadInt32();
                run.RoomEnemies[room] = ReadIds(r).Select(e => new EnemyId(e)).ToList();
            }
            var checkpoints = ReadCount(r);
            if (checkpoints != layout.Checkpoints.Count) throw new FormatException("The gate has a different number of checkpoints here.");
            run.CheckpointActivated = new bool[checkpoints];
            for (var c = 0; c < checkpoints; c++) run.CheckpointActivated[c] = r.ReadBoolean();
            for (int i = 0, n = ReadCount(r); i < n; i++) run.RevivesUsed[new PlayerId(r.ReadInt32())] = r.ReadInt32();
            for (int i = 0, n = ReadCount(r); i < n; i++) run.Inside.Add((r.ReadInt32(), r.ReadInt32()));
            return run;
        }
    }
}
