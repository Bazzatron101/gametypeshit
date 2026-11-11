using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The headless gameplay simulation. Each <see cref="Step"/> advances exactly one fixed tick
    /// from the commands given for that tick, and records what happened in <see cref="Events"/>.
    /// It has no dependency on rendering, scene objects or networking. Presentation reads from it.
    /// </summary>
    public sealed class SimulationWorld
    {
        /// <summary>Distances and input lengths below this count as zero.</summary>
        private const float Epsilon = 1e-4f;
        private static readonly Vec3 DefaultFacing = new Vec3(0f, 0f, 1f);

        private sealed class Player
        {
            public PlayerId Id;
            public Vec3 Position;
            public Vec3 Facing;
            public int AttackReadyAtTick;
        }

        private sealed class Enemy
        {
            public EnemyId Id;
            public EnemyDefinition Definition;
            public Vec3 Position;
            public int Health;
        }

        private readonly SimulationConfig _config;
        private readonly int _lightAttackCooldownTicks;
        private readonly float _lightAttackMinCos;

        // Lists keep iteration order fixed (join/spawn order) so every run resolves the same way.
        private readonly List<Player> _players = new List<Player>();
        private readonly Dictionary<PlayerId, Player> _playersById = new Dictionary<PlayerId, Player>();
        private readonly List<PlayerId> _playerIds = new List<PlayerId>();
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly Dictionary<EnemyId, Enemy> _enemiesById = new Dictionary<EnemyId, Enemy>();
        private readonly List<EnemyId> _enemyIds = new List<EnemyId>();
        private readonly List<SimulationEvent> _events = new List<SimulationEvent>();
        private int _nextEnemyId = 1;

        public SimulationWorld(SimulationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.TickRate <= 0) throw new ArgumentOutOfRangeException(nameof(config), "TickRate must be positive.");
            if (config.LightAttack == null) throw new ArgumentException("LightAttack must be set.", nameof(config));
            TickDelta = 1f / config.TickRate;
            // Round up to whole ticks, but not when float error leaves a hair over a whole number (0.1 * 60 = 6.0000005).
            _lightAttackCooldownTicks = (int)MathF.Ceiling(config.LightAttack.CooldownSeconds * config.TickRate - Epsilon);
            _lightAttackMinCos = MathF.Cos(config.LightAttack.ArcDegrees * 0.5f * MathF.PI / 180f);
        }

        /// <summary>Number of ticks simulated so far.</summary>
        public int Tick { get; private set; }

        /// <summary>Seconds of game time per tick.</summary>
        public float TickDelta { get; }

        /// <summary>Players in the order they joined.</summary>
        public IReadOnlyList<PlayerId> PlayerIds => _playerIds;

        /// <summary>Enemies in the order they spawned, defeated ones included.</summary>
        public IReadOnlyList<EnemyId> EnemyIds => _enemyIds;

        /// <summary>What happened during the most recent <see cref="Step"/>, in order.</summary>
        public IReadOnlyList<SimulationEvent> Events => _events;

        public void AddPlayer(PlayerId id, Vec3 spawnPosition)
        {
            if (_playersById.ContainsKey(id)) throw new ArgumentException($"{id} is already in the simulation.", nameof(id));
            var player = new Player { Id = id, Position = spawnPosition, Facing = DefaultFacing };
            _players.Add(player);
            _playersById.Add(id, player);
            _playerIds.Add(id);
        }

        public PlayerState GetPlayer(PlayerId id)
        {
            var p = FindPlayer(id);
            return new PlayerState(p.Id, p.Position, p.Facing);
        }

        public EnemyId SpawnEnemy(EnemyDefinition definition, Vec3 position)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var id = new EnemyId(_nextEnemyId++);
            var enemy = new Enemy { Id = id, Definition = definition, Position = position, Health = definition.MaxHealth };
            _enemies.Add(enemy);
            _enemiesById.Add(id, enemy);
            _enemyIds.Add(id);
            return id;
        }

        public EnemyState GetEnemy(EnemyId id)
        {
            if (!_enemiesById.TryGetValue(id, out var e)) throw new ArgumentException($"{id} is not in the simulation.", nameof(id));
            return new EnemyState(e.Id, e.Definition, e.Position, e.Health);
        }

        /// <summary>
        /// Advances one tick: players move, then attack, then enemies chase. Players with no entry
        /// in <paramref name="commands"/> act as if given <see cref="PlayerCommand.None"/>.
        /// </summary>
        public void Step(IReadOnlyDictionary<PlayerId, PlayerCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            foreach (var id in commands.Keys)
                if (!_playersById.ContainsKey(id)) throw new ArgumentException($"Command for {id}, who is not in the simulation.", nameof(commands));

            _events.Clear();
            var tick = Tick + 1;

            foreach (var player in _players)
                if (commands.TryGetValue(player.Id, out var cmd)) MovePlayer(player, cmd);

            foreach (var player in _players)
                if (commands.TryGetValue(player.Id, out var cmd) && cmd.LightAttack) TryLightAttack(player, tick);

            foreach (var enemy in _enemies)
                if (enemy.Health > 0) Chase(enemy);

            Tick = tick;
        }

        private Player FindPlayer(PlayerId id) =>
            _playersById.TryGetValue(id, out var p) ? p : throw new ArgumentException($"{id} is not in the simulation.", nameof(id));

        private void MovePlayer(Player player, PlayerCommand cmd)
        {
            var input = new Vec3(cmd.MoveX, 0f, cmd.MoveZ);
            var amount = input.Length;
            if (amount < Epsilon) return;

            player.Position += input * (_config.PlayerMoveSpeed * TickDelta);
            player.Facing = input * (1f / amount);
        }

        private void TryLightAttack(Player player, int tick)
        {
            if (tick < player.AttackReadyAtTick) return;
            player.AttackReadyAtTick = tick + _lightAttackCooldownTicks;
            _events.Add(new AttackPerformed(tick, player.Id));

            var attack = _config.LightAttack;
            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0 || !InAttackCone(player, enemy.Position, attack.Range)) continue;

                var amount = Math.Min(attack.Damage, enemy.Health);
                enemy.Health -= amount;
                _events.Add(new DamageDealt(tick, player.Id, enemy.Id, amount, enemy.Health));
                if (enemy.Health == 0) _events.Add(new EnemyDefeated(tick, enemy.Id, player.Id));
            }
        }

        private bool InAttackCone(Player player, Vec3 target, float range)
        {
            var offset = Flat(target - player.Position);
            var distance = offset.Length;
            if (distance > range) return false;
            if (distance < Epsilon) return true; // standing on top of it counts as a hit
            var cos = (offset.X * player.Facing.X + offset.Z * player.Facing.Z) / distance;
            return cos >= _lightAttackMinCos - 1e-6f;
        }

        private void Chase(Enemy enemy)
        {
            Player target = null;
            var bestDistance = float.MaxValue;
            foreach (var player in _players)
            {
                var d = Flat(player.Position - enemy.Position).Length;
                if (d < bestDistance) { bestDistance = d; target = player; }
            }
            if (target == null) return;

            var gap = bestDistance - enemy.Definition.StopDistance;
            if (gap <= 0f) return;

            var step = MathF.Min(enemy.Definition.MoveSpeed * TickDelta, gap);
            var direction = Flat(target.Position - enemy.Position) * (1f / bestDistance);
            enemy.Position += direction * step;
        }

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);
    }
}
