using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Online
{
    /// <summary>
    /// Makes this copy's player a bot (ticket 14): a <see cref="BotBrain"/> drives the local
    /// player through the runner's normal input path, so a bot client joins and plays exactly like
    /// a person would. On the host it also keeps a fight going: defeated enemies are cleared and
    /// Goblins and Hobgoblins topped up around the host's player.
    /// <para>
    /// With a result file it measures for the stress test: frame times, bandwidth and the damage
    /// each player dealt, then writes JSON and quits. A host given the number of clients to expect
    /// measures only while that many are connected (so the figures are for a full session), then for
    /// <c>seconds</c>; a client measures for <c>seconds</c> once it has joined, after a short warm-up.
    /// </para>
    /// </summary>
    public sealed class BotPlayer : MonoBehaviour, IPlayerCommandSource
    {
        private const float WarmupSeconds = 3f; // frame times from loading and joining are not the steady state
        private const float GiveUpSeconds = 60f; // waiting for the session to fill or a client to join

        private SimulationRunner _runner;
        private BotBrain _brain;
        private EnemyTopUp _topUp;
        private readonly List<SimulationEvent> _seen = new List<SimulationEvent>();
        private int _checkpointsSeen, _revivesSeen, _bossAttacksSeen;
        private string _rewardSeen = "";

        /// <param name="expectClients">Host only: measure while this many clients are connected (0: from the warm-up on).</param>
        public void Begin(SimulationRunner runner, int seed, float seconds, string resultPath, int expectClients = 0)
        {
            _runner = runner;
            _brain = new BotBrain(seed);
            _runner.SetLocalCommandSource(this);
            if (resultPath != null) StartCoroutine(Measure(seconds, resultPath, expectClients));
        }

        /// <summary>The runner clears its frame events before sampling input, so the bot reads last frame's, kept here.</summary>
        private void LateUpdate()
        {
            if (_runner == null) return;
            _seen.Clear();
            _seen.AddRange(_runner.FrameEvents);
            foreach (var e in _seen)
            {
                // Counted for this copy's whole life, not just the measured window: a checkpoint may fall before it.
                if (e is CheckpointActivated) _checkpointsSeen++;
                if (e is PlayerRevived) _revivesSeen++;
                if (e is BossAttackStarted) _bossAttacksSeen++;
                if (e is GateRewardGranted reward && reward.Player == _runner.LocalPlayerId) _rewardSeen = reward.Modifier?.Id ?? "(pool owned)";
            }
            // In a gate the gate supplies the enemies; outside one, keep a fight going.
            if (_runner.Host != null && !_runner.World.HasGate && Time.frameCount % 30 == 0) TopUpEnemies();
        }

        public PlayerCommand Sample()
        {
            if (_runner.World == null || !_runner.LocalPlayerId.HasValue) return PlayerCommand.None;
            return _brain.Next(_runner.World, _runner.LocalPlayerId.Value, _seen);
        }

        private void TopUpEnemies()
        {
            if (_topUp == null)
            {
                var goblin = _runner.Content?.Enemies.FirstOrDefault(e => e != null && e.name == "Goblin");
                if (goblin == null) return;
                var hobgoblin = _runner.Content.Enemies.FirstOrDefault(e => e != null && e.name == "Hobgoblin") ?? goblin;
                _topUp = new EnemyTopUp(goblin.ToDefinition(), hobgoblin.ToDefinition());
            }
            _topUp.Apply(_runner.World, _runner.World.GetPlayer(_runner.Host.LocalPlayerId).Position);
        }

        [Serializable]
        private sealed class Result
        {
            public string role;
            public bool joined;
            public int localPlayerId;
            public int players;
            public int connectedClients;
            public int expectedClients;
            public bool sessionWasFull;
            public float measuredSeconds;
            public int frames;
            public float frameMsMean, frameMsP99, frameMsMax;
            public int enemiesDefeated;
            public int playerDefeats;
            public long hostBytesSent, hostBytesReceived;
            public int lastSnapshotBytes;
            public float largestCorrection;
            public int unconfirmedCommands;
            public int unreadableEventLists;
            public List<string> damageByPlayer = new List<string>();

            // The gate this copy ended in, as its own world saw it (empty id: none).
            public string gateId = "";
            public bool gateCleared;
            public int roomsCleared, rooms, latestCheckpoint, gateAttempt;
            public int checkpointsSeen, revivesSeen;
            // This copy's own player: shown a reward for the clear, and the modifiers its world says it holds.
            public string rewardSeen = "";
            public List<string> localModifiers = new List<string>();
            public int bossAttacksSeen;
        }

        /// <summary>True once the session is in the state worth measuring: full (host) or joined (client), after a warm-up.</summary>
        private bool Ready(int expectClients, float since) =>
            Time.realtimeSinceStartup - since >= WarmupSeconds &&
            (_runner.Host != null ? _runner.Host.ConnectedClients >= expectClients : _runner.LocalPlayerId.HasValue);

        private IEnumerator Measure(float seconds, string resultPath, int expectClients)
        {
            var launched = Time.realtimeSinceStartup;
            var frames = new List<float>();
            var damage = new SortedDictionary<int, int>();
            int defeated = 0, playerDefeats = 0;
            long bytesSentAtStart = 0, bytesReceivedAtStart = 0, bytesSentAtEnd = 0, bytesReceivedAtEnd = 0;
            var measureStart = 0f;
            var measureEnd = 0f;
            var full = false;
            var roundTrips = new List<int>(); // unconfirmed commands each frame: the round trip in ticks

            // Wait for a full session (or our own join) and a warm-up; give up after a while.
            while (!Ready(expectClients, launched) && Time.realtimeSinceStartup - launched < GiveUpSeconds) yield return null;
            full = Ready(expectClients, launched);
            measureStart = Time.realtimeSinceStartup;
            bytesSentAtStart = _runner.Host?.BytesSent ?? 0;
            bytesReceivedAtStart = _runner.Host?.BytesReceived ?? 0;

            // Measure for the given time, stopping early if the session stops being full.
            while (full && Time.realtimeSinceStartup - measureStart < seconds)
            {
                yield return null;
                if (_runner.Host != null && _runner.Host.ConnectedClients < expectClients) break;
                if (_runner.Client != null && !_runner.Client.IsConnected) break; // the host has finished
                if (_runner.Client != null) roundTrips.Add(_runner.Client.UnconfirmedCommands);
                frames.Add(Time.unscaledDeltaTime * 1000f);
                measureEnd = Time.realtimeSinceStartup;
                bytesSentAtEnd = _runner.Host?.BytesSent ?? 0;
                bytesReceivedAtEnd = _runner.Host?.BytesReceived ?? 0;
                foreach (var e in _runner.FrameEvents)
                {
                    if (e is DamageDealt d) damage[d.Attacker.Value] = (damage.TryGetValue(d.Attacker.Value, out var sum) ? sum : 0) + d.Amount;
                    if (e is EnemyDefeated) defeated++;
                    if (e is PlayerDefeated) playerDefeats++;

                }
            }

            frames.Sort();
            var world = _runner.World;
            var result = new Result
            {
                role = _runner.Host != null ? "host" : "client",
                joined = world != null && _runner.LocalPlayerId.HasValue,
                localPlayerId = _runner.LocalPlayerId?.Value ?? 0,
                players = world?.PlayerIds.Count ?? 0,
                connectedClients = _runner.Host?.ConnectedClients ?? 0,
                expectedClients = expectClients,
                sessionWasFull = full,
                measuredSeconds = frames.Count > 0 ? measureEnd - measureStart : 0f,
                frames = frames.Count,
                frameMsMean = frames.Count > 0 ? frames.Average() : 0f,
                frameMsP99 = frames.Count > 0 ? frames[Math.Min(frames.Count - 1, (int)(frames.Count * 0.99f))] : 0f,
                frameMsMax = frames.Count > 0 ? frames[frames.Count - 1] : 0f,
                enemiesDefeated = defeated,
                playerDefeats = playerDefeats,
                hostBytesSent = frames.Count > 0 ? bytesSentAtEnd - bytesSentAtStart : 0,
                hostBytesReceived = frames.Count > 0 ? bytesReceivedAtEnd - bytesReceivedAtStart : 0,
                lastSnapshotBytes = _runner.Host?.LastSnapshotSize ?? 0,
                largestCorrection = _runner.Client?.LargestCorrection ?? 0f,
                unconfirmedCommands = roundTrips.Count > 0 ? roundTrips.OrderBy(t => t).ElementAt(roundTrips.Count / 2) : 0, // median
                unreadableEventLists = _runner.Client?.UnreadableEventLists ?? 0,
                damageByPlayer = damage.Select(pair => $"{pair.Key}:{pair.Value}").ToList(),
            };
            var gate = world?.Gate;
            if (gate != null)
            {
                result.gateId = gate.Definition.Id;
                result.gateCleared = gate.IsCleared;
                result.rooms = gate.Layout.Rooms.Count;
                for (var room = 0; room < result.rooms; room++) if (gate.RoomState(room) == RoomState.Cleared) result.roomsCleared++;
                result.latestCheckpoint = gate.LatestCheckpoint;
                result.gateAttempt = gate.Attempt;
            }
            result.checkpointsSeen = _checkpointsSeen;
            result.rewardSeen = _rewardSeen;
            result.bossAttacksSeen = _bossAttacksSeen;
            if (world != null && _runner.LocalPlayerId.HasValue)
                foreach (var m in world.GetPlayer(_runner.LocalPlayerId.Value).ActiveModifiers) result.localModifiers.Add(m.Id);
            result.revivesSeen = _revivesSeen;
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, prettyPrint: true));
            Application.Quit();
        }
    }
}
