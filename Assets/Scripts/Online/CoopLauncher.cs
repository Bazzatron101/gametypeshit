using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing;
using HunterSystem.Game;
using HunterSystem.Simulation;
using Steamworks;
using UnityEngine;

namespace HunterSystem.Online
{
    /// <summary>
    /// The co-op spike's front door: host or join a session, locally (Fish-Net's Tugboat over
    /// UDP, for two copies on one PC) or over Steam (FishyFacepunch through the Steam relay, test
    /// app 480). The host can spawn Goblins; a stats panel shows what the netcode is doing.
    /// <para>
    /// The same can be done from the command line, which is how two built copies are tested
    /// without anyone at the keyboard:
    /// <c>-coop host|join -transport local|steam [-address IP-or-SteamID] [-player-key K] [-autotest SECONDS FILE]</c>.
    /// Steam's "Join Game" passes <c>+connect STEAMID</c>, which joins over Steam.
    /// </para>
    /// </summary>
    public sealed class CoopLauncher : MonoBehaviour
    {
        private enum Role
        {
            None,
            Host,
            Client,
        }

        [SerializeField, Tooltip("Inactive object with a NetworkManager and Tugboat (local UDP)")] private GameObject localNetwork;
        [SerializeField, Tooltip("Inactive object with a NetworkManager and FishyFacepunch (Steam)")] private GameObject steamNetwork;
        [SerializeField, Min(1f)] private float spawnRingRadius = 7f;

        private SimulationRunner _runner;
        private NetworkManager _network;
        private bool _onSteam;
        private Role _role = Role.None;
        private string _address = "";
        private string _status = "Not connected.";
        private GUIStyle _label, _header;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            RunFromCommandLine();
        }

        // --- Hosting and joining ------------------------------------------------------------

        public void Host(bool steam)
        {
            UseNetwork(steam);
            _role = Role.Host;
            var transport = new FishNetHostTransport(_network, addressIsIdentity: steam);
            _network.ServerManager.StartConnection();
            _runner.StartHost(transport, id => new Vec3(1.5f * (id.Value - 1), 0f, 0f));
            if (steam)
            {
                // Friends can now use "Join Game" from the Steam friends list: Steam launches the
                // game with this value on its command line, which RunFromCommandLine reads.
                SteamFriends.SetRichPresence("connect", "+connect " + SteamClient.SteamId);
                _status = $"Hosting on Steam as {SteamClient.SteamId}.";
            }
            else
            {
                _status = "Hosting locally (UDP 7770).";
            }
            SpawnGoblins(3);
        }

        public void Join(bool steam, string address, string playerKey = null)
        {
            UseNetwork(steam);
            _role = Role.Client;
            var transport = new FishNetClientTransport(_network);
            _network.ClientManager.StartConnection(string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim());
            // On Steam the host identifies us by our verified SteamID anyway; locally, pass -player-key to be able to rejoin.
            var key = steam ? "steam:" + SteamClient.SteamId : playerKey ?? $"local-{Guid.NewGuid():N}";
            _runner.StartClient(transport, key);
            _status = steam ? $"Joining {address} over Steam..." : $"Joining {address} locally...";
        }

        /// <summary>Host only: puts Goblins in a ring around the host's player.</summary>
        public void SpawnGoblins(int count) => SpawnEnemies("Goblin", count);

        public void SpawnEnemies(string enemyName, int count)
        {
            if (_runner.Host == null || _runner.Content == null) return;
            var definition = _runner.Content.Enemies.FirstOrDefault(e => e != null && e.name == enemyName);
            if (definition == null) return;
            var centre = _runner.World.GetPlayer(_runner.Host.LocalPlayerId).Position;
            for (var i = 0; i < count; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI * 2f / count;
                var at = centre + new Vec3(Mathf.Sin(angle) * spawnRingRadius, 0f, Mathf.Cos(angle) * spawnRingRadius);
                _runner.World.SpawnEnemy(definition.ToDefinition(), at);
            }
        }

        private void UseNetwork(bool steam)
        {
            if (_network != null) throw new InvalidOperationException("A session is already running.");
            _onSteam = steam;
            var chosen = steam ? steamNetwork : localNetwork;
            chosen.SetActive(true); // Awake initialises Fish-Net and, for Steam, the Steam client
            _network = chosen.GetComponent<NetworkManager>();
            if (steam) SteamFriends.OnGameRichPresenceJoinRequested += (_, connect) => _status = $"Already in a session; ignored a join to {connect}.";
        }

        // --- Command line and the automated test ------------------------------------------------

