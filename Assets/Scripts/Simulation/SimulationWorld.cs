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
            public int Health;
            public PlayerLifeState LifeState;
            public int AttackReadyAtTick;

            // Tick bounds below are exclusive ends: "until tick N" means up to and including N - 1.
            public int DodgeReadyAtTick;
            public int DodgeEndsAtTick;
            public int InvulnerableUntilTick;
            public Vec3 DodgeDirection;

            public bool IsAlive => LifeState == PlayerLifeState.Alive;
            public bool IsDodging(int tick) => IsAlive && tick < DodgeEndsAtTick;
            public bool IsInvulnerable(int tick) => IsAlive && tick < InvulnerableUntilTick;

            /// <summary>
            /// The one place player health goes down. Returns the health actually removed; sets
            /// <paramref name="defeated"/> when this hit took the player to zero. Death rules hook in here.
            /// </summary>
            public int TakeDamage(int damage, out bool defeated)
            {
                var amount = Math.Min(Math.Max(0, damage), Health);
                Health -= amount;
                defeated = amount > 0 && Health == 0;
                if (defeated) LifeState = PlayerLifeState.Defeated;
                return amount;
            }
        }

        private sealed class Enemy
        {
            public EnemyId Id;
            public EnemyDefinition Definition;
            public int WindupTicks;
            public int AttackCooldownTicks;
            public Vec3 Position;
            public int Health;
            public int AttackReadyAtTick;

            /// <summary>0 when not winding up.</summary>
            public int AttackLandsAtTick;
            public Player AttackTarget;
        }

        private readonly SimulationConfig _config;
        private readonly int _lightAttackCooldownTicks;
        private readonly float _lightAttackMinCos;
        private readonly int _dodgeTicks;
        private readonly int _dodgeInvulnerableTicks;
        private readonly int _dodgeCooldownTicks;

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
            if (config.Dodge == null) throw new ArgumentException("Dodge must be set.", nameof(config));
            TickDelta = 1f / config.TickRate;
            _lightAttackCooldownTicks = SecondsToTicks(config.LightAttack.CooldownSeconds);
            _lightAttackMinCos = MathF.Cos(config.LightAttack.ArcDegrees * 0.5f * MathF.PI / 180f);
            _dodgeTicks = Math.Max(1, SecondsToTicks(config.Dodge.DurationSeconds));
            _dodgeInvulnerableTicks = SecondsToTicks(config.Dodge.InvulnerabilitySeconds);
            _dodgeCooldownTicks = SecondsToTicks(config.Dodge.CooldownSeconds);
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
            var player = new Player { Id = id, Position = spawnPosition, Facing = DefaultFacing, Health = _config.PlayerMaxHealth };
            _players.Add(player);
            _playersById.Add(id, player);
            _playerIds.Add(id);
        }

        public PlayerState GetPlayer(PlayerId id)
        {
            var p = FindPlayer(id);
            return new PlayerState(p.Id, p.Position, p.Facing, p.Health, _config.PlayerMaxHealth,
                p.LifeState, p.IsDodging(Tick), p.IsInvulnerable(Tick));
        }

        public EnemyId SpawnEnemy(EnemyDefinition definition, Vec3 position)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var id = new EnemyId(_nextEnemyId++);
            var enemy = new Enemy
            {
                Id = id,
                Definition = definition,
                WindupTicks = Math.Max(1, SecondsToTicks(definition.AttackWindupSeconds)),
                AttackCooldownTicks = SecondsToTicks(definition.AttackCooldownSeconds),
                Position = position,
                Health = definition.MaxHealth,
            };
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
        /// Advances one tick: players dodge, move and attack, then enemies act. Players with no
        /// entry in <paramref name="commands"/> act as if given <see cref="PlayerCommand.None"/>.
        /// Players act first, so a dodge pressed on the tick a hit lands still counts.
        /// </summary>
        public void Step(IReadOnlyDictionary<PlayerId, PlayerCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            foreach (var id in commands.Keys)
                if (!_playersById.ContainsKey(id)) throw new ArgumentException($"Command for {id}, who is not in the simulation.", nameof(commands));

            _events.Clear();
            var tick = Tick + 1;

            foreach (var player in _players)
            {
                if (!player.IsAlive) continue;
                commands.TryGetValue(player.Id, out var cmd);

                if (cmd.Dodge) TryDodge(player, cmd, tick);

                if (player.IsDodging(tick))
                {
                    player.Position += player.DodgeDirection * (_config.Dodge.Distance / _dodgeTicks);
                    continue; // no steering or attacking mid-dash
                }

                MovePlayer(player, cmd);
                if (cmd.LightAttack) TryLightAttack(player, tick);
            }

            foreach (var enemy in _enemies)
                if (enemy.Health > 0) UpdateEnemy(enemy, tick);

            Tick = tick;
        }

        private int SecondsToTicks(float seconds) =>
            // Round up to whole ticks, but not when float error leaves a hair over a whole number (0.1 * 60 = 6.0000005).
            Math.Max(0, (int)MathF.Ceiling(seconds * _config.TickRate - Epsilon));

        private Player FindPlayer(PlayerId id) =>
            _playersById.TryGetValue(id, out var p) ? p : throw new ArgumentException($"{id} is not in the simulation.", nameof(id));

        /// <summary>The command's move input as a unit direction, if there is any input at all.</summary>
        private static bool TryGetMoveDirection(PlayerCommand cmd, out Vec3 direction, out float magnitude)
        {
            var input = new Vec3(cmd.MoveX, 0f, cmd.MoveZ);
            magnitude = input.Length;
            direction = magnitude < Epsilon ? Vec3.Zero : input * (1f / magnitude);
            return magnitude >= Epsilon;
        }

        private void MovePlayer(Player player, PlayerCommand cmd)
        {
            if (!TryGetMoveDirection(cmd, out var direction, out var magnitude)) return;

            player.Position += direction * (magnitude * _config.PlayerMoveSpeed * TickDelta);
            player.Facing = direction;
        }

        private void TryDodge(Player player, PlayerCommand cmd, int tick)
        {
            if (tick < player.DodgeReadyAtTick || player.IsDodging(tick)) return;

            player.DodgeDirection = TryGetMoveDirection(cmd, out var direction, out _) ? direction : player.Facing;
            player.Facing = player.DodgeDirection;
            player.DodgeEndsAtTick = tick + _dodgeTicks;
            player.InvulnerableUntilTick = tick + _dodgeInvulnerableTicks;
            player.DodgeReadyAtTick = tick + _dodgeCooldownTicks;
            _events.Add(new DodgePerformed(tick, player.Id));
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

        /// <summary>Winding up: hold still until the hit lands. Otherwise attack a target in reach, or chase the nearest one.</summary>
        private void UpdateEnemy(Enemy enemy, int tick)
        {
            if (enemy.AttackLandsAtTick != 0)
            {
                if (tick < enemy.AttackLandsAtTick) return;
                ResolveEnemyAttack(enemy, tick);
                return;
            }

            var target = NearestLivePlayer(enemy.Position, out var distance);
            if (target == null) return;

            if (distance <= enemy.Definition.AttackRange && tick >= enemy.AttackReadyAtTick)
            {
                enemy.AttackTarget = target;
                enemy.AttackLandsAtTick = tick + enemy.WindupTicks;
                _events.Add(new EnemyAttackStarted(tick, enemy.Id, target.Id, enemy.AttackLandsAtTick));
                return;
            }

            var gap = distance - enemy.Definition.StopDistance;
            if (gap <= 0f) return;
            var step = MathF.Min(enemy.Definition.MoveSpeed * TickDelta, gap);
            enemy.Position += Flat(target.Position - enemy.Position) * (step / distance);
        }

        private void ResolveEnemyAttack(Enemy enemy, int tick)
        {
            var target = enemy.AttackTarget;
            enemy.AttackLandsAtTick = 0;
            enemy.AttackTarget = null;
            enemy.AttackReadyAtTick = tick + enemy.AttackCooldownTicks;

            if (!target.IsAlive || Flat(target.Position - enemy.Position).Length > enemy.Definition.AttackRange) return; // whiff

            if (target.IsInvulnerable(tick))
            {
                _events.Add(new AttackDodged(tick, enemy.Id, target.Id));
                return;
            }

            var amount = target.TakeDamage(enemy.Definition.AttackDamage, out var defeated);
            if (amount == 0) return; // a zero-damage hit is not news
            _events.Add(new PlayerDamaged(tick, enemy.Id, target.Id, amount, target.Health));
            if (defeated) _events.Add(new PlayerDefeated(tick, target.Id, enemy.Id));
        }

        private Player NearestLivePlayer(Vec3 from, out float distance)
        {
            Player nearest = null;
            distance = float.MaxValue;
            foreach (var player in _players)
            {
                if (!player.IsAlive) continue;
                var d = Flat(player.Position - from).Length;
                if (d < distance) { distance = d; nearest = player; }
            }
            return nearest;
        }

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);
    }
}
