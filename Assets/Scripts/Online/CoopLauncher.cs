using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing;
using HunterSystem.Game;
using HunterSystem.Netcode;
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
    /// Steam's "Join Game" passes <c>+connect STEAMID</c>, which joins over Steam; a copy already
    /// running at the menu (or disconnected) gets the same request from Steam and joins straight away.
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

        /// <summary>Steam's test app. The game needs its own app ID before release.</summary>
        private const uint SteamAppId = 480;

        private SimulationRunner _runner;
        private NetworkManager _network;
        private bool _onSteam;
        private Role _role = Role.None;
        private string _address = "";
        private string _joinedAddress, _playerKey;
        private bool _steamReady, _weStartedSteam;
        private string _status = "Not connected.";
        private GUIStyle _label, _header;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            StartSteam();
            RunFromCommandLine();
        }

        /// <summary>
        /// Starts Steam at the menu, so a friend's invite or "Join Game" reaches this copy before any
        /// session exists. FishyFacepunch uses it as it is. Without Steam running, only local play is offered.
        /// </summary>
        private void StartSteam()
        {
            try
            {
                if (!SteamClient.IsValid)
                {
                    SteamClient.Init(SteamAppId, asyncCallbacks: true);
                    _weStartedSteam = true;
                }
                _steamReady = true;
                SteamFriends.OnGameRichPresenceJoinRequested += OnSteamJoinRequested;
            }
            catch (Exception e)
            {
                _steamReady = false;
                _status = "Steam is not available (" + e.Message + "). Local play only.";
            }
        }

        private void OnSteamJoinRequested(Friend friend, string connect)
        {
            var host = ParseConnect(connect);
            if (host == null) return;
            try
            {
                if (_network == null) Join(steam: true, host);
                else if (CanRejoin && _onSteam) Rejoin(host);
                else _status = "Already in a session; ignored a join to " + friend.Name + ".";
            }
            catch (Exception e)
            {
                _status = "Could not join " + friend.Name + ": " + e.Message;
            }
        }

        /// <summary>The host's SteamID from rich presence's <c>+connect ID</c>.</summary>
        private static string ParseConnect(string connect)
        {
            var parts = (connect ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var i = Array.IndexOf(parts, "+connect");
            var id = i >= 0 && i + 1 < parts.Length ? parts[i + 1] : null;
            return id != null && ulong.TryParse(id, out _) ? id : null;
        }

        private void OnDestroy()
        {
            if (_steamReady) SteamFriends.OnGameRichPresenceJoinRequested -= OnSteamJoinRequested;
        }

        private void OnApplicationQuit()
        {
            if (_weStartedSteam && SteamClient.IsValid) SteamClient.Shutdown();
        }

        // --- Hosting and joining ------------------------------------------------------------

        public void Host(bool steam)
        {
            UseNetwork(steam);
            _role = Role.Host;
            var transport = new FishNetHostTransport(_network, addressIsIdentity: steam);
            _network.ServerManager.StartConnection();
            _runner.StartHost(transport, id => JoinPoint.BesideParty(_runner.World, id, fallback: Vec3.Zero));
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
            // On Steam the host identifies us by our verified SteamID anyway; locally, the key lets us rejoin as the same character.
            _playerKey = steam ? "steam:" + SteamClient.SteamId : playerKey ?? $"local-{Guid.NewGuid():N}";
            Connect(string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim());
        }

        /// <summary>True when this copy was a client and its connection has closed: it can rejoin without restarting.</summary>
        public bool CanRejoin => _role == Role.Client && _runner.Client != null && !_rejoining &&
                                 (!_runner.Client.IsConnected || _runner.Client.RefusalReason != null);

        private bool _rejoining;

        /// <summary>Reconnects to <paramref name="address"/> (default: the same host). The host gives back the same character, beside the party.</summary>
        public void Rejoin(string address = null)
        {
            if (!CanRejoin) return;
            StartCoroutine(RejoinWhenStopped(address ?? _joinedAddress));
        }

        /// <summary>Fish-Net ignores a start while the old connection is still stopping, so wait for it first.</summary>
        private IEnumerator RejoinWhenStopped(string address)
        {
            _rejoining = true;
            _runner.StopOnline();
            _status = "Rejoining...";
            var giveUpAt = Time.realtimeSinceStartup + 5f;
            while (_network.ClientManager.Started && Time.realtimeSinceStartup < giveUpAt) yield return null;
            yield return null;
            _rejoining = false;
            Connect(address);
        }

        private void Connect(string address)
        {
            _joinedAddress = address;
            var transport = new FishNetClientTransport(_network);
            _network.ClientManager.StartConnection(address);
            _runner.StartClient(transport, _playerKey);
            _status = _onSteam ? $"Joining {address} over Steam..." : $"Joining {address} locally...";
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
            if (steam && !_steamReady) throw new InvalidOperationException("Steam is not available.");
            chosen.SetActive(true); // Awake initialises Fish-Net (Steam is already running, see StartSteam)
            _network = chosen.GetComponent<NetworkManager>();
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
            if ((steam || connect != null) && !_steamReady) return; // the status already says Steam is unavailable
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
            public int targetId;
            /// <summary>Damage to the shared target this copy was shown, per attacker: "playerId:total".</summary>
            public List<string> damageByPlayer = new List<string>();
        }

        private const string TargetName = "Target";

        /// <summary>
        /// A scripted fight: the host puts one tough, harmless target beside itself; both players walk
        /// up to it and attack it for a while, then stand still so prediction and the host must agree.
        /// Each copy tallies the damage it was shown per attacker, writes what it saw and quits.
        /// </summary>
        private IEnumerator AutoTest(float seconds, string resultPath)
        {
            var start = Time.time;
            var defeated = 0;
            var damage = new SortedDictionary<int, int>();
            if (_role == Role.Host)
            {
                foreach (var id in _runner.World.EnemyIds.ToList()) _runner.World.DespawnEnemy(id);
                var at = _runner.World.GetPlayer(_runner.Host.LocalPlayerId).Position + new Vec3(0f, 0f, 3f);
                _runner.World.SpawnEnemy(new EnemyDefinition { Name = TargetName, MaxHealth = 1000000, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f }, at);
            }

            var fightUntil = start + seconds * 0.6f;
            _runner.SetLocalCommandSource(new ScriptedSource(() => Time.time < fightUntil ? WalkUpAndAttack() : PlayerCommand.None));

            while (Time.time - start < seconds)
            {
                foreach (var e in _runner.FrameEvents)
                {
                    if (e is EnemyDefeated) defeated++;
                    if (e is DamageDealt d && d.Target == FindTarget())
                        damage[d.Attacker.Value] = (damage.TryGetValue(d.Attacker.Value, out var sum) ? sum : 0) + d.Amount;
                }
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
                targetId = FindTarget()?.Value ?? 0,
                damageByPlayer = damage.Select(pair => $"{pair.Key}:{pair.Value}").ToList(),
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

        private EnemyId? FindTarget()
        {
            var world = _runner.World;
            if (world == null) return null;
            foreach (var id in world.EnemyIds) if (world.GetEnemy(id).Definition.Name == TargetName) return id;
            return null;
        }

        private int _lastSwing = -1;

        /// <summary>Walk to the target, then swing at it three times a second.</summary>
        private PlayerCommand WalkUpAndAttack()
        {
            var world = _runner.World;
            var target = FindTarget();
            if (world == null || !_runner.LocalPlayerId.HasValue || target == null) return PlayerCommand.None;
            var offset = world.GetEnemy(target.Value).Position - world.GetPlayer(_runner.LocalPlayerId.Value).Position;
            var distance = offset.Length;
            const float reach = 1.2f, faceOnly = 0.01f; // close enough for a light attack; a nudge that turns without walking
            var swing = (int)(Time.time * 3f) != _lastSwing;
            if (swing) _lastSwing = (int)(Time.time * 3f);
            if (distance > reach) return new PlayerCommand(offset.X / distance, offset.Z / distance);
            if (distance < 1e-3f) return new PlayerCommand(0f, 0f, lightAttack: swing);
            return new PlayerCommand(offset.X / distance * faceOnly, offset.Z / distance * faceOnly, lightAttack: swing);
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
                GUI.enabled = _steamReady;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host on Steam")) Host(steam: true);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Host SteamID:", _label, GUILayout.Width(100));
                _address = GUILayout.TextField(_address, GUILayout.Width(170));
                if (GUILayout.Button("Join on Steam")) Join(steam: true, _address);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
                GUILayout.Label(_steamReady
                    ? "Or: accept a friend's Steam invite, or right-click them in your friends list > Join Game."
                    : _status, _label);
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
                    if (_onSteam && GUILayout.Button("Invite friends")) SteamFriends.OpenOverlay("friends"); // right-click a friend > Invite to Game
                    if (_onSteam && GUILayout.Button("Copy my SteamID")) GUIUtility.systemCopyBuffer = SteamClient.SteamId.ToString();
                    GUILayout.EndHorizontal();
                }
                if (_runner.Client != null)
                {
                    var tickMs = world != null ? world.TickDelta * 1000f : 16.7f;
                    GUILayout.Label($"Round trip ~{_runner.Client.UnconfirmedCommands * tickMs:0} ms   " +
                                    $"Largest correction: {_runner.Client.LargestCorrection:0.00} m", _label);
                    if (CanRejoin && GUILayout.Button("Rejoin")) Rejoin();
                }
            }
            GUILayout.EndArea();
        }

        private string CurrentStatus()
        {
            var client = _runner.Client;
            if (client == null) return _status;
            if (client.RefusalReason != null) return "Turned away: " + client.RefusalReason;
            if (!client.IsConnected) return "Disconnected from the host. Your character waits in the run: press Rejoin.";
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
