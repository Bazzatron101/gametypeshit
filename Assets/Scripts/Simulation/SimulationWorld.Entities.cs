using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Internal mutable state records. Callers only ever see the read-only snapshots.</summary>
    public sealed partial class SimulationWorld
    {
        private sealed class Player
        {
            public PlayerId Id;
            public Vec3 Position;
            public Vec3 Facing;
            public int Health;
            public int MaxHealth;
            public PlayerLifeState LifeState;

            /// <summary>Their connection dropped: still in the run, but no teammate for the gate's rules and ignored by enemies.</summary>
            public bool Away;

            /// <summary>In the hub: ready for the next gate.</summary>
            public bool Ready;

            public int Level = 1;
            public int Xp;
            public int UnspentStatPoints;
            public int[] Stats;

            public readonly SkillDefinition[] SkillSlots = new SkillDefinition[SkillSlotCount];
            public readonly List<SkillDefinition> KnownSkills = new List<SkillDefinition>();

            /// <summary>Cooldowns are per skill (by Id), so moving a skill between slots would not reset it.</summary>
            public readonly Dictionary<string, int> SkillReadyAtTick = new Dictionary<string, int>();

            /// <summary>Length of each skill's last cooldown in ticks, after modifiers.</summary>
            public readonly Dictionary<string, int> SkillCooldownTicks = new Dictionary<string, int>();

            public readonly List<ModifierDefinition> Modifiers = new List<ModifierDefinition>();

            /// <summary>The skill being charged, or null. Cleared when it fires, on defeat, and on revive.</summary>
            public ActiveCharge Charge;

            public bool IsCharging => Charge != null;

            public void ClearCharge() => Charge = null;

            /// <summary>Returns the charge and clears it, ready to fire.</summary>
            public ActiveCharge TakeCharge()
            {
                var charge = Charge;
                Charge = null;
                return charge;
            }

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
                if (defeated)
                {
                    LifeState = PlayerLifeState.Defeated;
                    ClearCharge();
                }
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

            /// <summary>Which way it faces on the floor (unit length). Bosses turn slowly, so getting behind them works.</summary>
            public Vec3 Facing = new Vec3(0f, 0f, -1f);

            // Bosses only (ticket 18).
            /// <summary>Index into the boss's attacks while one is winding up or charging; -1 otherwise.</summary>
            public int BossAttack = -1;
            public Vec3 AttackOrigin;
            public Vec3 AttackDirection;
            public bool Charging;
            public float ChargeTravelled;
            /// <summary>How far the current charge will run: its range, or less when a wall is in the way.</summary>
            public float ChargeReach;
            public readonly List<int> ChargeHit = new List<int>();
            public bool Enraged;
            /// <summary>Enraged this tick; its adds arrive once the enemies have all acted.</summary>
            public bool EnragePending;
        }

        /// <summary>A skill being charged: which one, from which slot, and since which tick.</summary>
        private sealed class ActiveCharge
        {
            public SkillDefinition Skill;
            public int Slot;
            public int StartTick;
        }

        private sealed class Projectile
        {
            public int Id;
            public Player Owner;
            public SkillDefinition Skill;

            // Fixed at launch, so stats spent or data changed mid-flight do not alter a bolt already thrown.
            public int Damage;
            public float Speed;

            /// <summary>The skill as resolved for this use: range, hit radius, bursts, and who it has already hit.</summary>
            public ResolvedSkill Resolved;

            public Vec3 Position;
            public Vec3 Direction;
            public float Travelled;
            public int LaunchTick;
        }

        /// <summary>An attack definition with its seconds and degrees resolved once into ticks and cosines.</summary>
        private sealed class ResolvedAttack
        {
            public AttackKind Kind;
            public AttackDefinition Definition;
            public int WindupTicks;
            public int CooldownTicks;
            public float MinCos;
        }
    }
}
