using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// A client: predicts its own player by running its own copy of the simulation with its own
    /// commands straight away, and sends each command, numbered, to the host. When a snapshot
    /// arrives it restores the host's world and replays the commands the host has not processed
    /// yet, so the prediction is corrected without losing responsiveness.
    /// <para>
    /// Other players are not predicted: in <see cref="World"/> they stand where the host last put
    /// them. <see cref="DisplayPosition"/> glides them from there to each new snapshot's position
    /// over a snapshot interval, so they walk instead of jumping.
    /// </para>
    /// <para>
    /// <see cref="Events"/> is what to show each tick: this player's own actions (swings, dodges,
    /// skills) as predicted, so they feel instant, and everything else as the host reports it
    /// with each snapshot (hits, kills, level-ups, enemies' attacks, other players' actions).
    /// </para>
    /// </summary>
    public sealed class ClientSession
    {
        private readonly SimulationConfig _config;
        private readonly IClientTransport _transport;
        private readonly List<(int sequence, PlayerCommand command)> _unconfirmed = new List<(int, PlayerCommand)>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private int _sequence;

        private readonly List<SimulationEvent> _events = new List<SimulationEvent>();
        private int _lastHostEventTick;   // host events up to here have been shown (or happened before we joined)

        private sealed class Glide
        {
            public Vec3 From, To;
            public int Progress;

            public Vec3 At(int interval) => From + (To - From) * Math.Min(1f, Progress / (float)interval);
        }

        /// <summary>A jump further than this (a rejoin, a respawn) is shown at once rather than glided.</summary>
        private const float SnapDistance = 2f;

        private readonly Dictionary<PlayerId, Glide> _glides = new Dictionary<PlayerId, Glide>();
        private readonly List<PlayerId> _goneGlides = new List<PlayerId>();
        private int _lastSnapshotTick;
        private int _snapshotInterval = 1;

        public ClientSession(SimulationConfig config, IClientTransport transport, string clientKey)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.Send(Wire.Hello(clientKey));
        }

        /// <summary>The predicted world, or null until the host's welcome arrives.</summary>
        public SimulationWorld World { get; private set; }

        public PlayerId? PlayerId { get; private set; }

        public bool IsJoined => World != null && PlayerId.HasValue;

        /// <summary>False once the connection to the host has closed.</summary>
        public bool IsConnected => _transport.IsConnected;

        /// <summary>Host event lists that could not be read (a build mismatch) and were skipped.</summary>
        public int UnreadableEventLists { get; private set; }

        /// <summary>Why the host turned this client away (the session is full), or null.</summary>
        public string RefusalReason { get; private set; }

        /// <summary>How far the host's corrections have moved the local player, at most (metres): a measure of misprediction.</summary>
        public float LargestCorrection { get; private set; }

        /// <summary>Commands sent but not yet included in a snapshot: roughly the round trip in ticks.</summary>
        public int UnconfirmedCommands => _unconfirmed.Count;

        /// <summary>What to show for the last <see cref="Tick"/>: host events that arrived, then this player's own predicted actions.</summary>
        public IReadOnlyList<SimulationEvent> Events => _events;

        /// <summary>Where to draw a player: this client's own as predicted, others glided between snapshots.</summary>
        public Vec3 DisplayPosition(PlayerId id)
        {
            var current = World.GetPlayer(id).Position;
            if (id == PlayerId || !_glides.TryGetValue(id, out var glide)) return current;
            return glide.At(_snapshotInterval);
        }

        /// <summary>One client tick: apply what arrived, then predict and send this tick's command.</summary>
        public void Tick(PlayerCommand command)
        {
            _events.Clear();
            ReceiveAll();
            if (!IsJoined) return;

            _sequence++;
            _unconfirmed.Add((_sequence, command));
            _transport.Send(Wire.Command(_sequence, command));
            Predict(command);
            ShowOwnActions();

            foreach (var glide in _glides.Values) glide.Progress++;
        }

        public void Disconnect() => _transport.Disconnect();

        private void ReceiveAll()
        {
            SnapshotMessage latest = null;
            while (_transport.TryReceive(out var payload))
            {
                switch (Wire.TypeOf(payload))
                {
                    case MessageType.Welcome:
                        var (you, snapshot) = Wire.ReadWelcome(payload);
                        PlayerId = you;
                        World = SimulationWorld.FromSnapshot(_config, snapshot.World);
                        _unconfirmed.Clear();
                        _sequence = 0;
                        latest = null;
                        _lastHostEventTick = snapshot.Tick; // what happened before we arrived is not ours to show
                        _lastSnapshotTick = snapshot.Tick;
                        _glides.Clear();
                        break;
                    case MessageType.Snapshot when IsJoined:
                        latest = Wire.ReadSnapshot(payload); // only the newest world matters...
                        ShowHostEvents(latest);              // ...but every snapshot's events do
                        break;
                    case MessageType.Refused:
                        RefusalReason = Wire.ReadRefused(payload);
                        break;
                }
            }
            if (latest != null) Reconcile(latest);
        }

        private void Reconcile(SnapshotMessage snapshot)
        {
            var me = PlayerId.Value;
            var before = World.GetPlayer(me).Position;

            World.RestoreSnapshot(snapshot.World);
            snapshot.LastProcessedSequence.TryGetValue(me.Value, out var processed);
            _unconfirmed.RemoveAll(c => c.sequence <= processed);
            foreach (var (_, command) in _unconfirmed) Predict(command);

            LargestCorrection = Math.Max(LargestCorrection, (World.GetPlayer(me).Position - before).Length);
            GlideOthersTowardsSnapshot(snapshot.Tick);
        }

        /// <summary>
        /// Each glide lasts as many ticks as the host ran between this snapshot and the last one,
        /// not as long as the gap between arrivals, so a late packet followed by a quick one does not snap.
        /// </summary>
        private void GlideOthersTowardsSnapshot(int snapshotTick)
        {
            var previousInterval = _snapshotInterval;
            _snapshotInterval = Math.Max(1, snapshotTick - _lastSnapshotTick);
            _lastSnapshotTick = snapshotTick;

            _goneGlides.Clear();
            foreach (var id in _glides.Keys) if (!World.HasPlayer(id)) _goneGlides.Add(id);
            foreach (var id in _goneGlides) _glides.Remove(id);

            foreach (var id in World.PlayerIds)
            {
                if (id == PlayerId) continue;
                var target = World.GetPlayer(id).Position;
                if (!_glides.TryGetValue(id, out var glide))
                {
                    _glides[id] = new Glide { From = target, To = target, Progress = 0 };
                    continue;
                }
                var shown = glide.At(previousInterval);
                glide.From = (target - shown).Length > SnapDistance ? target : shown;
                glide.To = target;
                glide.Progress = 0;
            }
        }

        /// <summary>The host's events, once each, minus its copies of this player's own actions (already shown, predicted).</summary>
        private void ShowHostEvents(SnapshotMessage snapshot)
        {
            List<SimulationEvent> events;
            try
            {
                events = SimulationEventCodec.Decode(snapshot.Events, _config);
            }
            catch (ArgumentException)
            {
                UnreadableEventLists++; // presentation only: the world itself still comes from the snapshot
                _lastHostEventTick = Math.Max(_lastHostEventTick, snapshot.Tick);
                return;
            }
            foreach (var e in events)
            {
                if (e.Tick <= _lastHostEventTick || IsOwnAction(e)) continue;
                _events.Add(e);
            }
            _lastHostEventTick = Math.Max(_lastHostEventTick, snapshot.Tick);
        }

        /// <summary>
        /// The own actions predicted for this tick's new command. Only the step for a new command
        /// counts: replays re-run commands already shown. (Keyed to the command, not the tick number,
        /// which can step back when the host sheds a backlog.)
        /// </summary>
        private void ShowOwnActions()
        {
            foreach (var e in World.Events)
            {
                if (IsOwnAction(e)) _events.Add(e);
            }
        }

        private bool IsOwnAction(SimulationEvent e) => e switch
        {
            AttackPerformed a => a.Player == PlayerId,
            AttackWindupStarted a => a.Player == PlayerId,
            DodgePerformed d => d.Player == PlayerId,
            SkillUsed s => s.Player == PlayerId,
            SkillChargeStarted s => s.Player == PlayerId,
            _ => false,
        };

        private void Predict(PlayerCommand command)
        {
            _commands.Clear();
            _commands[PlayerId.Value] = command;
            World.Step(_commands);
        }
    }
}
