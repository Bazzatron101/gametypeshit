using System;
using System.Collections.Generic;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Drives the simulation from the Unity frame loop. Each frame it samples input, runs as many
    /// fixed ticks as real time calls for, and collects the events those ticks produced. The world
    /// itself knows nothing about Unity; this class and the views are the only bridge.
    /// <para>
    /// Offline (the default) it runs its own world with the players and enemies placed in the scene.
    /// After <see cref="StartHost"/> it runs the authoritative world of a <see cref="HostSession"/>;
    /// after <see cref="StartClient"/>, the predicted world of a <see cref="ClientSession"/>. Either
    /// way views read <see cref="World"/>, and the local player's input goes in one command per tick.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("The game's content and tuning. Every scene points at the same asset.")]
        private GameContentAsset content;

        [SerializeField, Min(1), Tooltip("Online: the host sends a full snapshot every this many ticks")]
        private int snapshotEveryTicks = 3;

        private readonly Dictionary<PlayerId, IPlayerCommandSource> _sources = new Dictionary<PlayerId, IPlayerCommandSource>();
        private readonly Dictionary<PlayerId, PlayerCommandBuffer> _buffers = new Dictionary<PlayerId, PlayerCommandBuffer>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private readonly Dictionary<PlayerId, Vec3> _previousPlayerPositions = new Dictionary<PlayerId, Vec3>();
        private readonly Dictionary<EnemyId, Vec3> _previousEnemyPositions = new Dictionary<EnemyId, Vec3>();
        private readonly List<SimulationEvent> _frameEvents = new List<SimulationEvent>();
        private SimulationConfig _config;
        private SimulationWorld _offlineWorld;
        private FixedTimestepClock _clock;

        // Online: one local player, whose id is known once the session says so.
        private IPlayerCommandSource _localSource;
        private PlayerCommandBuffer _localBuffer;

        /// <summary>The world views should draw: offline, the host's, or the client's predicted one (null until a client is welcomed).</summary>
        public SimulationWorld World => Host?.World ?? (Client != null ? Client.World : _offlineWorld);

        public HostSession Host { get; private set; }
        public ClientSession Client { get; private set; }
        public bool IsOnline => Host != null || Client != null;

        /// <summary>The player on this machine: the host's own, the client's once welcomed, or null offline.</summary>
        public PlayerId? LocalPlayerId => Host != null ? Host.LocalPlayerId : Client?.PlayerId;

        /// <summary>The game's content list (may be null in a bare test scene).</summary>
        public GameContentAsset Content => content;

        /// <summary>
        /// Events from every tick run this frame, in order. Read them in LateUpdate. On a client these
        /// are its own predicted actions plus everything the host reported (see <see cref="ClientSession.Events"/>).
        /// </summary>
        public IReadOnlyList<SimulationEvent> FrameEvents => _frameEvents;

        private void Awake()
        {
            _config = new SimulationConfig(); // defaults apply when a bare test scene has no content
            if (content != null) content.ApplyTo(_config);
            _offlineWorld = new SimulationWorld(_config);
            _clock = new FixedTimestepClock(_config.TickRate);

            foreach (var view in FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
            {
                var id = view.PlayerId;
                var spawn = ToVec3(view.transform.position);
                _offlineWorld.AddPlayer(id, spawn);
                _previousPlayerPositions[id] = spawn;
                _buffers[id] = new PlayerCommandBuffer();
                _sources[id] = view.IsLocal ? new LocalInputCommandSource() : new ConstantCommandSource(PlayerCommand.None);
            }

            foreach (var view in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                var spawn = ToVec3(view.transform.position);
                var id = _offlineWorld.SpawnEnemy(view.CreateDefinition(), spawn);
                _previousEnemyPositions[id] = spawn;
                view.Bind(id);
            }
        }

        // --- Online ------------------------------------------------------------------------

        /// <summary>Hosts a session over <paramref name="transport"/>. The host's own player joins at once.</summary>
        public void StartHost(IHostTransport transport, Func<PlayerId, Vec3> spawnPoint)
        {
            if (IsOnline) throw new InvalidOperationException("Already online.");
            Host = new HostSession(_config, transport, spawnPoint, snapshotEveryTicks);
            Host.AddLocalPlayer();
            StartLocalInput();
        }

        /// <summary>Joins a session over <paramref name="transport"/>; <paramref name="clientKey"/> identifies this player for rejoining.</summary>
        public void StartClient(IClientTransport transport, string clientKey)
        {
            if (IsOnline) throw new InvalidOperationException("Already online.");
            Client = new ClientSession(_config, transport, clientKey);
            StartLocalInput();
        }

        public void StopOnline()
        {
            Client?.Disconnect();
            Host = null;
            Client = null;
        }

        private void StartLocalInput()
        {
            _localSource = new LocalInputCommandSource();
            _localBuffer = new PlayerCommandBuffer();
            _previousPlayerPositions.Clear();
            _previousEnemyPositions.Clear();
        }

        // --- Offline helpers (the arena, tests) ------------------------------------------------

        /// <summary>Spawns an enemy mid-run with its greybox visual (the test arena; later, gate spawners).</summary>
        public EnemyView SpawnEnemy(EnemyDefinitionAsset definition, Vector3 position, Material bodyMaterial)
        {
            var view = GreyboxVisuals.CreateEnemy(definition != null ? definition.name : "Enemy", position, definition, bodyMaterial);
            var spawn = ToVec3(position);
            var id = World.SpawnEnemy(view.CreateDefinition(), spawn);
            _previousEnemyPositions[id] = spawn;
            view.Bind(id);
            return view;
        }

        /// <summary>Removes an enemy and its visual outright (no defeat, no XP).</summary>
        public void DespawnEnemy(EnemyView view)
        {
            if (view == null) return;
            if (World.HasEnemy(view.Id)) World.DespawnEnemy(view.Id);
            _previousEnemyPositions.Remove(view.Id);
            Destroy(view.gameObject);
        }

        /// <summary>A loadout change for the player, sent to the simulation as a command on the next tick.</summary>
        public void RequestSkillAssignment(PlayerId id, SkillAssignment assignment) => BufferFor(id).QueueSkillAssignment(assignment);

        /// <summary>
        /// Spends one of the player's unspent stat points on <paramref name="stat"/>. It goes to the
        /// simulation as a command on the next tick, so the simulation stays the authority.
        /// </summary>
        public void RequestStatAllocation(PlayerId id, Stat stat) => BufferFor(id).QueueStatAllocation(stat);

        /// <summary>Replaces where a player's commands come from (tests, bots).</summary>
        public void SetCommandSource(PlayerId id, IPlayerCommandSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (IsOnline && id == LocalPlayerId)
            {
                _localSource = source;
                return;
            }
            if (World == null) throw new InvalidOperationException("Not joined yet: there is no world to set a command source in.");
            World.GetPlayer(id); // throws now, not every frame, if the player is not in the simulation
            _sources[id] = source;
        }

        /// <summary>Online: replaces where the local player's commands come from (automated tests, bots).</summary>
        public void SetLocalCommandSource(IPlayerCommandSource source) =>
            _localSource = source ?? throw new ArgumentNullException(nameof(source));

        private PlayerCommandBuffer BufferFor(PlayerId id)
        {
            if (IsOnline && id == LocalPlayerId) return _localBuffer;
            if (_buffers.TryGetValue(id, out var buffer)) return buffer;
            throw new ArgumentException($"{id} has no input buffer on this machine.", nameof(id));
        }

        // --- The frame loop ------------------------------------------------------------------

        private void Update()
        {
            _frameEvents.Clear();
            if (IsOnline) UpdateOnline();
            else UpdateOffline();
        }

        private void UpdateOffline()
        {
            foreach (var pair in _sources) _buffers[pair.Key].Add(pair.Value.Sample());

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                _commands.Clear();
                foreach (var pair in _buffers) _commands[pair.Key] = pair.Value.TakeForTick();
                RememberPositions();
                _offlineWorld.Step(_commands);
                _frameEvents.AddRange(_offlineWorld.Events);
            }
        }

        private void UpdateOnline()
        {
            _localBuffer.Add(_localSource.Sample());

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                RememberPositions();
                var command = _localBuffer.TakeForTick();
                if (Host != null)
                {
                    Host.Tick(command);
                    _frameEvents.AddRange(Host.World.Events);
                }
                else
                {
                    Client.Tick(command);
                    _frameEvents.AddRange(Client.Events);
                }
            }
        }

        private void RememberPositions()
        {
            var world = World;
            if (world == null) return;
            foreach (var id in world.PlayerIds) _previousPlayerPositions[id] = CurrentPosition(id);
            foreach (var id in world.EnemyIds) _previousEnemyPositions[id] = world.GetEnemy(id).Position;
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(PlayerId id)
        {
            var current = CurrentPosition(id);
            return Interpolate(_previousPlayerPositions.TryGetValue(id, out var previous) ? previous : current, current);
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(EnemyId id)
        {
            var current = World.GetEnemy(id).Position;
            return Interpolate(_previousEnemyPositions.TryGetValue(id, out var previous) ? previous : current, current);
        }

        /// <summary>On a client, other players glide between snapshots; everyone else is where the world has them.</summary>
        private Vec3 CurrentPosition(PlayerId id) =>
            Client != null && Client.IsJoined ? Client.DisplayPosition(id) : World.GetPlayer(id).Position;

        private Vector3 Interpolate(Vec3 previous, Vec3 current) =>
            Vector3.Lerp(ToVector3(previous), ToVector3(current), _clock.Alpha);

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);
        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
