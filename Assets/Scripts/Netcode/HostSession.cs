using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// The host: runs the one authoritative <see cref="SimulationWorld"/>. Each tick it takes one
    /// command per player (its own player's directly, remote players' from their queues), steps the
    /// world, and every few ticks sends every client a snapshot with each player's last processed
    /// command number. A client that connects (or reconnects with a known key) is welcomed with its
    /// player id and the whole world: joining and rejoining are the same path (ADR 0003).
    /// </summary>
    public sealed class HostSession
    {
        /// <summary>The spec's party limit. Characters of disconnected players still count: they can rejoin (ADR 0003).</summary>
        public const int MaxPlayers = 10;

        /// <summary>
        /// The most commands a player may have waiting. Beyond this (a stall, or a client whose clock
        /// runs fast) the oldest movement-only commands are dropped, so lag cannot keep growing.
        /// Commands with a press are never dropped.
        /// </summary>
        public const int MaxBacklog = 6;

        private sealed class Remote
        {
            public PlayerId Id;
            public string Key;
            public int Connection = -1;
            public readonly Queue<(int sequence, PlayerCommand command)> Queue = new Queue<(int, PlayerCommand)>();
            public int LastReceived;
            public int LastProcessed;
            public PlayerCommand LastCommand;

            /// <summary>Forgets the old connection's commands: a fresh connection numbers its commands from 1 again.</summary>
            public void ResetConnection(int connection)
            {
                Connection = connection;
                Queue.Clear();
                LastReceived = 0;
                LastProcessed = 0;
                LastCommand = PlayerCommand.None;
            }
        }

        private readonly IHostTransport _transport;
        private readonly Func<PlayerId, Vec3> _spawnPoint;
        private readonly int _snapshotEveryTicks;
        private readonly List<Remote> _remotes = new List<Remote>();
        private readonly Dictionary<int, Remote> _byConnection = new Dictionary<int, Remote>();
        private readonly HashSet<int> _awaitingHello = new HashSet<int>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private readonly List<SimulationEvent> _eventsSinceSnapshot = new List<SimulationEvent>();
        private int _nextPlayerId = 1;
        private int _ticksSinceSnapshot;

        /// <param name="spawnPoint">Where a newly joining player appears (the latest checkpoint, once gates exist).</param>
        public HostSession(SimulationConfig config, IHostTransport transport, Func<PlayerId, Vec3> spawnPoint, int snapshotEveryTicks = 3)
        {
            World = new SimulationWorld(config);
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _spawnPoint = spawnPoint ?? throw new ArgumentNullException(nameof(spawnPoint));
            _snapshotEveryTicks = Math.Max(1, snapshotEveryTicks);
        }

        public SimulationWorld World { get; }

        /// <summary>The host's own player, once added.</summary>
        public PlayerId LocalPlayerId { get; private set; }

        /// <summary>Bytes of the most recent snapshot, for bandwidth measurements.</summary>
        public int LastSnapshotSize { get; private set; }

        public int ConnectedClients => _byConnection.Count;

        /// <summary>Everything sent to clients so far (welcomes, snapshots, refusals), in bytes: the host's upload.</summary>
        public long BytesSent { get; private set; }

        /// <summary>Everything received from clients so far, in bytes.</summary>
        public long BytesReceived { get; private set; }

        /// <summary>Messages from clients that could not be read and were ignored.</summary>
        public int MalformedMessages { get; private set; }

        /// <param name="character">The host's own character from their save; null for a new one.</param>
        public PlayerId AddLocalPlayer(CharacterRecord character = null)
        {
            LocalPlayerId = new PlayerId(_nextPlayerId++);
            World.AddPlayer(LocalPlayerId, _spawnPoint(LocalPlayerId), character ?? CharacterRecord.New());
            return LocalPlayerId;
        }

        /// <summary>One host tick: handle arrivals, step the world, and maybe send a snapshot.</summary>
        public void Tick(PlayerCommand localCommand)
        {
            ReceiveAll();

            _commands.Clear();
            if (World.HasPlayer(LocalPlayerId)) _commands[LocalPlayerId] = localCommand;
            foreach (var remote in _remotes) _commands[remote.Id] = NextCommand(remote);
            World.Step(_commands);
            _eventsSinceSnapshot.AddRange(World.Events);

            if (++_ticksSinceSnapshot >= _snapshotEveryTicks)
            {
                _ticksSinceSnapshot = 0;
                var snapshot = CurrentSnapshot();
                snapshot.Events = SimulationEventCodec.Encode(_eventsSinceSnapshot);
                _eventsSinceSnapshot.Clear();
                var message = Wire.Snapshot(snapshot);
                LastSnapshotSize = message.Length;
                foreach (var connection in _byConnection.Keys) Send(connection, message);
            }
        }

        /// <summary>
        /// The next queued command, or, if none has arrived (loss or a hiccup), the last one's
        /// movement and held keys without any presses: a late packet keeps you walking but never
        /// repeats an attack.
        /// </summary>
        private static PlayerCommand NextCommand(Remote remote)
        {
            if (remote.Queue.Count > 0)
            {
                var (sequence, command) = remote.Queue.Dequeue();
                remote.LastProcessed = sequence;
                remote.LastCommand = command;
                return command;
            }
            var last = remote.LastCommand;
            return new PlayerCommand(last.MoveX, last.MoveZ, heldSkillSlots: last.HeldSkillSlots);
        }

        private void ReceiveAll()
        {
            while (_transport.TryReceive(out var connection, out var kind, out var payload))
            {
                switch (kind)
                {
                    case NetEventKind.Connected:
                        _awaitingHello.Add(connection);
                        break;
                    case NetEventKind.Disconnected:
                        _awaitingHello.Remove(connection);
                        if (_byConnection.TryGetValue(connection, out var gone))
                        {
                            // The character stays in the run, standing still, until its owner rejoins.
                            _byConnection.Remove(connection);
                            gone.ResetConnection(-1);
                        }
                        break;
                    case NetEventKind.Data:
                        BytesReceived += payload?.Length ?? 0;
                        HandleData(connection, payload);
                        break;
                }
            }
        }

        /// <summary>Anything a client sends is untrusted: a message that cannot be read is counted and ignored.</summary>
        private void HandleData(int connection, byte[] payload)
        {
            try
            {
                HandleMessage(connection, payload);
            }
            catch (Exception e) when (e is ArgumentException || e is System.IO.IOException || e is FormatException || e is InvalidCastException)
            {
                MalformedMessages++;
            }
        }

        private void HandleMessage(int connection, byte[] payload)
        {
            switch (Wire.TypeOf(payload))
            {
                case MessageType.Hello when _awaitingHello.Remove(connection):
                    var (claimedKey, character) = Wire.ReadHello(payload);
                    Welcome(connection, claimedKey, character);
                    break;
                case MessageType.Command when _byConnection.TryGetValue(connection, out var remote):
                    var (sequence, command) = Wire.ReadCommand(payload);
                    if (sequence <= remote.LastReceived) return; // stale or duplicate
                    remote.LastReceived = sequence;
                    remote.Queue.Enqueue((sequence, command));
                    ShedBacklog(remote);
                    break;
                case MessageType.Command:
                case MessageType.Hello:
                    break; // a command before a welcome, or a second hello: ignored
                default:
                    MalformedMessages++;
                    break;
            }
        }

        private static void ShedBacklog(Remote remote)
        {
            while (remote.Queue.Count > MaxBacklog && IsMovementOnly(remote.Queue.Peek().command))
            {
                var (sequence, command) = remote.Queue.Dequeue();
                remote.LastProcessed = sequence; // the client stops replaying it
                remote.LastCommand = command;
            }
        }

        private static bool IsMovementOnly(PlayerCommand c) =>
            !c.LightAttack && !c.HeavyAttack && !c.Dodge && !c.UseSkillSlot.HasValue && !c.AllocateStat.HasValue && !c.AssignSkill.HasValue;

        /// <summary>
        /// A new player arrives with the character they sent. A returning one gets back the character
        /// the session kept for them, with what it earned here, unless the one they send is further
        /// along (they played elsewhere or restored a backup in between): then that one replaces it,
        /// so their next save cannot take them backwards.
        /// </summary>
        private void Welcome(int connection, string claimedKey, byte[] characterBytes)
        {
            // An identity the transport vouches for (a SteamID) beats whatever the client claims.
            var key = _transport.IdentityOf(connection) ?? claimedKey;
            var remote = _remotes.Find(r => r.Key == key);
            if (remote == null)
            {
                if (World.PlayerIds.Count >= MaxPlayers)
                {
                    Send(connection, Wire.Refused($"The session is full ({MaxPlayers} players)."));
                    return;
                }
                CharacterRecord character;
                try
                {
                    character = characterBytes.Length > 0 ? CharacterCodec.Decode(characterBytes) : CharacterRecord.New();
                }
                catch (ArgumentException e)
                {
                    // Not let in as a fresh character: the guest's game would then save that over their real one.
                    Send(connection, Wire.Refused("The host could not read your character (are both games the same version?): " + e.Message));
                    return;
                }
                remote = new Remote { Id = new PlayerId(_nextPlayerId++), Key = key };
                _remotes.Add(remote);
                World.AddPlayer(remote.Id, _spawnPoint(remote.Id), character);
            }
            else if (remote.Connection >= 0)
            {
                _byConnection.Remove(remote.Connection); // the same key from a new connection replaces the old one, in place
            }
            else
            {
                World.PlacePlayer(remote.Id, _spawnPoint(remote.Id)); // a returning player: where the party is, not where they left (ADR 0003)
                BringNewerCharacter(remote.Id, characterBytes);
            }

            remote.ResetConnection(connection);
            _byConnection[connection] = remote;
            Send(connection, Wire.Welcome(remote.Id, CurrentSnapshot()));
        }

        private void BringNewerCharacter(PlayerId id, byte[] characterBytes)
        {
            if (characterBytes.Length == 0) return;
            try
            {
                var sent = CharacterCodec.Decode(characterBytes);
                if (sent.IsFurtherAlongThan(World.CaptureCharacter(id))) World.ReplaceCharacter(id, sent);
            }
            catch (ArgumentException)
            {
                MalformedMessages++; // unreadable: the session's copy stands
            }
        }

        private void Send(int connection, byte[] message)
        {
            BytesSent += message.Length;
            _transport.Send(connection, message);
        }

        private SnapshotMessage CurrentSnapshot()
        {
            var snapshot = new SnapshotMessage { Tick = World.Tick, World = World.CaptureSnapshot() };
            foreach (var remote in _remotes) snapshot.LastProcessedSequence[remote.Id.Value] = remote.LastProcessed;
            return snapshot;
        }
    }
}
