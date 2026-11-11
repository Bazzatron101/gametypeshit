using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The headless gameplay simulation. Each <see cref="Step"/> advances exactly one fixed tick
    /// from the commands given for that tick. It has no dependency on rendering, scene objects
    /// or networking. Presentation reads state from it.
    /// </summary>
    public sealed class SimulationWorld
    {
        private readonly SimulationConfig _config;
        private readonly Dictionary<PlayerId, PlayerState> _players = new Dictionary<PlayerId, PlayerState>();
        private readonly List<PlayerId> _order = new List<PlayerId>();

        public SimulationWorld(SimulationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.TickRate <= 0) throw new ArgumentOutOfRangeException(nameof(config), "TickRate must be positive.");
            TickDelta = 1f / config.TickRate;
        }

        /// <summary>Number of ticks simulated so far.</summary>
        public int Tick { get; private set; }

        /// <summary>Seconds of game time per tick.</summary>
        public float TickDelta { get; }

        /// <summary>Players in the order they joined.</summary>
        public IReadOnlyList<PlayerId> PlayerIds => _order;

        public void AddPlayer(PlayerId id, Vec3 spawnPosition)
        {
            if (_players.ContainsKey(id)) throw new ArgumentException($"{id} is already in the simulation.", nameof(id));
            _players.Add(id, new PlayerState(id, spawnPosition));
            _order.Add(id);
        }

        public PlayerState GetPlayer(PlayerId id) =>
            _players.TryGetValue(id, out var state) ? state : throw new ArgumentException($"{id} is not in the simulation.", nameof(id));

        /// <summary>
        /// Advances one tick. Players with no entry in <paramref name="commands"/> act as if given
        /// <see cref="PlayerCommand.None"/>.
        /// </summary>
        public void Step(IReadOnlyDictionary<PlayerId, PlayerCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            foreach (var id in commands.Keys)
                if (!_players.ContainsKey(id)) throw new ArgumentException($"Command for {id}, who is not in the simulation.", nameof(commands));

            var stepDistance = _config.PlayerMoveSpeed * TickDelta;
            foreach (var id in _order)
            {
                if (!commands.TryGetValue(id, out var cmd)) continue;
                var state = _players[id];
                var move = new Vec3(cmd.MoveX, 0f, cmd.MoveZ) * stepDistance;
                _players[id] = state.WithPosition(state.Position + move);
            }

            Tick++;
        }
    }
}
