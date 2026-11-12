using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Events as bytes, so a host can send what happened on its ticks to clients for presentation.
    /// Skills travel by id and resolve to the receiving side's own definitions (both run the same
    /// build); modifiers travel in full, as in snapshots. Bytes that cannot be read, or that name a
    /// skill the receiver does not have, are rejected with an <see cref="ArgumentException"/>.
    /// </summary>
    public static class SimulationEventCodec
    {
        // Wire codes. Never renumber: add new events at the end.
        private enum Code : byte
        {
            AttackPerformed = 1, AttackWindupStarted, DamageDealt, DodgePerformed, EnemyAttackStarted, PlayerDamaged,
            AttackDodged, PlayerDefeated, XpGained, PlayerLeveledUp, StatPointAllocated, SkillUsed, SkillChargeStarted,
            SkillChargeUnlocked, SkillSlotChanged, ModifierGranted, ModifierRevoked, SkillUnlocked, Explosion,
            ProjectileEnded, EnemyDefeated,
        }

        public static byte[] Encode(IEnumerable<SimulationEvent> events)
        {
            var list = events as IReadOnlyCollection<SimulationEvent> ?? events.ToList();
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(list.Count);
            foreach (var e in list) Write(w, e);
            w.Flush();
            return stream.ToArray();
        }

        public static List<SimulationEvent> Decode(byte[] bytes, SimulationConfig config)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try
            {
                using var r = new BinaryReader(new MemoryStream(bytes));
                var count = r.ReadInt32();
                if (count < 0 || count > bytes.Length) throw new FormatException("Impossible event count.");
                var events = new List<SimulationEvent>(count);
                for (var i = 0; i < count; i++) events.Add(Read(r, config));
                return events;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException)
            {
                throw new ArgumentException("Not a valid event list.", nameof(bytes), e);
            }
        }

        private static void Write(BinaryWriter w, SimulationEvent e)
        {
            switch (e)
            {
                case AttackPerformed a:
                    Head(w, Code.AttackPerformed, e); w.Write(a.Player.Value); w.Write((byte)a.Kind);
                    break;
                case AttackWindupStarted a:
                    Head(w, Code.AttackWindupStarted, e); w.Write(a.Player.Value); w.Write((byte)a.Kind); w.Write(a.LandsAtTick);
                    break;
                case DamageDealt d:
                    Head(w, Code.DamageDealt, e); w.Write(d.Attacker.Value); w.Write(d.Target.Value); w.Write(d.Amount);
                    w.Write(d.RemainingHealth); SimulationWorld.WriteNullable(w, d.Skill?.Id); w.Write(d.IsSecondary);
                    break;
                case DodgePerformed d:
                    Head(w, Code.DodgePerformed, e); w.Write(d.Player.Value);
                    break;
                case EnemyAttackStarted a:
                    Head(w, Code.EnemyAttackStarted, e); w.Write(a.Enemy.Value); w.Write(a.Target.Value); w.Write(a.LandsAtTick);
                    break;
                case PlayerDamaged d:
                    Head(w, Code.PlayerDamaged, e); w.Write(d.Attacker.Value); w.Write(d.Target.Value); w.Write(d.Amount); w.Write(d.RemainingHealth);
                    break;
                case AttackDodged a:
                    Head(w, Code.AttackDodged, e); w.Write(a.Attacker.Value); w.Write(a.Player.Value);
                    break;
                case PlayerDefeated d:
                    Head(w, Code.PlayerDefeated, e); w.Write(d.Player.Value); w.Write(d.DefeatedBy.Value);
                    break;
                case XpGained x:
                    Head(w, Code.XpGained, e); w.Write(x.Player.Value); w.Write(x.Amount); w.Write(x.Source.HasValue);
                    if (x.Source.HasValue) w.Write(x.Source.Value.Value);
                    break;
                case PlayerLeveledUp l:
                    Head(w, Code.PlayerLeveledUp, e); w.Write(l.Player.Value); w.Write(l.NewLevel); w.Write(l.StatPointsGranted);
                    break;
                case StatPointAllocated s:
                    Head(w, Code.StatPointAllocated, e); w.Write(s.Player.Value); w.Write((byte)s.Stat); w.Write(s.NewValue);
                    break;
                case SkillUsed s:
                    Head(w, Code.SkillUsed, e); w.Write(s.Player.Value); w.Write(s.Skill.Id); w.Write(s.Slot); w.Write(s.Range);
                    w.Write(s.Radius); w.Write(s.ChargeFraction); w.Write(s.IsCounter);
                    break;
                case SkillChargeStarted s:
                    Head(w, Code.SkillChargeStarted, e); w.Write(s.Player.Value); w.Write(s.Skill.Id); w.Write(s.Slot);
                    break;
                case SkillChargeUnlocked s:
                    Head(w, Code.SkillChargeUnlocked, e); w.Write(s.Player.Value); w.Write(s.Skill.Id);
                    break;
                case SkillSlotChanged s:
                    Head(w, Code.SkillSlotChanged, e); w.Write(s.Player.Value); w.Write(s.Slot); SimulationWorld.WriteNullable(w, s.Skill?.Id);
                    break;
                case ModifierGranted m:
                    Head(w, Code.ModifierGranted, e); w.Write(m.Player.Value); SimulationWorld.WriteModifier(w, m.Modifier);
                    break;
                case ModifierRevoked m:
                    Head(w, Code.ModifierRevoked, e); w.Write(m.Player.Value); SimulationWorld.WriteNullable(w, m.ModifierId);
                    break;
                case SkillUnlocked s:
                    Head(w, Code.SkillUnlocked, e); w.Write(s.Player.Value); w.Write(s.Skill.Id); w.Write(s.Slot ?? -1);
                    break;
                case Explosion x:
                    Head(w, Code.Explosion, e); w.Write(x.Player.Value); SimulationWorld.WriteNullable(w, x.Skill?.Id);
                    SimulationWorld.WriteVec(w, x.Position); w.Write(x.Radius);
                    break;
                case ProjectileEnded p:
                    Head(w, Code.ProjectileEnded, e); w.Write(p.Projectile); w.Write(p.HitEnemy); SimulationWorld.WriteVec(w, p.Position);
                    break;
                case EnemyDefeated d:
                    Head(w, Code.EnemyDefeated, e); w.Write(d.Enemy.Value); w.Write(d.DefeatedBy.Value);
                    break;
                default:
                    throw new NotSupportedException($"No encoding for {e.GetType().Name}.");
            }
        }

        private static void Head(BinaryWriter w, Code code, SimulationEvent e)
        {
            w.Write((byte)code);
            w.Write(e.Tick);
        }

        private static SimulationEvent Read(BinaryReader r, SimulationConfig config)
        {
            var code = (Code)r.ReadByte();
            var tick = r.ReadInt32();
            PlayerId Player() => new PlayerId(r.ReadInt32());
            EnemyId Enemy() => new EnemyId(r.ReadInt32());
            SkillDefinition Skill() => SkillById(config, r.ReadString());
            SkillDefinition NullableSkill() => SimulationWorld.ReadNullable(r) is { } id ? SkillById(config, id) : null;
            AttackKind Kind() => (AttackKind)r.ReadByte();

            switch (code)
            {
                case Code.AttackPerformed: return new AttackPerformed(tick, Player(), Kind());
                case Code.AttackWindupStarted: return new AttackWindupStarted(tick, Player(), Kind(), r.ReadInt32());
                case Code.DamageDealt:
                {
                    var attacker = Player();
                    var target = Enemy();
                    var amount = r.ReadInt32();
                    var remaining = r.ReadInt32();
                    return new DamageDealt(tick, attacker, target, amount, remaining, NullableSkill(), r.ReadBoolean());
                }
                case Code.DodgePerformed: return new DodgePerformed(tick, Player());
                case Code.EnemyAttackStarted: return new EnemyAttackStarted(tick, Enemy(), Player(), r.ReadInt32());
                case Code.PlayerDamaged: return new PlayerDamaged(tick, Enemy(), Player(), r.ReadInt32(), r.ReadInt32());
                case Code.AttackDodged: return new AttackDodged(tick, Enemy(), Player());
                case Code.PlayerDefeated: return new PlayerDefeated(tick, Player(), Enemy());
                case Code.XpGained:
                {
                    var player = Player();
                    var amount = r.ReadInt32();
                    return new XpGained(tick, player, amount, r.ReadBoolean() ? Enemy() : (EnemyId?)null);
                }
                case Code.PlayerLeveledUp: return new PlayerLeveledUp(tick, Player(), r.ReadInt32(), r.ReadInt32());
                case Code.StatPointAllocated:
                {
                    var player = Player();
                    var stat = r.ReadByte();
                    if (stat >= PlayerStats.Count) throw new FormatException("Impossible stat.");
                    return new StatPointAllocated(tick, player, (Stat)stat, r.ReadInt32());
                }
                case Code.SkillUsed:
                    return new SkillUsed(tick, Player(), Skill(), r.ReadInt32(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadBoolean());
                case Code.SkillChargeStarted: return new SkillChargeStarted(tick, Player(), Skill(), r.ReadInt32());
                case Code.SkillChargeUnlocked: return new SkillChargeUnlocked(tick, Player(), Skill());
                case Code.SkillSlotChanged: return new SkillSlotChanged(tick, Player(), r.ReadInt32(), NullableSkill());
                case Code.ModifierGranted: return new ModifierGranted(tick, Player(), SimulationWorld.ReadModifier(r));
                case Code.ModifierRevoked: return new ModifierRevoked(tick, Player(), SimulationWorld.ReadNullable(r));
                case Code.SkillUnlocked:
                {
                    var player = Player();
                    var skill = Skill();
                    var slot = r.ReadInt32();
                    return new SkillUnlocked(tick, player, skill, slot >= 0 ? slot : (int?)null);
                }
                case Code.Explosion: return new Explosion(tick, Player(), NullableSkill(), SimulationWorld.ReadVec(r), r.ReadSingle());
                case Code.ProjectileEnded: return new ProjectileEnded(tick, r.ReadInt32(), r.ReadBoolean(), SimulationWorld.ReadVec(r));
                case Code.EnemyDefeated: return new EnemyDefeated(tick, Enemy(), Player());
                default: throw new FormatException($"Unknown event code {(byte)code}.");
            }
        }

        private static SkillDefinition SkillById(SimulationConfig config, string id) =>
            config.Skills.FirstOrDefault(s => s.Id == id) ?? throw new FormatException($"Unknown skill '{id}'.");
    }
}
