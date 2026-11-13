using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Snapshots: the whole simulation state at a tick boundary, as bytes. The host sends one to a
    /// player joining or rejoining a gate, and to clients correcting their prediction. A world
    /// restored from a snapshot continues exactly as the original would, given the same commands.
    /// <para>
    /// Skills are written by id and must exist in the restoring world's config (both sides run the
    /// same build). Enemy and modifier definitions are written in full. Queued operations are not
    /// included: they belong to the host, which applies them and sends the result in a later snapshot.
    /// </para>
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private static readonly byte[] SnapshotMagic = Encoding.ASCII.GetBytes("HSW5");

        public static SimulationWorld FromSnapshot(SimulationConfig config, byte[] snapshot)
        {
            var world = new SimulationWorld(config);
            world.RestoreSnapshot(snapshot);
            return world;
        }

        public byte[] CaptureSnapshot()
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(SnapshotMagic);
            w.Write(Tick);
            w.Write(_nextEnemyId);
            w.Write(_nextProjectileId);

            // Definitions shared by several enemies, and per-use skill records shared by several
            // bolts, are written once and referred to by index, so the sharing survives a restore.
            // A boss's adds are in the table too (and referred to by index), so they stay shared after a restore.
            var enemyDefinitions = _enemies.Select(e => e.Definition)
                .SelectMany(d => d.Boss == null ? new[] { d } : new[] { d }.Concat(d.Boss.EnrageAdds.Select(a => a.Enemy)))
                .Distinct().ToList();
            var resolvedSkills = _projectiles.Select(p => p.Resolved).Distinct().ToList();

            w.Write(_players.Count);
            foreach (var p in _players) WritePlayer(w, p);

            w.Write(enemyDefinitions.Count);
            foreach (var d in enemyDefinitions) WriteEnemyDefinition(w, d, enemyDefinitions);
            w.Write(_enemies.Count);
            foreach (var e in _enemies)
            {
                w.Write(e.Id.Value);
                w.Write(enemyDefinitions.IndexOf(e.Definition));
                w.Write(e.WindupTicks);
                w.Write(e.AttackCooldownTicks);
                WriteVec(w, e.Position);
                w.Write(e.Health);
                w.Write(e.AttackReadyAtTick);
                w.Write(e.AttackLandsAtTick);
                w.Write(e.AttackTarget != null);
                if (e.AttackTarget != null) w.Write(e.AttackTarget.Id.Value);
                WriteVec(w, e.Facing);
                w.Write(e.BossAttack);
                WriteVec(w, e.AttackOrigin);
                WriteVec(w, e.AttackDirection);
                w.Write(e.Charging);
                w.Write(e.ChargeTravelled);
                w.Write(e.ChargeReach);
                WriteIds(w, e.ChargeHit);
                w.Write(e.Enraged);
                w.Write(e.EnragePending);
            }

            w.Write(resolvedSkills.Count);
            foreach (var r in resolvedSkills)
            {
                w.Write(r.Definition.Id);
                w.Write(r.BaseDamage);
                w.Write(r.CooldownTicks);
                w.Write(r.Range);
                w.Write(r.Radius);
                w.Write(r.ProjectileCount);
                w.Write(r.SpreadDegrees);
                w.Write(r.ExplosionRadius);
                w.Write(r.ExplosionDamagePercent);
                WriteIds(w, r.DirectlyHit.Select(e => e.Id.Value));
                WriteIds(w, r.Burned.Select(e => e.Id.Value));
            }
            w.Write(_projectiles.Count);
            foreach (var p in _projectiles)
            {
                w.Write(p.Id);
                w.Write(p.Owner.Id.Value);
                w.Write(p.Skill.Id);
                w.Write(p.Damage);
                w.Write(p.Speed);
                w.Write(resolvedSkills.IndexOf(p.Resolved));
                WriteVec(w, p.Position);
                WriteVec(w, p.Direction);
                w.Write(p.Travelled);
                w.Write(p.LaunchTick);
            }

            WriteGate(w);
            w.Write(_clearedGates.Count);
            foreach (var id in _clearedGates) w.Write(id);

            w.Flush();
            return stream.ToArray();
        }

        /// <summary>
        /// Replaces this world's whole state with the snapshot's. The snapshot is parsed completely
        /// before anything changes, so a malformed one throws and leaves the world as it was.
        /// </summary>
        public void RestoreSnapshot(byte[] snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ParsedSnapshot parsed;
            try
            {
                parsed = Parse(snapshot);
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException ||
                                      e is ArgumentException || e is KeyNotFoundException)
            {
                throw new ArgumentException("Not a valid world snapshot.", nameof(snapshot), e);
            }

            Tick = parsed.Tick;
            _nextEnemyId = parsed.NextEnemyId;
            _nextProjectileId = parsed.NextProjectileId;
            _players.Clear();
            _playersById.Clear();
            _playerIds.Clear();
            foreach (var p in parsed.Players)
            {
                _players.Add(p);
                _playersById.Add(p.Id, p);
                _playerIds.Add(p.Id);
            }
            _enemies.Clear();
            _enemiesById.Clear();
            _enemyIds.Clear();
            foreach (var e in parsed.Enemies)
            {
                _enemies.Add(e);
                _enemiesById.Add(e.Id, e);
                _enemyIds.Add(e.Id);
            }
            _projectiles.Clear();
            _projectiles.AddRange(parsed.Projectiles);
            _gate = parsed.Gate;
            _clearedGates.Clear();
            _clearedGates.AddRange(parsed.ClearedGates);
            _events.Clear();
            _pendingOperations.Clear();
        }

        // --- Writing ---------------------------------------------------------------------

        private static void WritePlayer(BinaryWriter w, Player p)
        {
            w.Write(p.Id.Value);
            WriteVec(w, p.Position);
            WriteVec(w, p.Facing);
            w.Write(p.Health);
            w.Write(p.MaxHealth);
            w.Write((int)p.LifeState);
            w.Write(p.Level);
            w.Write(p.Xp);
            w.Write(p.UnspentStatPoints);
            WriteInts(w, p.Stats);

            foreach (var skill in p.SkillSlots) WriteNullable(w, skill?.Id);
            w.Write(p.KnownSkills.Count);
            foreach (var skill in p.KnownSkills) w.Write(skill.Id);
            WriteTickTable(w, p.SkillReadyAtTick);
            WriteTickTable(w, p.SkillCooldownTicks);
            w.Write(p.Modifiers.Count);
            foreach (var m in p.Modifiers) WriteModifier(w, m);

            w.Write(p.Charge != null);
            if (p.Charge != null)
            {
                w.Write(p.Charge.Skill.Id);
                w.Write(p.Charge.Slot);
                w.Write(p.Charge.StartTick);
            }

            WriteInts(w, p.AttackReadyAtTick);
            w.Write((int)p.PendingAttack);
            w.Write(p.PendingAttackLandsAtTick);
            w.Write(p.DodgeReadyAtTick);
            w.Write(p.DodgeEndsAtTick);
            w.Write(p.InvulnerableUntilTick);
            WriteVec(w, p.DodgeDirection);
            w.Write(p.Away);
            w.Write(p.Ready);
        }

        private static void WriteEnemyDefinition(BinaryWriter w, EnemyDefinition d, List<EnemyDefinition> table)
        {
            WriteNullable(w, d.Name);
            w.Write(d.MaxHealth);
            w.Write(d.MoveSpeed);
            w.Write(d.StopDistance);
            w.Write(d.AttackDamage);
            w.Write(d.AttackRange);
            w.Write(d.AttackWindupSeconds);
            w.Write(d.AttackCooldownSeconds);
            w.Write(d.XpReward);
            w.Write(d.Boss != null);
            if (d.Boss == null) return;
            w.Write(d.Boss.TurnDegreesPerSecond);
            w.Write(d.Boss.EnrageAtHealthPercent);
            w.Write(d.Boss.EnragedTimePercent);
            w.Write(d.Boss.Attacks.Count);
            foreach (var a in d.Boss.Attacks)
            {
                w.Write((byte)a.Kind);
                w.Write(a.Damage);
                w.Write(a.WindupSeconds);
                w.Write(a.CooldownSeconds);
                w.Write(a.Range);
                w.Write(a.ArcDegrees);
                w.Write(a.Radius);
                w.Write(a.Speed);
            }
            w.Write(d.Boss.EnrageAdds.Count);
            foreach (var add in d.Boss.EnrageAdds)
            {
                if (add.Enemy.Boss != null) throw new InvalidOperationException("A boss's adds cannot be bosses.");
                w.Write(table.IndexOf(add.Enemy));
                WriteVec(w, add.OffsetFromCentre);
            }
        }

        internal static void WriteModifier(BinaryWriter w, ModifierDefinition m)
        {
            WriteNullable(w, m.Id);
            WriteNullable(w, m.Name);
            WriteNullable(w, m.Description);
            WriteNullable(w, m.AppliesToTag);
            WriteNullable(w, m.AppliesToSkillId);
            w.Write(m.DamagePercent);
            w.Write(m.CooldownPercent);
            w.Write(m.RangeAdd);
            w.Write(m.RadiusAdd);
            w.Write(m.ExtraProjectiles);
            w.Write(m.ProjectileSpreadDegrees);
            w.Write(m.ExplosionRadius);
            w.Write(m.ExplosionDamagePercent);
        }

        internal static void WriteVec(BinaryWriter w, Vec3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        private static void WriteInts(BinaryWriter w, int[] values)
        {
            w.Write(values.Length);
            foreach (var v in values) w.Write(v);
        }

        /// <summary>Sorted, so equal state always gives equal bytes.</summary>
        private static void WriteIds(BinaryWriter w, IEnumerable<int> ids)
        {
            var sorted = ids.OrderBy(i => i).ToArray();
            w.Write(sorted.Length);
            foreach (var id in sorted) w.Write(id);
        }

        /// <summary>Sorted by key, so equal state always gives equal bytes.</summary>
        private static void WriteTickTable(BinaryWriter w, Dictionary<string, int> table)
        {
            w.Write(table.Count);
            foreach (var pair in table.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
        }

        internal static void WriteNullable(BinaryWriter w, string value)
        {
            w.Write(value != null);
            if (value != null) w.Write(value);
        }

        // --- Reading ---------------------------------------------------------------------

        private sealed class ParsedSnapshot
        {
            public int Tick, NextEnemyId, NextProjectileId;
            public readonly List<Player> Players = new List<Player>();
            public readonly List<Enemy> Enemies = new List<Enemy>();
            public readonly List<Projectile> Projectiles = new List<Projectile>();
            public GateRun Gate;
            public readonly List<string> ClearedGates = new List<string>();
        }

        private ParsedSnapshot Parse(byte[] snapshot)
        {
            using var r = new BinaryReader(new MemoryStream(snapshot));
            if (!r.ReadBytes(SnapshotMagic.Length).SequenceEqual(SnapshotMagic)) throw new FormatException("Missing snapshot header.");

            var parsed = new ParsedSnapshot { Tick = r.ReadInt32(), NextEnemyId = r.ReadInt32(), NextProjectileId = r.ReadInt32() };
            var playersById = new Dictionary<int, Player>();
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                var p = ReadPlayer(r);
                parsed.Players.Add(p);
                playersById.Add(p.Id.Value, p);
            }

            var definitions = new List<EnemyDefinition>();
            var addsToResolve = new List<(BossDefinition boss, int index, Vec3 offset)>();
            for (int i = 0, n = ReadCount(r); i < n; i++) definitions.Add(ReadEnemyDefinition(r, addsToResolve));
            foreach (var (boss, index, offset) in addsToResolve)
            {
                if (index < 0 || index >= definitions.Count || definitions[index].Boss != null) throw new FormatException("A boss's add is not in the table.");
                boss.EnrageAdds.Add(new RoomSpawn(definitions[index], offset));
            }
            var enemiesById = new Dictionary<int, Enemy>();
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                var e = new Enemy
                {
                    Id = new EnemyId(r.ReadInt32()),
                    Definition = definitions[r.ReadInt32()],
                    WindupTicks = r.ReadInt32(),
                    AttackCooldownTicks = r.ReadInt32(),
                    Position = ReadVec(r),
                    Health = r.ReadInt32(),
                    AttackReadyAtTick = r.ReadInt32(),
                    AttackLandsAtTick = r.ReadInt32(),
                };
                if (r.ReadBoolean()) e.AttackTarget = playersById[r.ReadInt32()];
                e.Facing = ReadVec(r);
                e.BossAttack = r.ReadInt32();
                if (e.BossAttack >= 0 && (e.Definition.Boss == null || e.BossAttack >= e.Definition.Boss.Attacks.Count)) throw new FormatException("A boss attack that does not exist.");
                e.AttackOrigin = ReadVec(r);
                e.AttackDirection = ReadVec(r);
                e.Charging = r.ReadBoolean();
                e.ChargeTravelled = r.ReadSingle();
                e.ChargeReach = r.ReadSingle();
                e.ChargeHit.AddRange(ReadIds(r));
                e.Enraged = r.ReadBoolean();
                e.EnragePending = r.ReadBoolean();
                parsed.Enemies.Add(e);
                enemiesById.Add(e.Id.Value, e);
            }

            var resolvedSkills = new List<ResolvedSkill>();
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                var resolved = new ResolvedSkill
                {
                    Definition = SkillById(r.ReadString()),
                    BaseDamage = r.ReadInt32(),
                    CooldownTicks = r.ReadInt32(),
                    Range = r.ReadSingle(),
                    Radius = r.ReadSingle(),
                    ProjectileCount = r.ReadInt32(),
                    SpreadDegrees = r.ReadSingle(),
                    ExplosionRadius = r.ReadSingle(),
                    ExplosionDamagePercent = r.ReadInt32(),
                };
                // Enemies despawned since the hit are simply gone from these sets.
                foreach (var id in ReadIds(r)) if (enemiesById.TryGetValue(id, out var e)) resolved.DirectlyHit.Add(e);
                foreach (var id in ReadIds(r)) if (enemiesById.TryGetValue(id, out var e)) resolved.Burned.Add(e);
                resolvedSkills.Add(resolved);
            }
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                parsed.Projectiles.Add(new Projectile
                {
                    Id = r.ReadInt32(),
                    Owner = playersById[r.ReadInt32()],
                    Skill = SkillById(r.ReadString()),
                    Damage = r.ReadInt32(),
                    Speed = r.ReadSingle(),
                    Resolved = resolvedSkills[r.ReadInt32()],
                    Position = ReadVec(r),
                    Direction = ReadVec(r),
                    Travelled = r.ReadSingle(),
                    LaunchTick = r.ReadInt32(),
                });
            }

            parsed.Gate = ReadGate(r);
            for (int i = 0, n = ReadCount(r); i < n; i++) parsed.ClearedGates.Add(r.ReadString());

            if (r.BaseStream.Position != r.BaseStream.Length) throw new FormatException("Unexpected bytes after the snapshot.");
            return parsed;
        }

        private Player ReadPlayer(BinaryReader r)
        {
            var p = new Player
            {
                Id = new PlayerId(r.ReadInt32()),
                Position = ReadVec(r),
                Facing = ReadVec(r),
                Health = r.ReadInt32(),
                MaxHealth = r.ReadInt32(),
                LifeState = (PlayerLifeState)r.ReadInt32(),
                Level = r.ReadInt32(),
                Xp = r.ReadInt32(),
                UnspentStatPoints = r.ReadInt32(),
                Stats = ReadInts(r),
            };
            if (p.Stats.Length != PlayerStats.Count) throw new FormatException("Stat table has the wrong size.");

            for (var slot = 0; slot < SkillSlotCount; slot++)
            {
                var id = ReadNullable(r);
                p.SkillSlots[slot] = id == null ? null : SkillById(id);
            }
            for (int i = 0, n = ReadCount(r); i < n; i++) p.KnownSkills.Add(SkillById(r.ReadString()));
            ReadTickTable(r, p.SkillReadyAtTick);
            ReadTickTable(r, p.SkillCooldownTicks);
            for (int i = 0, n = ReadCount(r); i < n; i++) p.Modifiers.Add(ReadModifier(r));

            if (r.ReadBoolean())
                p.Charge = new ActiveCharge { Skill = SkillById(r.ReadString()), Slot = r.ReadInt32(), StartTick = r.ReadInt32() };

            var attackReady = ReadInts(r);
            if (attackReady.Length != p.AttackReadyAtTick.Length) throw new FormatException("Attack cooldown table has the wrong size.");
            Array.Copy(attackReady, p.AttackReadyAtTick, attackReady.Length);
            p.PendingAttack = (AttackKind)r.ReadInt32();
            p.PendingAttackLandsAtTick = r.ReadInt32();
            p.DodgeReadyAtTick = r.ReadInt32();
            p.DodgeEndsAtTick = r.ReadInt32();
            p.InvulnerableUntilTick = r.ReadInt32();
            p.DodgeDirection = ReadVec(r);
            p.Away = r.ReadBoolean();
            p.Ready = r.ReadBoolean();
            return p;
        }

        /// <param name="addsToResolve">A boss's adds name entries of the definitions table, which may come later; they are filled in once the table is read.</param>
        private static EnemyDefinition ReadEnemyDefinition(BinaryReader r, List<(BossDefinition, int, Vec3)> addsToResolve)
        {
            var d = new EnemyDefinition
            {
                Name = ReadNullable(r),
                MaxHealth = r.ReadInt32(),
                MoveSpeed = r.ReadSingle(),
                StopDistance = r.ReadSingle(),
                AttackDamage = r.ReadInt32(),
                AttackRange = r.ReadSingle(),
                AttackWindupSeconds = r.ReadSingle(),
                AttackCooldownSeconds = r.ReadSingle(),
                XpReward = r.ReadInt32(),
            };
            if (!r.ReadBoolean()) return d;
            d.Boss = new BossDefinition
            {
                TurnDegreesPerSecond = r.ReadSingle(),
                EnrageAtHealthPercent = r.ReadInt32(),
                EnragedTimePercent = r.ReadInt32(),
            };
            for (int i = 0, n = ReadCount(r); i < n; i++)
            {
                var kind = r.ReadByte();
                if (kind > (byte)BossAttackKind.Charge) throw new FormatException("Unknown boss attack.");
                d.Boss.Attacks.Add(new BossAttackDefinition
                {
                    Kind = (BossAttackKind)kind, Damage = r.ReadInt32(), WindupSeconds = r.ReadSingle(), CooldownSeconds = r.ReadSingle(),
                    Range = r.ReadSingle(), ArcDegrees = r.ReadSingle(), Radius = r.ReadSingle(), Speed = r.ReadSingle(),
                });
            }
            for (int i = 0, n = ReadCount(r); i < n; i++) addsToResolve.Add((d.Boss, r.ReadInt32(), ReadVec(r)));
            return d;
        }

        internal static ModifierDefinition ReadModifier(BinaryReader r) => new ModifierDefinition
        {
            Id = ReadNullable(r),
            Name = ReadNullable(r),
            Description = ReadNullable(r),
            AppliesToTag = ReadNullable(r),
            AppliesToSkillId = ReadNullable(r),
            DamagePercent = r.ReadInt32(),
            CooldownPercent = r.ReadInt32(),
            RangeAdd = r.ReadSingle(),
            RadiusAdd = r.ReadSingle(),
            ExtraProjectiles = r.ReadInt32(),
            ProjectileSpreadDegrees = r.ReadSingle(),
            ExplosionRadius = r.ReadSingle(),
            ExplosionDamagePercent = r.ReadInt32(),
        };

        private SkillDefinition SkillById(string id) =>
            _config.Skills.FirstOrDefault(s => s.Id == id) ?? throw new FormatException($"The snapshot names skill '{id}', which this world does not have.");

        internal static Vec3 ReadVec(BinaryReader r) => new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        /// <summary>A collection size, checked so a corrupt snapshot cannot ask for a huge allocation.</summary>
        private static int ReadCount(BinaryReader r)
        {
            var n = r.ReadInt32();
            if (n < 0 || n > r.BaseStream.Length) throw new FormatException("Impossible collection size.");
            return n;
        }

        private static int[] ReadInts(BinaryReader r)
        {
            var values = new int[ReadCount(r)];
            for (var i = 0; i < values.Length; i++) values[i] = r.ReadInt32();
            return values;
        }

        private static IEnumerable<int> ReadIds(BinaryReader r) => ReadInts(r);

        private static void ReadTickTable(BinaryReader r, Dictionary<string, int> table)
        {
            for (int i = 0, n = ReadCount(r); i < n; i++) table[r.ReadString()] = r.ReadInt32();
        }

        internal static string ReadNullable(BinaryReader r) => r.ReadBoolean() ? r.ReadString() : null;
    }
}
