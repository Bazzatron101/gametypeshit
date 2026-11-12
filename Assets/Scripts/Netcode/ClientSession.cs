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
    /// Other players are not predicted: between snapshots they stand where the host last put
    /// them. Presentation should interpolate them between snapshots (a later step).
    /// </para>
    /// </summary>
    public sealed class ClientSession
    {
        private readonly SimulationConfig _config;
        private readonly IClientTransport _transport;
        private readonly List<(int sequence, PlayerCommand command)> _unconfirmed = new List<(int, PlayerCommand)>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private int _sequence;

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

        /// <summary>Why the host turned this client away (the session is full), or null.</summary>
        public string RefusalReason { get; private set; }

        /// <summary>How far the host's corrections have moved the local player, at most (metres): a measure of misprediction.</summary>
        public float LargestCorrection { get; private set; }

        /// <summary>Commands sent but not yet included in a snapshot: roughly the round trip in ticks.</summary>
        public int UnconfirmedCommands => _unconfirmed.Count;

        /// <summary>One client tick: apply what arrived, then predict and send this tick's command.</summary>
        public void Tick(PlayerCommand command)
        {
            ReceiveAll();
            if (!IsJoined) return;

            _sequence++;
            _unconfirmed.Add((_sequence, command));
            _transport.Send(Wire.Command(_sequence, command));
            Predict(command);
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
                        break;
                    case MessageType.Snapshot when IsJoined:
                        latest = Wire.ReadSnapshot(payload); // only the newest matters
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
        }

        private void Predict(PlayerCommand command)
        {
            _commands.Clear();
            _commands[PlayerId.Value] = command;
            World.Step(_commands);
        }
    }
}
