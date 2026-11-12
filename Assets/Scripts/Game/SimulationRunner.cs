using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Drives the <see cref="SimulationWorld"/> from the Unity frame loop. Each frame it samples
    /// one command per player, runs as many fixed ticks as real time calls for, and collects the
    /// events those ticks produced. The world itself knows nothing about Unity; this class and
    /// the views are the only bridge.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("The game's content and tuning. Every scene points at the same asset.")]
        private GameContentAsset content;

        private readonly Dictionary<PlayerId, IPlayerCommandSource> _sources = new Dictionary<PlayerId, IPlayerCommandSource>();
        private readonly Dictionary<PlayerId, PlayerCommandBuffer> _buffers = new Dictionary<PlayerId, PlayerCommandBuffer>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private readonly Dictionary<PlayerId, Vec3> _previousPlayerPositions = new Dictionary<PlayerId, Vec3>();
        private readonly Dictionary<EnemyId, Vec3> _previousEnemyPositions = new Dictionary<EnemyId, Vec3>();
        private readonly List<SimulationEvent> _frameEvents = new List<SimulationEvent>();
        private FixedTimestepClock _clock;

        public SimulationWorld World { get; private set; }

        /// <summary>The game's content list (may be null in a bare test scene).</summary>
        public GameContentAsset Content => content;

        /// <summary>Events from every tick run this frame, in order. Read them in LateUpdate.</summary>
        public IReadOnlyList<SimulationEvent> FrameEvents => _frameEvents;

        private void Awake()
        {
            var config = new SimulationConfig(); // defaults apply when a bare test scene has no content
            if (content != null) content.ApplyTo(config);
            World = new SimulationWorld(config);
            _clock = new FixedTimestepClock(config.TickRate);

            foreach (var view in FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
            {
                var id = view.PlayerId;
                var spawn = ToVec3(view.transform.position);
                World.AddPlayer(id, spawn);
                _previousPlayerPositions[id] = spawn;
                _buffers[id] = new PlayerCommandBuffer();
                _sources[id] = view.IsLocal ? new LocalInputCommandSource() : new ConstantCommandSource(PlayerCommand.None);
            }

            foreach (var view in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                var spawn = ToVec3(view.transform.position);
                var id = World.SpawnEnemy(view.CreateDefinition(), spawn);
                _previousEnemyPositions[id] = spawn;
                view.Bind(id);
            }
        }

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
        public void RequestSkillAssignment(PlayerId id, SkillAssignment assignment)
        {
            if (!_buffers.TryGetValue(id, out var buffer))
                throw new System.ArgumentException($"{id} has no input buffer; only players spawned from a PlayerView have a loadout.", nameof(id));
            buffer.QueueSkillAssignment(assignment);
        }

        /// <summary>Replaces where a player's commands come from (tests, bots, network).</summary>
        public void SetCommandSource(PlayerId id, IPlayerCommandSource source)
        {
            World.GetPlayer(id); // throws now, not every frame, if the player is not in the simulation
            _sources[id] = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Spends one of the player's unspent stat points on <paramref name="stat"/>. It goes to the
        /// simulation as a command on the next tick, so the simulation stays the authority.
        /// </summary>
        public void RequestStatAllocation(PlayerId id, Stat stat)
        {
            if (!_buffers.TryGetValue(id, out var buffer))
                throw new System.ArgumentException($"{id} has no input buffer; only players spawned from a PlayerView can spend points.", nameof(id));
            buffer.QueueStatAllocation(stat);
        }

        private void Update()
        {
            _frameEvents.Clear();
            foreach (var pair in _sources) _buffers[pair.Key].Add(pair.Value.Sample());

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                _commands.Clear();
                foreach (var pair in _buffers) _commands[pair.Key] = pair.Value.TakeForTick();

                foreach (var id in World.PlayerIds) _previousPlayerPositions[id] = World.GetPlayer(id).Position;
                foreach (var id in World.EnemyIds) _previousEnemyPositions[id] = World.GetEnemy(id).Position;

                World.Step(_commands);
                _frameEvents.AddRange(World.Events);
            }
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(PlayerId id) =>
            Interpolate(_previousPlayerPositions[id], World.GetPlayer(id).Position);

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(EnemyId id) =>
            Interpolate(_previousEnemyPositions[id], World.GetEnemy(id).Position);

        private Vector3 Interpolate(Vec3 previous, Vec3 current) =>
            Vector3.Lerp(ToVector3(previous), ToVector3(current), _clock.Alpha);

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);
        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
