using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// A simple player, for filling sessions without people (ticket 14). Each tick it looks at the
    /// world it can see and the events it was shown, and decides one command, as a person at the
    /// keyboard would: spend stat points, dodge a hit it saw coming, walk to the nearest enemy,
    /// then swing at it and use whichever skills are ready. In a gate with nothing in sight it walks
    /// on to the next room, and when it is down it asks to get up.
    /// <para>
    /// It sends commands through the normal path (a client session on a bot, the host's own input on
    /// a bot host), so everything it does is checked by the simulation like anyone else's input.
    /// </para>
    /// </summary>
    public sealed class BotBrain
    {
        /// <summary>Dodge when a seen hit is due within this many ticks (inside the dodge's invulnerability).</summary>
        private const int DodgeLeadTicks = 6;

        private const int LightEveryTicks = 20;
        private const int HeavyEveryTicks = 150;
        private const int SkillEveryTicks = 45;
        private const int AskToRespawnEveryTicks = 60;

        /// <summary>Stay this close to the party when there is nothing to fight.</summary>
        private const float FollowDistance = 4f;

        private readonly float _reach;
        private readonly int _phase;
        private readonly List<int> _incomingHits = new List<int>();
        private int _nextStat;
        private int _nextSkillSlot;
        private int _decidedTick = -1;
        private PlayerCommand _decided;
        private int _dodgedBossHitAt = -1;

        /// <param name="seed">Spreads bots' timing so ten of them do not all swing on the same tick. Any int.</param>
        /// <param name="reach">How close it walks before attacking; inside the light attack's range.</param>
        public BotBrain(int seed, float reach = 1.5f)
        {
            var spread = unchecked((uint)seed * 2654435761u); // Knuth's multiplicative hash: neighbouring seeds land far apart
            _phase = (int)(spread % LightEveryTicks);
            _reach = reach;
            _nextStat = (int)((uint)seed % PlayerStats.Count);
        }

        public PlayerCommand Next(SimulationWorld world, PlayerId me, IReadOnlyList<SimulationEvent> events)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.HasPlayer(me)) return PlayerCommand.None;
            var self = world.GetPlayer(me);
            if (self.IsDefeated)
            {
                // Down: all it can do is ask to get up, about once a second (only a solo gate answers).
                _incomingHits.Clear();
                return (world.Tick + _phase) % AskToRespawnEveryTicks == 0 ? new PlayerCommand(0f, 0f, respawn: true) : PlayerCommand.None;
            }

            var tick = world.Tick + 1; // the command is for the next step

            // The game asks once a frame, often several times before the next tick runs. Every ask
            // for the same tick gets the same decision (so a dodge keeps its direction), and a choice
            // such as a stat point goes out once: the input buffer queues each one it is given.
            if (tick == _decidedTick) return _decided.WithAllocateStat(null);

            NoteIncomingHits(events, me, tick);
            var action = Act(world, me, self, tick);
            // One point a tick, alongside whatever it is doing.
            if (self.UnspentStatPoints > 0) action = action.WithAllocateStat((Stat)(_nextStat++ % PlayerStats.Count));
            _decidedTick = tick;
            _decided = action;
            return action;
        }

        private PlayerCommand Act(SimulationWorld world, PlayerId me, PlayerState self, int tick)
        {
            var target = NearestLivingEnemy(world, self.Position);
            if (BossHitDueSoon(world, self.Position, tick, out var escape)) return new PlayerCommand(escape.X, escape.Z, dodge: true);
            if (TakeHitDueSoon(tick))
            {
                // Dodge away from what is about to hit (or just back, with nothing in sight).
                var away = target.HasValue ? Direction(world.GetEnemy(target.Value).Position, self.Position) : new Vec3(0f, 0f, -1f);
                return new PlayerCommand(away.X, away.Z, dodge: true);
            }

            if (!target.HasValue) return world.HasGate ? WalkTheGate(world.Gate, self.Position) : FollowParty(world, me, self.Position);

            var offset = world.GetEnemy(target.Value).Position - self.Position;
            offset = new Vec3(offset.X, 0f, offset.Z);
            var distance = offset.Length;
            if (distance > _reach) return new PlayerCommand(offset.X / distance, offset.Z / distance);

            // In reach: attacks turn the player through soft lock-on, so it can stand still and swing.
            var beat = tick + _phase;
            if (beat % SkillEveryTicks == 0 && TryReadySkill(self, tick, out var slot)) return new PlayerCommand(0f, 0f, useSkillSlot: slot);
            if (beat % HeavyEveryTicks == 0) return new PlayerCommand(0f, 0f, heavyAttack: true);
            if (beat % LightEveryTicks == 0) return new PlayerCommand(0f, 0f, lightAttack: true);
            return PlayerCommand.None;
        }

        private void NoteIncomingHits(IReadOnlyList<SimulationEvent> events, PlayerId me, int tick)
        {
            if (events != null)
            {
                foreach (var e in events)
                {
                    if (e is EnemyAttackStarted a && a.Target == me) _incomingHits.Add(a.LandsAtTick);
                }
            }
            _incomingHits.RemoveAll(landsAt => landsAt < tick);
        }

        /// <summary>
        /// A boss attack is about to land (or a charge is coming) and the bot is standing in its
        /// marked area: dodge, sideways out of a charge's lane or away from anything else. Once per attack.
        /// </summary>
        private bool BossHitDueSoon(SimulationWorld world, Vec3 at, int tick, out Vec3 escape)
        {
            escape = Vec3.Zero;
            foreach (var id in world.EnemyIds)
            {
                var boss = world.GetEnemy(id);
                var attack = boss.CurrentBossAttack;
                if (attack == null || boss.IsDefeated || boss.AttackLandsAtTick == _dodgedBossHitAt) continue; // each attack dodged once
                if (!attack.Covers(boss.AttackOrigin, boss.AttackDirection, at)) continue;
                // A charge is dodged as it comes close; anything else just before it lands.
                if (boss.IsCharging ? Flat(boss.Position - at).Length > attack.Radius + 2.5f : boss.AttackLandsAtTick - tick > DodgeLeadTicks) continue;
                _dodgedBossHitAt = boss.AttackLandsAtTick;

                if (attack.Kind == BossAttackKind.Charge)
                {
                    var side = (at.X - boss.AttackOrigin.X) * boss.AttackDirection.Z - (at.Z - boss.AttackOrigin.Z) * boss.AttackDirection.X;
                    escape = side >= 0f ? new Vec3(boss.AttackDirection.Z, 0f, -boss.AttackDirection.X) : new Vec3(-boss.AttackDirection.Z, 0f, boss.AttackDirection.X);
                }
                else
                {
                    escape = Direction(boss.AttackOrigin, at);
                    if (escape.Length < 0.5f) escape = new Vec3(-boss.AttackDirection.X, 0f, -boss.AttackDirection.Z);
                }
                return true;
            }
            return false;
        }

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);

        /// <summary>True, and forgets that hit, when one is due within <see cref="DodgeLeadTicks"/>.</summary>
        private bool TakeHitDueSoon(int tick)
        {
            var index = _incomingHits.FindIndex(landsAt => landsAt - tick <= DodgeLeadTicks);
            if (index < 0) return false;
            _incomingHits.RemoveAt(index);
            return true;
        }

        private bool TryReadySkill(PlayerState self, int tick, out int slot)
        {
            var slots = self.SkillSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var candidate = (_nextSkillSlot + i) % slots.Count;
                if (slots[candidate].Skill == null || slots[candidate].ReadyAtTick > tick) continue;
                slot = candidate;
                _nextSkillSlot = candidate + 1;
                return true;
            }
            slot = -1;
            return false;
        }

        private static EnemyId? NearestLivingEnemy(SimulationWorld world, Vec3 from)
        {
            EnemyId? best = null;
            var bestDistance = float.MaxValue;
            foreach (var id in world.EnemyIds)
            {
                var enemy = world.GetEnemy(id);
                if (enemy.IsDefeated) continue;
                var distance = (enemy.Position - from).Length;
                if (distance >= bestDistance) continue;
                best = id;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Nothing to fight in a gate: head for the first room that is not cleared (its enemies appear
        /// on arrival), stopping at a checkpoint not yet reached on the way.
        /// </summary>
        private static PlayerCommand WalkTheGate(GateState gate, Vec3 from)
        {
            var rooms = gate.Layout.Rooms;
            for (var room = 0; room < rooms.Count; room++)
            {
                if (gate.RoomState(room) == RoomState.Cleared) continue;
                var destination = rooms[room].Centre;
                var checkpoints = gate.Layout.Checkpoints;
                for (var c = 0; c < checkpoints.Count; c++)
                {
                    if (checkpoints[c].Room >= room || gate.IsCheckpointActivated(c)) continue;
                    destination = checkpoints[c].Position; // one behind us that was missed
                    break;
                }
                var toward = Direction(from, destination);
                return new PlayerCommand(toward.X, toward.Z);
            }

            // Every room cleared: finish on the last checkpoint not yet reached, if any.
            for (var c = 0; c < gate.Layout.Checkpoints.Count; c++)
            {
                if (gate.IsCheckpointActivated(c)) continue;
                var toward = Direction(from, gate.Layout.Checkpoints[c].Position);
                return new PlayerCommand(toward.X, toward.Z);
            }
            return PlayerCommand.None;
        }

        private static PlayerCommand FollowParty(SimulationWorld world, PlayerId me, Vec3 from)
        {
            foreach (var id in world.PlayerIds)
            {
                if (id == me) continue;
                var other = world.GetPlayer(id);
                if (other.IsDefeated) continue;
                var offset = other.Position - from;
                if (offset.Length <= FollowDistance) return PlayerCommand.None;
                var toward = Direction(from, other.Position);
                return new PlayerCommand(toward.X, toward.Z);
            }
            return PlayerCommand.None;
        }

        /// <summary>Unit direction on the ground from <paramref name="from"/> to <paramref name="to"/>, or none if they coincide.</summary>
        private static Vec3 Direction(Vec3 from, Vec3 to)
        {
            var d = new Vec3(to.X - from.X, 0f, to.Z - from.Z);
            var length = d.Length;
            return length > 1e-4f ? d * (1f / length) : Vec3.Zero;
        }
    }
}
