using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Summons (ticket 57): creatures a skill calls up to fight for their summoner. Each lasts a set time or until it
    /// falls, goes for the enemy nearest to it, and can be hit: ordinary enemies take on whichever is nearer, a
    /// hunter or a summon, and a boss's sweeps and slams catch summons standing in them. A summoner has at most
    /// <see cref="MaxSummonsPerPlayer"/> at once; calling another sends the oldest away. A summon's damage is the
    /// skill's, raised by its scaling stat; its health is raised by Sense. Kills and XP are the summoner's.
    /// Summons go when a gate starts, starts again, or ends.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        public const int MaxSummonsPerPlayer = 3;

        /// <summary>How close a summon with no enemy to fight keeps to its summoner, in metres.</summary>
        private const float SummonFollowDistance = 2.5f;

        /// <summary>How far from its summoner a summon goes looking for a fight, in metres.</summary>
        private const float SummonLeash = 20f;

        private sealed class Summon
        {
            public int Id;
            public Player Owner;
            public SkillDefinition Skill;
            public Vec3 Position;
            public Vec3 Facing = new Vec3(0f, 0f, 1f);
            public int Health, MaxHealth, Damage;
            public int ExpiresAtTick, AttackReadyAtTick;
        }

        private readonly List<Summon> _summons = new List<Summon>();
        private int _nextSummonId = 1;

        /// <summary>Every summon in the world, oldest first.</summary>
        public IReadOnlyList<SummonState> Summons =>
            _summons.Select(s => new SummonState(s.Id, s.Owner.Id, s.Skill, s.Position, s.Facing, s.Health, s.MaxHealth)).ToArray();

        /// <summary>The skill's creatures appear around the summoner, sending the oldest away past the cap.</summary>
        private void CallSummons(Player owner, ResolvedSkill resolved, int damage, int tick)
        {
            var skill = resolved.Definition;
            var count = Math.Max(1, skill.SummonCount) + resolved.ProjectileCount - 1; // "more bolts" modifiers call more summons
            for (var i = 0; i < count; i++)
            {
                var mine = _summons.Where(s => s.Owner == owner).ToList();
                if (mine.Count >= MaxSummonsPerPlayer) EndSummon(mine[0], tick, fell: false);

                var angle = (i - (count - 1) * 0.5f) * 40f;
                var health = Math.Max(1, ScaledDamage(owner, skill.SummonHealth, Stat.Sense));
                var summon = new Summon
                {
                    Id = _nextSummonId++,
                    Owner = owner,
                    Skill = skill,
                    Position = Ground.Confine(owner.Position, owner.Position + RotateFlat(owner.Facing, angle) * 1.5f),
                    Facing = owner.Facing,
                    Health = health,
                    MaxHealth = health,
                    Damage = damage,
                    ExpiresAtTick = tick + Math.Max(1, SecondsToTicks(skill.SummonLifetimeSeconds)),
                    AttackReadyAtTick = tick + 1,
                };
                _summons.Add(summon);
                _events.Add(new SummonAppeared(tick, summon.Id, owner.Id, skill, summon.Position));
            }
        }

        /// <summary>Each tick: summons whose time is up go; the rest fight the nearest enemy, or keep by their summoner.</summary>
        private void UpdateSummons(int tick)
        {
            foreach (var summon in _summons.ToList())
            {
                if (tick >= summon.ExpiresAtTick || summon.Owner.Away)
                {
                    EndSummon(summon, tick, fell: false);
                    continue;
                }

                var target = NearestLiveEnemy(summon.Position, out var distance);
                if (target != null && Flat(target.Position - summon.Owner.Position).Length > SummonLeash) target = null;
                if (target == null)
                {
                    var gap = Flat(summon.Owner.Position - summon.Position).Length - SummonFollowDistance;
                    if (gap > 0f) MoveSummon(summon, summon.Owner.Position, MathF.Min(summon.Skill.SummonMoveSpeed * TickDelta, gap));
                    continue;
                }

                var toward = Flat(target.Position - summon.Position);
                if (toward.Length > Epsilon) summon.Facing = toward * (1f / toward.Length);
                if (distance <= summon.Skill.SummonAttackRange)
                {
                    if (tick < summon.AttackReadyAtTick) continue;
                    summon.AttackReadyAtTick = tick + Math.Max(1, SecondsToTicks(summon.Skill.SummonAttackSeconds));
                    DamageEnemy(summon.Owner, target, summon.Damage, tick, summon.Skill);
                    continue;
                }
                MoveSummon(summon, target.Position, MathF.Min(summon.Skill.SummonMoveSpeed * TickDelta, distance - summon.Skill.SummonAttackRange * 0.8f));
            }
        }

        private void MoveSummon(Summon summon, Vec3 goal, float step)
        {
            var offset = Flat(goal - summon.Position);
            var length = offset.Length;
            if (length < Epsilon || step <= 0f) return;
            var next = summon.Position + offset * (MathF.Min(step, length) / length);
            next = Ground.Confine(summon.Position, next);
            summon.Position = new Vec3(next.X, Ground.HeightAt(next), next.Z);
        }

        private Enemy NearestLiveEnemy(Vec3 from, out float distance)
        {
            Enemy nearest = null;
            distance = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0) continue;
                var d = Flat(enemy.Position - from).Length;
                if (d < distance) { distance = d; nearest = enemy; }
            }
            return nearest;
        }

        /// <summary>The live summon nearest <paramref name="from"/>, or null.</summary>
        private Summon NearestSummon(Vec3 from, out float distance)
        {
            Summon nearest = null;
            distance = float.MaxValue;
            foreach (var summon in _summons)
            {
                var d = Flat(summon.Position - from).Length;
                if (d < distance) { distance = d; nearest = summon; }
            }
            return nearest;
        }

        /// <summary>The one place a summon's health goes down: an enemy's hit, or a boss attack it stood in.</summary>
        private void DamageSummon(Summon summon, EnemyId attacker, int damage, int tick)
        {
            if (!_summons.Contains(summon)) return;
            var amount = Math.Min(Math.Max(0, damage), summon.Health);
            if (amount == 0) return;
            summon.Health -= amount;
            _events.Add(new SummonDamaged(tick, summon.Id, attacker, amount, summon.Health));
            if (summon.Health == 0) EndSummon(summon, tick, fell: true);
        }

        private void EndSummon(Summon summon, int tick, bool fell)
        {
            if (!_summons.Remove(summon)) return;
            _events.Add(new SummonEnded(tick, summon.Id, fell));
        }

        /// <summary>Every summon goes, without a fuss (a gate starting, starting again, or ending).</summary>
        private void ClearSummons() => _summons.Clear();

        private void WriteSummons(BinaryWriter w)
        {
            w.Write(_nextSummonId);
            w.Write(_summons.Count);
            foreach (var s in _summons)
            {
                w.Write(s.Id);
                w.Write(s.Owner.Id.Value);
                w.Write(s.Skill.Id);
                WriteVec(w, s.Position);
                WriteVec(w, s.Facing);
                w.Write(s.Health);
                w.Write(s.MaxHealth);
                w.Write(s.Damage);
                w.Write(s.ExpiresAtTick);
                w.Write(s.AttackReadyAtTick);
            }
        }

        /// <summary>Reads what <see cref="WriteSummons"/> wrote. A summon whose summoner is not in this snapshot is left out.</summary>
        private (int nextId, List<Summon> summons) ReadSummons(BinaryReader r, IReadOnlyDictionary<int, Player> players)
        {
            var nextId = r.ReadInt32();
            var summons = new List<Summon>();
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                var s = new Summon { Id = r.ReadInt32() };
                var owner = r.ReadInt32();
                s.Skill = SkillById(r.ReadString());
                s.Position = ReadVec(r);
                s.Facing = ReadVec(r);
                s.Health = r.ReadInt32();
                s.MaxHealth = r.ReadInt32();
                s.Damage = r.ReadInt32();
                s.ExpiresAtTick = r.ReadInt32();
                s.AttackReadyAtTick = r.ReadInt32();
                if (s.Skill.Effect != SkillEffectKind.Summon) throw new FormatException($"The snapshot summons with '{s.Skill.Id}', which is no summon.");
                if (!players.TryGetValue(owner, out s.Owner)) continue;
                summons.Add(s);
            }
            return (nextId, summons);
        }
    }

    /// <summary>Read-only view of a summon (ticket 57).</summary>
    public readonly struct SummonState
    {
        public readonly int Id;
        public readonly PlayerId Owner;
        public readonly SkillDefinition Skill;
        public readonly Vec3 Position;
        public readonly Vec3 Facing;
        public readonly int Health, MaxHealth;

        public SummonState(int id, PlayerId owner, SkillDefinition skill, Vec3 position, Vec3 facing, int health, int maxHealth)
        {
            Id = id;
            Owner = owner;
            Skill = skill;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
        }
    }
}
