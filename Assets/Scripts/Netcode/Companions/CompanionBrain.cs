using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// A bot teammate (ADR 0014). It plays its class's role in the party, follows the player who
    /// leads it, and plays better the more it has been through: its <see cref="CompanionExperience"/>
    /// grows with every fight, and its proficiency sets how often it notices a telegraphed hit, how
    /// fast it reacts, how carefully it picks targets and spends skills, and how well it keeps its place.
    /// <para>
    /// The roles, at full proficiency:
    /// <list type="bullet">
    /// <item><b>Tank</b> (Warrior) leads into a fight, takes the boss head-on, and goes for the enemy closest to the back line.</item>
    /// <item><b>Skirmisher</b> (Rogue) finishes weak enemies, clears a boss's adds first, and fights the boss from behind, out of its sweeps and charges.</item>
    /// <item><b>Marksman</b> (Archer) focuses the weakest enemy in range from a distance and backs off when anything comes close.</item>
    /// <item><b>Caster</b> (Wizard) keeps its distance, aims at the thickest pack, and keeps its big spells for groups and bosses.</item>
    /// </list>
    /// Everyone charges skills when it is safe (a dodge mid-charge is a perfect-dodge counter). Against
    /// a boss's marked area, the front line dodges through at the last moment and the back line walks
    /// out. With the leader down, they push on to the next checkpoint to bring them back. A teammate
    /// with a heal (the Cleric, ticket 59) heals before it attacks: its heal bolt goes to the most hurt
    /// ally in range, its heal area when it or an ally inside it is hurt.
    /// </para>
    /// <para>
    /// A rookie still plays its role, but notices fewer hits, swings and casts on a slow rhythm,
    /// goes for whatever is nearest, crowds a boss and uses skills as soon as they are ready.
    /// Like <see cref="BotBrain"/>, it only sends commands, which the simulation checks like anyone else's.
    /// </para>
    /// </summary>
    public sealed class CompanionBrain
    {
        private const int DodgeLeadTicks = 6;
        private const int AskToRespawnEveryTicks = 60;

        /// <summary>Enemies this close to it or to its leader are its fight.</summary>
        private const float EngageRadius = 14f;

        /// <summary>The light attack's reach is 2.5 m; it closes to this before swinging.</summary>
        private const float MeleeReach = 1.5f;
        private const float TankReach = 0.8f;
        private const float LightAttackRange = 2.4f;

        /// <summary>A ranged teammate backs away from anything closer than this.</summary>
        private const float PersonalSpace = 3.5f;

        /// <summary>The longest it holds a charge, in seconds, whatever happens.</summary>
        private const float MaxChargeSeconds = 3f;

        private struct Incoming
        {
            public int LandsAt;
            public bool Noticed;
        }

        private readonly Random _random;
        private readonly int _phase;
        private readonly float _side;
        private readonly Stat[] _statPlan;
        private readonly List<Incoming> _incoming = new List<Incoming>();
        private readonly Dictionary<long, bool> _bossAttacksNoticed = new Dictionary<long, bool>();
        private readonly HashSet<EnemyId> _bosses = new HashSet<EnemyId>();
        private readonly List<EnemyState> _enemies = new List<EnemyState>();
        private readonly List<EnemyState> _engaged = new List<EnemyState>();
        private int _nextStat;
        private int _decidedTick = -1;
        private PlayerCommand _decided;
        private int _combatTicks;
        private int? _chargingSlot;
        private int _chargeStartedTick;
        private float _chargeTarget;
        private float _tickSeconds = 1f / 60f;

        // Steering across the overworld (ticket 43): the last way found, for which goal, on which tick.
        private Vec3 _waypoint;
        private Vec3 _waypointGoal;
        private int _waypointTick = int.MinValue;
        private int _tick;

        /// <param name="record">The teammate; its experience grows as it plays.</param>
        /// <param name="seed">Spreads its timing and decisions from other teammates'. Any int.</param>
        public CompanionBrain(CompanionRecord record, int seed)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            if (Record.Experience == null) Record.Experience = new CompanionExperience();
            Role = record.Role;
            _random = new Random(seed);
            var spread = unchecked((uint)seed * 2654435761u);
            _phase = (int)(spread % 60u);
            _side = (spread & 0x10000u) != 0 ? 1f : -1f;
            _statPlan = CompanionRoles.StatPlan(Role);
        }

        public CompanionRecord Record { get; }
        public CompanionRole Role { get; }

        /// <summary>The player it follows and fights beside (the host); null to make its own way through a gate.</summary>
        public PlayerId? Leader { get; set; }

        /// <summary>How well it plays now, from <see cref="CompanionExperience.Rookie"/> towards 1.</summary>
        public float Proficiency => Record.Experience.Proficiency;

        public PlayerCommand Next(SimulationWorld world, PlayerId me, IReadOnlyList<SimulationEvent> events)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.HasPlayer(me)) return PlayerCommand.None;
            var tick = world.Tick + 1; // the command is for the next step
            if (tick == _decidedTick) return _decided.WithAllocateStat(null); // asked twice for one tick: same answer, the stat point once
            _decidedTick = tick;

            Learn(world, me, events, tick);
            var self = world.GetPlayer(me);
            PlayerCommand action;
            if (self.IsDefeated)
            {
                _incoming.Clear();
                _chargingSlot = null;
                action = (world.Tick + _phase) % AskToRespawnEveryTicks == 0 ? new PlayerCommand(0f, 0f, respawn: true) : PlayerCommand.None;
            }
            else
            {
                action = Act(world, me, self, tick);
                if (self.UnspentStatPoints > 0) action = action.WithAllocateStat(_statPlan[_nextStat++ % _statPlan.Length]);
                if (CompanionRoles.GearToWear(Role, self) is { } gear) action = action.WithEquipGear(gear);
            }
            _decided = action;
            return action;
        }

        // --- Learning ------------------------------------------------------------------------

        /// <summary>What happened last tick goes into its experience; hits aimed at it are remembered, each noticed or not.</summary>
        private void Learn(SimulationWorld world, PlayerId me, IReadOnlyList<SimulationEvent> events, int tick)
        {
            var experience = Record.Experience;
            _tickSeconds = world.TickDelta;
            _enemies.Clear();
            foreach (var id in world.EnemyIds)
            {
                var enemy = world.GetEnemy(id);
                if (enemy.IsBoss) _bosses.Add(id);
                if (!enemy.IsDefeated) _enemies.Add(enemy);
            }

            if (events != null)
            {
                foreach (var e in events)
                {
                    switch (e)
                    {
                        case EnemyDefeated defeated:
                            experience.EnemiesDefeated++;
                            if (_bosses.Remove(defeated.Enemy)) experience.BossesDefeated++;
                            break;
                        case GateCleared _:
                            experience.GatesCleared++;
                            break;
                        case PlayerDefeated down when down.Player == me:
                            experience.TimesDowned++;
                            break;
                        case AttackDodged dodged when dodged.Player == me:
                            experience.AttacksDodged++;
                            break;
                        case EnemyAttackStarted attack when attack.Target == me:
                            _incoming.Add(new Incoming { LandsAt = attack.LandsAtTick, Noticed = Notices() });
                            break;
                    }
                }
            }
            _incoming.RemoveAll(hit => hit.LandsAt < tick);

            if (world.HasPlayer(me) && !world.GetPlayer(me).IsDefeated && NearestDistance(world.GetPlayer(me).Position) <= EngageRadius)
            {
                var ticksPerSecond = Math.Max(1, (int)MathF.Round(1f / _tickSeconds));
                if (++_combatTicks >= ticksPerSecond)
                {
                    _combatTicks = 0;
                    experience.SecondsInCombat++;
                }
            }
        }

        /// <summary>Whether it sees a telegraphed hit coming: under half the time as a rookie, almost always at its best.</summary>
        private bool Notices() => _random.NextDouble() < 0.3 + 0.7 * Proficiency;

        private float NearestDistance(Vec3 from)
        {
            var best = float.MaxValue;
            foreach (var enemy in _enemies) best = MathF.Min(best, Flat(enemy.Position - from).Length);
            return best;
        }

        // --- Deciding ------------------------------------------------------------------------

        private PlayerCommand Act(SimulationWorld world, PlayerId me, PlayerState self, int tick)
        {
            var p = Proficiency;
            if (_chargingSlot.HasValue && self.ChargingSlot != _chargingSlot && tick - _chargeStartedTick > 2) _chargingSlot = null; // the charge never started, or it was fired

            _tick = tick;
            var leader = LeaderState(world, me);
            if (world.InHub && !self.IsReady) return new PlayerCommand(0f, 0f, setReady: true); // the ready check, wherever the party is outside a gate
            if (world.InHub && !world.InOverworld) return Travel(world, self, leader, p);
            // Out in the overworld (ticket 43) it fights what leaks from the gates, as in a gate, and otherwise follows.

            if (TryAvoidBoss(self, tick, p, out var avoid)) return avoid;
            if (TryDodgeHit(self, tick, out var dodge)) return dodge;
            if (!_chargingSlot.HasValue && TryHeal(world, me, self, tick, p, out var heal)) return heal; // in a fight or after it

            _engaged.Clear();
            foreach (var enemy in _enemies)
            {
                if (Flat(enemy.Position - self.Position).Length <= EngageRadius ||
                    (leader.HasValue && Flat(enemy.Position - leader.Value.Position).Length <= EngageRadius))
                    _engaged.Add(enemy);
            }
            if (_engaged.Count == 0)
            {
                _chargingSlot = null; // nothing to fire at: let go
                return Travel(world, self, leader, p);
            }

            var target = ChooseTarget(world, me, self, p);
            var aim = Yaw(self.Position, target.Position);
            var (move, sprint) = Position(self, target, p);

            if (_chargingSlot.HasValue) return ContinueCharge(self, move, aim, tick);
            if (TryChooseSkill(self, target, tick, p, out var slot, out var charge))
            {
                if (charge)
                {
                    _chargingSlot = slot;
                    _chargeStartedTick = tick;
                    _chargeTarget = p >= 0.75f ? 1f : 0.6f;
                }
                return Command(Vec3.Zero, skillSlot: slot, aim: aim);
            }

            var distance = Flat(target.Position - self.Position).Length;
            if (distance <= AttackReach(self, AttackKind.Light))
            {
                var beat = tick + _phase;
                if (distance <= AttackReach(self, AttackKind.Heavy) && WantsHeavy(target, tick, p, beat)) return Command(Vec3.Zero, heavy: true, aim: aim);
                var every = Math.Max(1, (int)MathF.Round(Lerp(18f, 1f, p)));
                if (beat % every == 0) return Command(move, light: true, aim: aim);
            }
            return Command(move, sprint: sprint, aim: aim);
        }

        /// <summary>
        /// How close it must be to attack: the worn weapon's reach (ticket 46: a bow from range, a
        /// staff's blast round itself), a little short of the edge; the default swing's otherwise.
        /// </summary>
        private static float AttackReach(PlayerState self, AttackKind kind)
        {
            var weapon = self.Worn[(int)GearSlot.Weapon];
            var hit = (kind == AttackKind.Heavy ? weapon?.HeavyAttack : weapon?.LightAttack)?.Hit;
            if (hit == null) return LightAttackRange;
            return 0.9f * (hit.Effect == SkillEffectKind.Area ? hit.Radius : hit.Range);
        }

        /// <summary>The leader if it is in the world and standing; null otherwise.</summary>
        private PlayerState? LeaderState(SimulationWorld world, PlayerId me)
        {
            if (!Leader.HasValue || Leader.Value == me || !world.HasPlayer(Leader.Value)) return null;
            var leader = world.GetPlayer(Leader.Value);
            return leader.IsDefeated || leader.IsAway ? (PlayerState?)null : leader;
        }

        /// <summary>A command; while a charge is held, its key stays down.</summary>
        private PlayerCommand Command(Vec3 move, bool light = false, bool dodge = false, bool heavy = false, int? skillSlot = null, bool sprint = false, float? aim = null) =>
            new PlayerCommand(move.X, move.Z, lightAttack: light, dodge: dodge, heavyAttack: heavy, useSkillSlot: skillSlot,
                heldSkillSlots: _chargingSlot.HasValue ? 1 << _chargingSlot.Value : 0, sprint: sprint, aimYawDegrees: aim);

        // --- Staying alive -------------------------------------------------------------------

        /// <summary>
        /// A boss's marked area covers it. If it noticed (once per attack), it walks out while there is
        /// time (a seasoned teammate) and dodges out just before the hit lands.
        /// </summary>
        private bool TryAvoidBoss(PlayerState self, int tick, float p, out PlayerCommand command)
        {
            command = default;
            foreach (var boss in _enemies)
            {
                var attack = boss.CurrentBossAttack;
                if (attack == null) continue;
                var reach = boss.AttackReach > 0f ? boss.AttackReach : (float?)null;
                if (!attack.Covers(boss.AttackOrigin, boss.AttackDirection, self.Position, reach)) continue;

                var key = ((long)boss.Id.Value << 32) | (uint)boss.AttackLandsAtTick;
                if (!_bossAttacksNoticed.TryGetValue(key, out var noticed))
                {
                    if (_bossAttacksNoticed.Count > 64) _bossAttacksNoticed.Clear();
                    noticed = Notices();
                    _bossAttacksNoticed[key] = noticed;
                }
                if (!noticed) continue;

                var escape = Escape(boss, attack, self.Position);
                var dueIn = boss.AttackLandsAtTick - tick;
                var closeEnough = boss.IsCharging ? Flat(boss.Position - self.Position).Length <= attack.Radius + 2.5f : dueIn <= DodgeLeadTicks;
                if (closeEnough && !self.IsDodging)
                {
                    command = Command(escape, dodge: true);
                    return true;
                }
                // The front line stands its ground and dodges through at the last moment: the tank keeps the boss on
                // it, and the Rogue keeps hitting. The back line walks out and keeps its dodge.
                var holdsGround = CompanionRoles.IsFrontLine(Role);
                if (p >= 0.5f && !boss.IsCharging && !holdsGround)
                {
                    command = Command(escape, sprint: true); // time to walk out and keep the dodge
                    return true;
                }
            }
            return false;
        }

        /// <summary>Which way out: sideways out of a charge's lane, sideways and round behind a sweep, away from a slam.</summary>
        private static Vec3 Escape(EnemyState boss, BossAttackDefinition attack, Vec3 at)
        {
            var d = boss.AttackDirection;
            var sideOf = (at.X - boss.AttackOrigin.X) * d.Z - (at.Z - boss.AttackOrigin.Z) * d.X;
            var sideways = sideOf >= 0f ? new Vec3(d.Z, 0f, -d.X) : new Vec3(-d.Z, 0f, d.X);
            if (attack.Kind == BossAttackKind.Charge) return sideways;
            if (attack.Kind == BossAttackKind.Sweep)
            {
                var round = sideways - d * 0.5f; // the answer to a sweep is behind it
                return round * (1f / round.Length);
            }
            var away = Direction(boss.AttackOrigin, at);
            return away.Length < 0.5f ? new Vec3(-boss.AttackDirection.X, 0f, -boss.AttackDirection.Z) : away;
        }

        /// <summary>A hit it noticed lands within the dodge's invulnerability: dodge away from the nearest enemy.</summary>
        private bool TryDodgeHit(PlayerState self, int tick, out PlayerCommand command)
        {
            command = default;
            var index = _incoming.FindIndex(hit => hit.LandsAt - tick <= DodgeLeadTicks);
            if (index < 0) return false;
            var hit = _incoming[index];
            _incoming.RemoveAt(index);
            if (!hit.Noticed || self.IsDodging) return false;

            var away = new Vec3(0f, 0f, -1f);
            var nearest = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                var d = Flat(enemy.Position - self.Position).Length;
                if (d >= nearest) continue;
                nearest = d;
                away = Direction(enemy.Position, self.Position);
            }
            command = Command(away, dodge: true); // a held charge stays held: dodging the hit is a perfect-dodge counter
            return true;
        }

        private bool HitDueWithin(int tick, int ticks)
        {
            foreach (var hit in _incoming) if (hit.LandsAt - tick <= ticks) return true;
            foreach (var boss in _enemies) if (boss.CurrentBossAttack != null && boss.AttackLandsAtTick - tick <= ticks) return true;
            return false;
        }

        // --- Targets and footwork ------------------------------------------------------------

        private EnemyState ChooseTarget(SimulationWorld world, PlayerId me, PlayerState self, float p)
        {
            var thinking = p >= 0.4f; // below this, whatever is nearest
            EnemyState best = _engaged[0];
            var bestScore = float.MaxValue;
            foreach (var enemy in _engaged)
            {
                var distance = Flat(enemy.Position - self.Position).Length;
                var health = enemy.MaxHealth > 0 ? (float)enemy.Health / enemy.MaxHealth : 1f;
                float score;
                if (!thinking) score = distance;
                else switch (Role)
                {
                    case CompanionRole.Tank:
                        // The boss is the tank's; otherwise whatever is closest to someone squishier.
                        score = enemy.IsBoss ? -1000f : DistanceToNearestAlly(world, me, enemy.Position) + distance * 0.5f;
                        break;
                    case CompanionRole.Skirmisher:
                        // The weak and the near; the boss's adds before the boss.
                        score = distance + health * 6f + (enemy.IsBoss && _engaged.Count > 1 ? 8f : 0f);
                        break;
                    case CompanionRole.Marksman:
                        // Focus fire: the weakest it can reach.
                        score = health * 10f + distance * 0.3f + (distance > 12f ? 20f : 0f);
                        break;
                    default:
                        // The thickest pack.
                        score = distance * 0.3f - Neighbours(enemy) * 4f;
                        break;
                }
                if (score >= bestScore) continue;
                bestScore = score;
                best = enemy;
            }
            return best;
        }

        private float DistanceToNearestAlly(SimulationWorld world, PlayerId me, Vec3 at)
        {
            var best = float.MaxValue;
            foreach (var id in world.PlayerIds)
            {
                if (id == me) continue;
                var ally = world.GetPlayer(id);
                if (ally.IsDefeated || ally.IsAway || CompanionRoles.ForClass(ally.Class?.Id) == CompanionRole.Tank) continue;
                best = MathF.Min(best, Flat(ally.Position - at).Length);
            }
            return best == float.MaxValue ? 0f : best;
        }

        private int Neighbours(EnemyState enemy)
        {
            var count = 0;
            foreach (var other in _engaged)
                if (other.Id != enemy.Id && Flat(other.Position - enemy.Position).Length <= 4f) count++;
            return count;
        }

        /// <summary>Where to move in a fight, by role: melee into reach (a seasoned Rogue behind a boss), ranged at a distance.</summary>
        private (Vec3 move, bool sprint) Position(PlayerState self, EnemyState target, float p)
        {
            var offset = Flat(target.Position - self.Position);
            var distance = offset.Length;
            var slack = Lerp(2.5f, 1f, p);

            if (CompanionRoles.IsFrontLine(Role))
            {
                if (Role == CompanionRole.Skirmisher && target.IsBoss && p >= 0.5f) return Flank(self, target);
                // The tank stands closer than anyone, so it is the one enemies go for.
                var reach = Role == CompanionRole.Tank && p >= 0.4f ? TankReach : MeleeReach;
                if (distance <= reach) return (Vec3.Zero, false);
                return (offset * (1f / distance), distance > 6f);
            }

            // Ranged: first, nothing in its face.
            if (p >= 0.4f)
            {
                foreach (var enemy in _engaged)
                {
                    var close = Flat(self.Position - enemy.Position);
                    if (close.Length >= PersonalSpace || close.Length < 1e-4f) continue;
                    return (close * (1f / close.Length), false);
                }
            }
            var wanted = CompanionRoles.PreferredRange(Role);
            if (target.IsBoss) wanted = MathF.Max(wanted, SlamRadius(target) + 2f);
            if (distance > wanted + slack) return (offset * (1f / distance), distance > wanted + 6f);
            if (distance < wanted - slack && distance > 1e-4f) return (offset * (-1f / distance), false);
            return (Vec3.Zero, false);
        }

        /// <summary>
        /// Behind the boss, where its sweeps and charges cannot reach; a slam it walks out of
        /// (<see cref="TryAvoidBoss"/>), then comes straight back.
        /// </summary>
        private static (Vec3 move, bool sprint) Flank(PlayerState self, EnemyState boss)
        {
            var back = boss.Facing.Length > 1e-4f ? boss.Facing : new Vec3(0f, 0f, 1f);
            var spot = boss.Position - back * 1.2f;
            var offset = Flat(spot - self.Position);
            if (offset.Length < 0.4f) return (Vec3.Zero, false);
            return (offset * (1f / offset.Length), offset.Length > 4f);
        }

        private static float SlamRadius(EnemyState boss) => boss.Definition?.Boss?.Attack(BossAttackKind.Slam)?.Radius ?? 0f;

        /// <summary>
        /// Nothing to fight: in formation around the leader (the tank in front, ranged behind). With
        /// no leader standing (down, or none), it makes its way through the gate, which brings a
        /// fallen leader back at the next checkpoint.
        /// </summary>
        private PlayerCommand Travel(SimulationWorld world, PlayerState self, PlayerState? leader, float p)
        {
            if (!leader.HasValue) return world.HasGate ? WalkTheGate(world.Gate, self.Position) : PlayerCommand.None;

            var l = leader.Value;
            var forward = l.Facing.Length > 1e-4f ? l.Facing : new Vec3(0f, 0f, 1f);
            var right = new Vec3(forward.Z, 0f, -forward.X);
            float ahead, across;
            switch (Role)
            {
                case CompanionRole.Tank: ahead = 2.5f; across = _side * 0.8f; break;
                case CompanionRole.Skirmisher: ahead = 1f; across = _side * 2.2f; break;
                case CompanionRole.Marksman: ahead = -2.5f; across = _side * 2.2f; break;
                default: ahead = -3.5f; across = -_side * 1.2f; break;
            }
            var spot = l.Position + forward * ahead + right * across;
            var offset = Flat(spot - self.Position);
            var distance = offset.Length;
            if (world.InOverworld)
            {
                // Keep up on a mount when the leader rides (ticket 42), and get down when they stop, ready to fight.
                if (l.IsMounted && !self.IsMounted && self.HasMount && world.CanMount(self.Id)) return new PlayerCommand(0f, 0f, toggleMount: true);
                if (!l.IsMounted && self.IsMounted && distance < 8f) return new PlayerCommand(0f, 0f, toggleMount: true);
            }
            if (distance <= Lerp(3f, 1f, p)) return PlayerCommand.None;
            var toward = Flat((world.InOverworld ? Steer(world, self.Position, spot) : spot) - self.Position);
            toward = toward.Length > 1e-4f ? toward * (1f / toward.Length) : offset * (1f / distance);
            return new PlayerCommand(toward.X, toward.Z, sprint: distance > 6f && !self.IsMounted);
        }

        /// <summary>Where to head for <paramref name="goal"/> across the overworld: straight, or round what is in the way (ticket 43).</summary>
        private Vec3 Steer(SimulationWorld world, Vec3 from, Vec3 goal)
        {
            if (_tick - _waypointTick >= 15 || Flat(_waypointGoal - goal).Length > 3f || Flat(_waypoint - from).Length < 0.5f)
            {
                _waypoint = GroundPath.NextWaypoint(world.Ground, from, goal);
                _waypointGoal = goal;
                _waypointTick = _tick;
            }
            return _waypoint;
        }

        /// <summary>No one to follow in a gate: on to the first room not cleared, a missed checkpoint first; when cleared, home.</summary>
        private static PlayerCommand WalkTheGate(GateState gate, Vec3 from)
        {
            if (gate.ReturnPortal.HasValue) return Toward(from, gate.ReturnPortal.Value);
            var rooms = gate.Layout.Rooms;
            for (var room = 0; room < rooms.Count; room++)
            {
                if (gate.RoomState(room) == RoomState.Cleared) continue;
                var destination = rooms[room].Centre;
                var checkpoints = gate.Layout.Checkpoints;
                for (var c = 0; c < checkpoints.Count; c++)
                {
                    if (checkpoints[c].Room >= room || gate.IsCheckpointActivated(c)) continue;
                    destination = checkpoints[c].Position;
                    break;
                }
                return Toward(from, destination);
            }
            for (var c = 0; c < gate.Layout.Checkpoints.Count; c++)
                if (!gate.IsCheckpointActivated(c)) return Toward(from, gate.Layout.Checkpoints[c].Position);
            return PlayerCommand.None;
        }

        private static PlayerCommand Toward(Vec3 from, Vec3 to)
        {
            var d = Direction(from, to);
            return new PlayerCommand(d.X, d.Z, sprint: Flat(to - from).Length > 6f);
        }

        // --- Attacks and skills ----------------------------------------------------------------

        /// <summary>A heavy swing from the front line: on a slow rhythm as a rookie; later only when nothing will interrupt the windup.</summary>
        private bool WantsHeavy(EnemyState target, int tick, float p, int beat)
        {
            if (!CompanionRoles.IsFrontLine(Role)) return false;
            if (p < 0.5f) return beat % 150 == 0;
            return beat % 40 == 0 && !HitDueWithin(tick, 30) && target.Health > target.MaxHealth / 4;
        }

        /// <summary>
        /// The best ready skill for this moment, scored by damage times what it would hit. A rookie
        /// only thinks about it now and then and fires at anything; later, an area or cone waits for
        /// two enemies (unless it is the boss or the last one), a long-cooldown skill waits for a
        /// boss or a crowd, and a chargeable skill is charged when no hit is due before it is full.
        /// </summary>
        private bool TryChooseSkill(PlayerState self, EnemyState target, int tick, float p, out int slot, out bool charge)
        {
            slot = -1;
            charge = false;
            var thinkEvery = Math.Max(1, (int)MathF.Round(Lerp(40f, 1f, p)));
            if ((tick + _phase) % thinkEvery != 0) return false;

            var bestScore = 0f;
            var slots = self.SkillSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var skill = slots[i].Skill;
                if (skill == null || slots[i].ReadyAtTick > tick) continue;
                var hits = Hits(self, skill, target, out var hitsBoss);
                if (hits == 0) continue;
                var spread = skill.Effect != SkillEffectKind.Projectile;
                if (p >= 0.5f && spread && hits < 2 && !hitsBoss && _engaged.Count >= 2) continue;
                if (p >= 0.7f && skill.CooldownSeconds >= 9f && !hitsBoss && hits < 3) continue;
                var score = skill.Damage * hits * (hitsBoss ? 1.5f : 1f);
                if (score <= bestScore) continue;
                bestScore = score;
                slot = i;
            }
            if (slot < 0) return false;

            var chosen = slots[slot];
            if (p >= 0.5f && chosen.ChargeUnlocked && chosen.Skill.CanCharge)
            {
                var chargeTicks = (int)MathF.Ceiling((chosen.Skill.ChargeSeconds + 0.3f) / _tickSeconds);
                charge = !HitDueWithin(tick, chargeTicks);
            }
            return true;
        }

        /// <summary>How many enemies the skill would hit from here, aimed at <paramref name="target"/>.</summary>
        private int Hits(PlayerState self, SkillDefinition skill, EnemyState target, out bool hitsBoss)
        {
            hitsBoss = false;
            var aim = Direction(self.Position, target.Position);
            var hits = 0;
            if (skill.Heals) return 0; // a heal hits no enemy: it is cast for allies (TryHeal)
            switch (skill.Effect)
            {
                case SkillEffectKind.Summon: // a summon is worth calling once there is a fight near (ticket 57)
                    hitsBoss = target.IsBoss;
                    return Flat(target.Position - self.Position).Length <= 10f ? 1 : 0;
                case SkillEffectKind.Projectile:
                    if (Flat(target.Position - self.Position).Length > skill.Range) return 0;
                    hitsBoss = target.IsBoss;
                    return 1;
                case SkillEffectKind.Area:
                    foreach (var enemy in _engaged)
                    {
                        if (Flat(enemy.Position - self.Position).Length > skill.Radius) continue;
                        hits++;
                        hitsBoss |= enemy.IsBoss;
                    }
                    return hits;
                default:
                    var minCos = MathF.Cos(skill.ArcDegrees * MathF.PI / 360f);
                    foreach (var enemy in _engaged)
                    {
                        var offset = Flat(enemy.Position - self.Position);
                        var distance = offset.Length;
                        if (distance > skill.Range) continue;
                        if (distance > 1e-4f && (offset.X * aim.X + offset.Z * aim.Z) / distance < minCos) continue;
                        hits++;
                        hitsBoss |= enemy.IsBoss;
                    }
                    return hits;
            }
        }

        // --- Healing (ticket 59) ----------------------------------------------------------------

        /// <summary>
        /// A ready heal, when someone needs it: a heal bolt aimed at the most hurt ally in range (it flies to
        /// the first ally along its path, so only an aim whose first ally needs it), a heal area when it or an
        /// ally inside it is hurt. Heals come before damage. A rookie thinks about it now and then, heals the
        /// nearest and tops up scratches; later it heals the worst hurt and waits until most of a heal counts.
        /// </summary>
        private bool TryHeal(SimulationWorld world, PlayerId me, PlayerState self, int tick, float p, out PlayerCommand command)
        {
            command = default;
            var thinkEvery = Math.Max(1, (int)MathF.Round(Lerp(40f, 1f, p)));
            if ((tick + _phase) % thinkEvery != 0) return false;

            var slots = self.SkillSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var skill = slots[i].Skill;
                if (skill == null || !skill.Heals || slots[i].ReadyAtTick > tick) continue;
                switch (skill.Effect)
                {
                    case SkillEffectKind.Projectile when HealBoltAim(world, me, self, skill, p) is { } aim:
                        command = Command(Vec3.Zero, skillSlot: i, aim: aim);
                        return true;
                    case SkillEffectKind.Area when HurtInArea(world, self, skill, p):
                        command = Command(Vec3.Zero, skillSlot: i);
                        return true;
                }
            }
            return false;
        }

        /// <summary>Hurt enough for this heal: standing, here, and missing a good part of what it restores (any of it, for a rookie).</summary>
        private static bool NeedsHealing(PlayerState who, SkillDefinition skill, float p)
        {
            if (who.IsDefeated || who.IsAway) return false;
            var missing = who.MaxHealth - who.Health;
            return missing >= MathF.Max(1f, skill.Healing * Lerp(0f, 0.6f, p));
        }

        /// <summary>Which way to fire a heal bolt, or null: at the most hurt ally in range (the nearest, for a rookie) whose path reaches someone hurt first.</summary>
        private static float? HealBoltAim(SimulationWorld world, PlayerId me, PlayerState self, SkillDefinition skill, float p)
        {
            var reach = skill.Range * 0.9f; // allies move while it flies
            var thinking = p >= 0.4f;
            float? best = null;
            var bestScore = float.MaxValue;
            foreach (var id in world.PlayerIds)
            {
                if (id == me) continue;
                var ally = world.GetPlayer(id);
                if (!NeedsHealing(ally, skill, p)) continue;
                var distance = Flat(ally.Position - self.Position).Length;
                if (distance > reach) continue;
                var score = thinking ? (float)ally.Health / Math.Max(1, ally.MaxHealth) + distance * 0.001f : distance;
                if (score >= bestScore) continue;
                var direction = Direction(self.Position, ally.Position);
                if (direction.Length < 0.5f) direction = self.Facing.Length > 1e-4f ? self.Facing : new Vec3(0f, 0f, 1f);
                var first = FirstAllyAlong(world, me, self.Position, direction, skill.Range, skill.Radius);
                if (first == null || !NeedsHealing(world.GetPlayer(first.Value), skill, p)) continue; // someone fine is in the way
                bestScore = score;
                best = MathF.Atan2(direction.X, direction.Z) * (180f / MathF.PI);
            }
            return best;
        }

        /// <summary>The first standing ally other than it that a bolt fired this way would reach, as the simulation finds it.</summary>
        private static PlayerId? FirstAllyAlong(SimulationWorld world, PlayerId me, Vec3 from, Vec3 direction, float length, float radius)
        {
            PlayerId? first = null;
            var along = float.MaxValue;
            foreach (var id in world.PlayerIds)
            {
                if (id == me) continue;
                var ally = world.GetPlayer(id);
                if (ally.IsDefeated || ally.IsAway) continue;
                var offset = Flat(ally.Position - from);
                var t = Math.Clamp(offset.X * direction.X + offset.Z * direction.Z, 0f, length);
                var miss = Flat(offset - direction * t).Length;
                if (miss > radius) continue;
                var contact = MathF.Max(0f, t - MathF.Sqrt(MathF.Max(0f, radius * radius - miss * miss)));
                if (contact >= along) continue;
                along = contact;
                first = id;
            }
            return first;
        }

        /// <summary>Whether a heal area cast now would heal someone who needs it: it, or an ally within the radius.</summary>
        private static bool HurtInArea(SimulationWorld world, PlayerState self, SkillDefinition skill, float p)
        {
            var radius = skill.Radius * 0.9f;
            foreach (var id in world.PlayerIds)
            {
                var who = world.GetPlayer(id);
                if (Flat(who.Position - self.Position).Length <= radius && NeedsHealing(who, skill, p)) return true;
            }
            return false;
        }

        /// <summary>Holding a charge: keep moving as the role wants, and let go when full enough (or it has held too long).</summary>
        private PlayerCommand ContinueCharge(PlayerState self, Vec3 move, float aim, int tick)
        {
            var heldSeconds = (tick - _chargeStartedTick) * _tickSeconds;
            if (self.ChargingSlot == _chargingSlot && (self.ChargeFraction >= _chargeTarget || heldSeconds >= MaxChargeSeconds))
            {
                _chargingSlot = null; // let go: it fires at the target
                return Command(Vec3.Zero, aim: aim);
            }
            return Command(move, aim: aim);
        }

        // --- Geometry ------------------------------------------------------------------------

        private static float Lerp(float rookie, float expert, float p)
        {
            var t = Math.Clamp((p - CompanionExperience.Rookie) / (1f - CompanionExperience.Rookie), 0f, 1f);
            return rookie + (expert - rookie) * t;
        }

        /// <summary>Yaw in degrees, clockwise from +Z seen from above, from one point to another.</summary>
        private static float Yaw(Vec3 from, Vec3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z) * (180f / MathF.PI);

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, 0f, v.Z);

        private static Vec3 Direction(Vec3 from, Vec3 to)
        {
            var d = new Vec3(to.X - from.X, 0f, to.Z - from.Z);
            var length = d.Length;
            return length > 1e-4f ? d * (1f / length) : Vec3.Zero;
        }
    }
}
