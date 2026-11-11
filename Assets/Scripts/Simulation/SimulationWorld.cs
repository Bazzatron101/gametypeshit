using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The headless gameplay simulation. Each <see cref="Step"/> advances exactly one fixed tick
    /// from the commands given for that tick, and records what happened in <see cref="Events"/>.
    /// It has no dependency on rendering, scene objects or networking. Presentation reads from it.
    /// Split by topic: core and stepping here; .Entities (internal state records), .Players
    /// (movement, dodge, attacks, lock-on), .Enemies, .Progression.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>Distances and input lengths below this count as zero.</summary>
        private const float Epsilon = 1e-4f;
        private static readonly Vec3 DefaultFacing = new Vec3(0f, 0f, 1f);
        private readonly SimulationConfig _config;
        private static readonly int AttackKindCount = Enum.GetValues(typeof(AttackKind)).Length;
        private readonly ResolvedAttack[] _attacks;
        private readonly float _softLockMinCos;
        private readonly int _damagePerMillePerStrength;
        private readonly int _damagePerMillePerIntelligence;
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private int _nextProjectileId = 1;
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
            if (config.HeavyAttack == null) throw new ArgumentException("HeavyAttack must be set.", nameof(config));
            if (config.SoftLockOn == null) throw new ArgumentException("SoftLockOn must be set.", nameof(config));
            if (config.Dodge == null) throw new ArgumentException("Dodge must be set.", nameof(config));
            if (config.StatEffects == null) throw new ArgumentException("StatEffects must be set.", nameof(config));
            var progression = config.Progression ?? throw new ArgumentException("Progression must be set.", nameof(config));
            if (progression.LevelCap < 1) throw new ArgumentException("LevelCap must be at least 1.", nameof(config));
            if (progression.XpToNextLevel == null || progression.XpToNextLevel.Length < progression.LevelCap - 1)
                throw new ArgumentException($"XpToNextLevel needs {progression.LevelCap - 1} entries for a level cap of {progression.LevelCap}.", nameof(config));
            for (var i = 0; i < progression.LevelCap - 1; i++)
                if (progression.XpToNextLevel[i] <= 0)
                    throw new ArgumentException($"XpToNextLevel[{i}] (level {i + 1} to {i + 2}) must be positive.", nameof(config));
            // Per-mille integer so damage scaling is exact: 10 damage at +5% is 10.5, which rounds to 11.
            _damagePerMillePerStrength = (int)MathF.Round(config.StatEffects.DamagePerStrength * 1000f);
            _damagePerMillePerIntelligence = (int)MathF.Round(config.StatEffects.DamagePerIntelligence * 1000f);
            ValidateSkills(config.Skills, progression.LevelCap);
            TickDelta = 1f / config.TickRate;
            _attacks = new ResolvedAttack[AttackKindCount];
            _attacks[(int)AttackKind.Light] = Resolve(AttackKind.Light, config.LightAttack);
            _attacks[(int)AttackKind.Heavy] = Resolve(AttackKind.Heavy, config.HeavyAttack);
            _softLockMinCos = HalfAngleCos(config.SoftLockOn.ConeDegrees);
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
            var stats = new int[PlayerStats.Count];
            for (var i = 0; i < stats.Length; i++) stats[i] = _config.Progression.StartingStatValue;
            var player = new Player { Id = id, Position = spawnPosition, Facing = DefaultFacing, Stats = stats };
            player.MaxHealth = ComputeMaxHealth(player);
            player.Health = player.MaxHealth;
            LearnStarterSkills(player);
            _players.Add(player);
            _playersById.Add(id, player);
            _playerIds.Add(id);
        }

        public bool HasPlayer(PlayerId id) => _playersById.ContainsKey(id);

        public PlayerState GetPlayer(PlayerId id)
        {
            var p = FindPlayer(id);
            return new PlayerState(p.Id, p.Position, p.Facing, p.Health, p.MaxHealth,
                p.LifeState, p.IsDodging(Tick), p.IsInvulnerable(Tick), p.IsWindingUp,
                p.Level, p.Xp, XpToNextLevel(p), p.Level >= _config.Progression.LevelCap, p.UnspentStatPoints, new PlayerStats(p.Stats),
                SkillBarSnapshot(p), p.KnownSkills.ToArray());
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
        /// Advances one tick: stat points are spent first (so a point spent this tick already counts),
        /// then players dodge, move and attack or use a skill, then projectiles fly, then enemies act. Players with no
        /// entry in <paramref name="commands"/> act as if given <see cref="PlayerCommand.None"/>.
        /// Players act first, so a dodge pressed on the tick a hit lands still counts, and a dodge
        /// cancels an attack windup.
        /// </summary>
        public void Step(IReadOnlyDictionary<PlayerId, PlayerCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            foreach (var id in commands.Keys)
                if (!_playersById.ContainsKey(id)) throw new ArgumentException($"Command for {id}, who is not in the simulation.", nameof(commands));

            _events.Clear();
            var tick = Tick + 1;

            foreach (var player in _players)
                if (commands.TryGetValue(player.Id, out var cmd) && cmd.AllocateStat.HasValue)
                    TryAllocateStat(player, cmd.AllocateStat.Value, tick);

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

                if (player.IsWindingUp)
                {
                    if (tick >= player.PendingAttackLandsAtTick)
                    {
                        var pending = _attacks[(int)player.PendingAttack];
                        player.PendingAttackLandsAtTick = 0;
                        SwingAttack(player, pending, tick);
                    }
                    continue; // committed: no steering or new attacks until it swings
                }

                MovePlayer(player, cmd);
                if (cmd.UseSkillSlot.HasValue && TryUseSkill(player, cmd.UseSkillSlot.Value, tick)) continue; // a skill takes the tick
                if (cmd.HeavyAttack) TryStartAttack(player, _attacks[(int)AttackKind.Heavy], tick);
                else if (cmd.LightAttack) TryStartAttack(player, _attacks[(int)AttackKind.Light], tick);
            }

            UpdateProjectiles(tick);

            foreach (var enemy in _enemies)
                if (enemy.Health > 0) UpdateEnemy(enemy, tick);

            Tick = tick;
        }

        private ResolvedAttack Resolve(AttackKind kind, AttackDefinition definition) => new ResolvedAttack
        {
            Kind = kind,
            Definition = definition,
            WindupTicks = SecondsToTicks(definition.WindupSeconds),
            CooldownTicks = SecondsToTicks(definition.CooldownSeconds),
            MinCos = HalfAngleCos(definition.ArcDegrees),
        };

        /// <summary>Cosine of half a cone's total width: a direction is inside if its cosine to the centre is at least this.</summary>
        private static float HalfAngleCos(float coneDegrees) => MathF.Cos(coneDegrees * 0.5f * MathF.PI / 180f);

        private int SecondsToTicks(float seconds) =>
            // Round up to whole ticks, but not when float error leaves a hair over a whole number (0.1 * 60 = 6.0000005).
            Math.Max(0, (int)MathF.Ceiling(seconds * _config.TickRate - Epsilon));

        private Player FindPlayer(PlayerId id) =>
            _playersById.TryGetValue(id, out var p) ? p : throw new ArgumentException($"{id} is not in the simulation.", nameof(id));


        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);
    }
}
