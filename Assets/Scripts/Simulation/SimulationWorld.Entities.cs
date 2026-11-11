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

            /// <summary>Grants (modifier set) and revokes (id set) waiting for the start of the next tick.</summary>
            public readonly List<(ModifierDefinition grant, string revokeId)> PendingModifierChanges =
                new List<(ModifierDefinition, string)>();

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
