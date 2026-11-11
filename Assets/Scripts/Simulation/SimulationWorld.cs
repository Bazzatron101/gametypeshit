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
            public int MaxHealth;
            public PlayerLifeState LifeState;

            public int Level = 1;
            public int Xp;
            public int UnspentStatPoints;
            public int[] Stats;

            /// <summary>Indexed by <see cref="AttackKind"/>: each attack has its own cooldown.</summary>
            public readonly int[] AttackReadyAtTick = new int[AttackKindCount];

            /// <summary>The attack being wound up; only meaningful while <see cref="PendingAttackLandsAtTick"/> is not 0.</summary>
            public AttackKind PendingAttack;
            public int PendingAttackLandsAtTick;

            // Tick bounds below are exclusive ends: "until tick N" means up to and including N - 1.
            public int DodgeReadyAtTick;
            public int DodgeEndsAtTick;
            public int InvulnerableUntilTick;
            public Vec3 DodgeDirection;

            public bool IsAlive => LifeState == PlayerLifeState.Alive;
            public bool IsDodging(int tick) => IsAlive && tick < DodgeEndsAtTick;
            public bool IsInvulnerable(int tick) => IsAlive && tick < InvulnerableUntilTick;
            public bool IsWindingUp => IsAlive && PendingAttackLandsAtTick != 0;

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
        private static readonly int AttackKindCount = Enum.GetValues(typeof(AttackKind)).Length;

        /// <summary>An attack definition with its seconds and degrees resolved once into ticks and cosines.</summary>
        private sealed class ResolvedAttack
        {
            public AttackKind Kind;
            public AttackDefinition Definition;
            public int WindupTicks;
            public int CooldownTicks;
            public float MinCos;
        }

        private readonly ResolvedAttack[] _attacks;
        private readonly float _softLockMinCos;
        private readonly int _damagePerMillePerStrength;
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
                p.Level, p.Xp, XpToNextLevel(p), p.Level >= _config.Progression.LevelCap, p.UnspentStatPoints, new PlayerStats(p.Stats));
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
        /// then players dodge, move and attack, then enemies act. Players with no
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
                if (cmd.HeavyAttack) TryStartAttack(player, _attacks[(int)AttackKind.Heavy], tick);
                else if (cmd.LightAttack) TryStartAttack(player, _attacks[(int)AttackKind.Light], tick);
            }

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

            var speed = _config.PlayerMoveSpeed * (1f + PointsAboveStart(player, Stat.Agility) * _config.StatEffects.MoveSpeedPerAgility);
            player.Position += direction * (magnitude * speed * TickDelta);
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
            player.PendingAttackLandsAtTick = 0; // a dodge cancels any attack windup
            _events.Add(new DodgePerformed(tick, player.Id));
        }

        /// <summary>
        /// Starts an attack if it is off cooldown: soft lock-on turns the player first, then the
        /// attack swings now (no windup) or is held until its windup ends.
        /// </summary>
        private void TryStartAttack(Player player, ResolvedAttack attack, int tick)
        {
            if (tick < player.AttackReadyAtTick[(int)attack.Kind]) return;
            player.AttackReadyAtTick[(int)attack.Kind] = tick + attack.CooldownTicks;

            ApplySoftLockOn(player, attack);

            if (attack.WindupTicks == 0)
            {
                SwingAttack(player, attack, tick);
                return;
            }

            player.PendingAttack = attack.Kind;
            player.PendingAttackLandsAtTick = tick + attack.WindupTicks;
            _events.Add(new AttackWindupStarted(tick, player.Id, attack.Kind, player.PendingAttackLandsAtTick));
        }

        /// <summary>
        /// Turns the player toward the nearest live enemy inside the assist cone around their
        /// current facing (which already reflects any direction they are holding). The turn is at
        /// most half the cone: that is the nudge. Enemies outside the cone are ignored, so aiming
        /// away overrides the assist. Only enemies this attack can reach count, so the assist never
        /// drags a swing off a hittable enemy toward one it cannot touch.
        /// </summary>
        private void ApplySoftLockOn(Player player, ResolvedAttack attack)
        {
            var assist = _config.SoftLockOn;
            if (!assist.Enabled) return;
            var reach = MathF.Min(assist.Range, attack.Definition.Range);

            Vec3? best = null;
            var bestDistance = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0) continue;
                var offset = Flat(enemy.Position - player.Position);
                var distance = offset.Length;
                if (distance < Epsilon || distance > reach || distance >= bestDistance) continue;
                if (!WithinCone(player.Facing, offset, distance, _softLockMinCos)) continue;
                best = offset * (1f / distance);
                bestDistance = distance;
            }

            if (best.HasValue) player.Facing = best.Value;
        }

        private void SwingAttack(Player player, ResolvedAttack attack, int tick)
        {
            _events.Add(new AttackPerformed(tick, player.Id, attack.Kind));
            var damage = ScaledDamage(player, attack.Definition.Damage);

            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0 || !InAttackCone(player, enemy.Position, attack)) continue;

                var amount = Math.Min(damage, enemy.Health);
                enemy.Health -= amount;
                _events.Add(new DamageDealt(tick, player.Id, enemy.Id, amount, enemy.Health));
                if (enemy.Health == 0)
                {
                    _events.Add(new EnemyDefeated(tick, enemy.Id, player.Id));
                    AwardXp(enemy, tick);
                }
            }
        }

        private static bool InAttackCone(Player player, Vec3 target, ResolvedAttack attack)
        {
            var offset = Flat(target - player.Position);
            var distance = offset.Length;
            if (distance > attack.Definition.Range) return false;
            if (distance < Epsilon) return true; // standing on top of it counts as a hit
            return WithinCone(player.Facing, offset, distance, attack.MinCos);
        }

        private static bool WithinCone(Vec3 facing, Vec3 offset, float distance, float minCos)
        {
            var cos = (offset.X * facing.X + offset.Z * facing.Z) / distance;
            return cos >= minCos - 1e-6f;
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

        // --- Progression -------------------------------------------------------------------

        private int PointsAboveStart(Player player, Stat stat) => player.Stats[(int)stat] - _config.Progression.StartingStatValue;

        private int ComputeMaxHealth(Player player) =>
            Math.Max(1, _config.PlayerMaxHealth + PointsAboveStart(player, Stat.Vitality) * _config.StatEffects.HealthPerVitality);

        /// <summary>Integer maths in thousandths, rounding halves up, so the result never depends on float precision.</summary>
        private int ScaledDamage(Player player, int baseDamage)
        {
            var perMille = 1000 + PointsAboveStart(player, Stat.Strength) * _damagePerMillePerStrength;
            return (int)Math.Max(0L, ((long)baseDamage * perMille + 500) / 1000);
        }

        /// <summary>XP needed to leave the current level, or 0 at the cap.</summary>
        private int XpToNextLevel(Player player) =>
            player.Level >= _config.Progression.LevelCap ? 0 : _config.Progression.XpToNextLevel[player.Level - 1];

        /// <summary>Every player gets the full reward: no splitting, no reduction for guests or carried players.</summary>
        private void AwardXp(Enemy enemy, int tick)
        {
            var reward = enemy.Definition.XpReward;
            if (reward <= 0) return;
            foreach (var player in _players) GainXp(player, reward, enemy.Id, tick);
        }

        private void GainXp(Player player, int amount, EnemyId source, int tick)
        {
            var gained = 0;
            var levelsGained = 0;
            while (amount > 0 && player.Level < _config.Progression.LevelCap)
            {
                var needed = XpToNextLevel(player) - player.Xp;
                var take = Math.Min(amount, needed);
                player.Xp += take;
                gained += take;
                amount -= take;
                if (player.Xp < XpToNextLevel(player)) break;

                player.Level++;
                player.Xp = 0;
                player.UnspentStatPoints += _config.Progression.FreePointsPerLevel;
                levelsGained++;
            }

            if (gained == 0) return; // at the cap: nothing counted
            _events.Add(new XpGained(tick, player.Id, gained, source));
            for (var i = levelsGained - 1; i >= 0; i--)
                _events.Add(new PlayerLeveledUp(tick, player.Id, player.Level - i, _config.Progression.FreePointsPerLevel));

            // The System restores a hunter who levels up.
            if (levelsGained > 0 && player.IsAlive) player.Health = player.MaxHealth;
        }

        private void TryAllocateStat(Player player, Stat stat, int tick)
        {
            if (player.UnspentStatPoints <= 0) return;
            player.UnspentStatPoints--;
            player.Stats[(int)stat]++;

            if (stat == Stat.Vitality)
            {
                var before = player.MaxHealth;
                player.MaxHealth = ComputeMaxHealth(player);
                if (player.IsAlive) player.Health += player.MaxHealth - before; // the new health arrives filled
            }

            _events.Add(new StatPointAllocated(tick, player.Id, stat, player.Stats[(int)stat]));
        }

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);
    }
}
