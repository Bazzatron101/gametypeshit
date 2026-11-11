using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Drives the <see cref="SimulationWorld"/> from the Unity frame loop. Each frame it samples
    /// one command per player and runs as many fixed ticks as real time calls for. The world
    /// itself knows nothing about Unity; this class and <see cref="PlayerView"/> are the
    /// only bridge.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField, Min(1)] private int tickRate = 60;
        [SerializeField, Min(0f)] private float playerMoveSpeed = 5f;

        private readonly Dictionary<PlayerId, IPlayerCommandSource> _sources = new Dictionary<PlayerId, IPlayerCommandSource>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private readonly Dictionary<PlayerId, Vec3> _previousPositions = new Dictionary<PlayerId, Vec3>();
        private FixedTimestepClock _clock;

        public SimulationWorld World { get; private set; }

        private void Awake()
        {
            World = new SimulationWorld(new SimulationConfig { TickRate = tickRate, PlayerMoveSpeed = playerMoveSpeed });
            _clock = new FixedTimestepClock(tickRate);

            foreach (var view in FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
            {
                var id = view.PlayerId;
                var spawn = ToVec3(view.transform.position);
                World.AddPlayer(id, spawn);
                _previousPositions[id] = spawn;
                _sources[id] = view.IsLocal ? new LocalInputCommandSource() : new ConstantCommandSource(PlayerCommand.None);
            }
        }

        /// <summary>Replaces where a player's commands come from (tests, bots, network).</summary>
        public void SetCommandSource(PlayerId id, IPlayerCommandSource source)
        {
            World.GetPlayer(id); // throws now, not every frame, if the player is not in the simulation
            _sources[id] = source ?? throw new System.ArgumentNullException(nameof(source));
        }

        private void Update()
        {
            _commands.Clear();
            foreach (var pair in _sources) _commands[pair.Key] = pair.Value.Sample();

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                foreach (var id in World.PlayerIds) _previousPositions[id] = World.GetPlayer(id).Position;
                World.Step(_commands);
            }
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(PlayerId id)
        {
            var previous = ToVector3(_previousPositions[id]);
            var current = ToVector3(World.GetPlayer(id).Position);
            return Vector3.Lerp(previous, current, _clock.Alpha);
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);
        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