        private void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, int offset = 1)
            {
                var i = Array.IndexOf(args, name);
                return i >= 0 && i + offset < args.Length ? args[i + offset] : null;
            }

            var steam = Arg("-transport") == "steam";
            var connect = Arg("+connect");
            if (connect != null) Join(steam: true, connect);
            else if (Arg("-coop") == "host") Host(steam);
            else if (Arg("-coop") == "join") Join(steam, Arg("-address") ?? "localhost", Arg("-player-key"));

            if (Arg("-autotest") != null && float.TryParse(Arg("-autotest"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                StartCoroutine(AutoTest(seconds, Arg("-autotest", 2) ?? "coop-autotest.json"));
        }

        [Serializable]
        private sealed class AutoTestResult
        {
            public string role;
            public bool joined;
            public int localPlayerId;
            public int players;
            public int enemies;
            public int enemiesDefeated;
            public float localX, localZ;
            public float largestCorrection;
            public int unconfirmedCommands;
            public int lastSnapshotBytes;
            public int connectedClients;
            public List<string> playerPositions = new List<string>();
            public string transport;
        }

        /// <summary>
        /// A scripted session: a client walks east while attacking for a while, then stands still
        /// so prediction and the host must agree; the host just runs. Each writes what it saw and quits.
        /// </summary>
        private IEnumerator AutoTest(float seconds, string resultPath)
        {
            var start = Time.time;
            var defeated = 0;
            if (_role == Role.Client)
            {
                var walkUntil = start + seconds * 0.4f;
                _runner.SetLocalCommandSource(new ScriptedSource(() => Time.time < walkUntil
                    ? new PlayerCommand(1f, 0.3f, lightAttack: Time.frameCount % 20 == 0)
                    : PlayerCommand.None));
            }

            while (Time.time - start < seconds)
            {
                foreach (var e in _runner.FrameEvents) if (e is EnemyDefeated) defeated++;
                yield return null;
            }

            var world = _runner.World;
            var result = new AutoTestResult
            {
                role = _role.ToString().ToLowerInvariant(),
                transport = _onSteam ? "steam" : "local",
                joined = world != null && _runner.LocalPlayerId.HasValue,
                localPlayerId = _runner.LocalPlayerId?.Value ?? 0,
                players = world?.PlayerIds.Count ?? 0,
                enemies = world?.EnemyIds.Count ?? 0,
                enemiesDefeated = defeated,
                largestCorrection = _runner.Client?.LargestCorrection ?? 0f,
                unconfirmedCommands = _runner.Client?.UnconfirmedCommands ?? 0,
                lastSnapshotBytes = _runner.Host?.LastSnapshotSize ?? 0,
                connectedClients = _runner.Host?.ConnectedClients ?? 0,
            };
            if (world != null)
            {
                if (_runner.LocalPlayerId.HasValue)
                {
                    var me = world.GetPlayer(_runner.LocalPlayerId.Value).Position;
                    (result.localX, result.localZ) = (me.X, me.Z);
                }
                foreach (var id in world.PlayerIds)
                {
                    var p = world.GetPlayer(id).Position;
                    result.playerPositions.Add(FormattableString.Invariant($"{id.Value}:{p.X:0.0000},{p.Z:0.0000}"));
                }
            }
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, prettyPrint: true));
            Application.Quit();
        }

        private sealed class ScriptedSource : IPlayerCommandSource
        {
            private readonly Func<PlayerCommand> _next;
            public ScriptedSource(Func<PlayerCommand> next) => _next = next;
            public PlayerCommand Sample() => _next();
        }

        // --- Menu and stats --------------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16, Screen.height - 230, 420, 214), GUI.skin.box);
            GUILayout.Label("CO-OP (netcode spike)", _header);

            if (_network == null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host locally")) Host(steam: false);
                if (GUILayout.Button("Join locally")) Join(steam: false, "localhost");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host on Steam")) Host(steam: true);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Host SteamID:", _label, GUILayout.Width(100));
                _address = GUILayout.TextField(_address, GUILayout.Width(170));
                if (GUILayout.Button("Join on Steam")) Join(steam: true, _address);
                GUILayout.EndHorizontal();
                GUILayout.Label("Or: in Steam, right-click the host in your friends list > Join Game.", _label);
            }
            else
            {
                GUILayout.Label(CurrentStatus(), _label);
                var world = _runner.World;
                GUILayout.Label($"Role: {_role}   You: {(_runner.LocalPlayerId.HasValue ? "Player " + _runner.LocalPlayerId.Value.Value : "joining...")}   " +
                                $"Players: {world?.PlayerIds.Count ?? 0}   Enemies: {world?.EnemyIds.Count ?? 0}", _label);
                if (_runner.Host != null)
                {
                    GUILayout.Label($"Clients: {_runner.Host.ConnectedClients}   Snapshot: {_runner.Host.LastSnapshotSize} bytes", _label);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Spawn 3 Goblins")) SpawnGoblins(3);
                    if (GUILayout.Button("Spawn a Hobgoblin")) SpawnEnemies("Hobgoblin", 1);
                    if (_onSteam && GUILayout.Button("Copy my SteamID")) GUIUtility.systemCopyBuffer = SteamClient.SteamId.ToString();
                    GUILayout.EndHorizontal();
                }
                if (_runner.Client != null)
                {
                    var tickMs = world != null ? world.TickDelta * 1000f : 16.7f;
                    GUILayout.Label($"Round trip ~{_runner.Client.UnconfirmedCommands * tickMs:0} ms   " +
                                    $"Largest correction: {_runner.Client.LargestCorrection:0.00} m", _label);
                }
            }
            GUILayout.EndArea();
        }

        private string CurrentStatus()
        {
            var client = _runner.Client;
            if (client == null) return _status;
            if (client.RefusalReason != null) return "Turned away: " + client.RefusalReason;
            if (!client.IsConnected) return "Disconnected from the host. Restart to rejoin; your character waits in the run.";
            return client.IsJoined ? "Connected." : _status;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        }
    }
}
