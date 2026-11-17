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
            /// <summary>The hunter's name (ticket 28), or null for one with none (a bare test player).</summary>
            public string Name;
            /// <summary>How the hunter looks (ticket 28). Changes nothing in play.</summary>
            public CharacterLook Look;
            public Vec3 Position;
            /// <summary>Where they stood when this tick began: walls and steep ground slide them back from here (ticket 37). Not in snapshots: it is always the position between ticks.</summary>
            public Vec3 SetOffFrom;
            public Vec3 Facing;

            /// <summary>The character owns a mount (ticket 42; saved with it).</summary>
            public bool HasMount;

            /// <summary>Riding it now.</summary>
            public bool Mounted;

            /// <summary>Flying (ticket 90): how high above the ground. 0 for anyone not in the air on wings.</summary>
            public float FlightHeight;
            public int Health;
            public int MaxHealth;
            public PlayerLifeState LifeState;

            /// <summary>Their connection dropped: still in the run, but no teammate for the gate's rules and ignored by enemies.</summary>
            public bool Away;

            /// <summary>In the hub: ready for the next gate.</summary>
            public bool Ready;

            /// <summary>The character's class, or null.</summary>
            public ClassDefinition Class;

            /// <summary>The hunter's species (ticket 81), or null (one from before species, a bare test player).</summary>
            public SpeciesDefinition Species;

            /// <summary>The last tick this player hit or was hit (ticket 81: the koala's healing waits for a quiet spell). 0: never.</summary>
            public int LastFightTick;

            /// <summary>The specialties picked (ticket 26), one per tier, lowest first.</summary>
            public readonly List<SpecialtyDefinition> Specialties = new List<SpecialtyDefinition>();

            /// <summary>The ranks whose class trial this hunter has passed (ticket 26), lowest first.</summary>
            public readonly List<string> PassedTrials = new List<string>();

            /// <summary>Gold from selling duplicates.</summary>
            public int Gold;

            /// <summary>Rewards drawn again once the pool was owned, one entry per copy, to sell.</summary>
            public readonly List<LootDefinition> Duplicates = new List<LootDefinition>();

            /// <summary>Every piece of gear the character has, worn or not, in the order found (ticket 23).</summary>
            public readonly List<GearDefinition> Bag = new List<GearDefinition>();

            /// <summary>The piece worn in each <see cref="GearSlot"/>, or null. Always one from <see cref="Bag"/>.</summary>
            public readonly GearDefinition[] Worn = new GearDefinition[GearDefinition.SlotCount];

            /// <summary>Where this tick's command aims skills (the camera), or null to use the facing. Set each tick the player is standing, before any skill; never saved.</summary>
            public Vec3? Aim;

            /// <summary>The tick the current jump took off; 0 on the ground.</summary>
            public int JumpStartTick;

            /// <summary>This player's jump height as a percentage of the base jump (classes raise it).</summary>
            public int JumpHeightPercent = 100;

            /// <summary>Moved at sprint speed on the last tick.</summary>
            public bool Sprinting;

            /// <summary>After an attack or skill starts, sprinting waits until this tick.</summary>
            public int SprintResumesAtTick;

            public bool IsAirborne => JumpStartTick != 0;

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
            /// <summary>Where they stood when this tick began: walls and steep ground slide them back from here (ticket 37). Not in snapshots: it is always the position between ticks.</summary>
            public Vec3 SetOffFrom;
            public int Health;

            /// <summary>Its health when full: the definition's, scaled for the party size when it spawned.</summary>
            public int MaxHealth;

            /// <summary>Its hits deal this percentage of their authored damage (party-size scaling).</summary>
            public int DamagePercent = 100;

            /// <summary>Leaked from a gate out in the overworld (ticket 43): that gate's id; null otherwise.</summary>
            public string LeakedFrom;

            /// <summary>A leaked monster's home (its gate's entrance) and how far from it it will chase. Null for none.</summary>
            public Vec3? Home;
            public float Leash;

            /// <summary>Steering's last answer (ticket 43): where to head, for which goal, worked out on which tick. Not in snapshots.</summary>
            public Vec3 Waypoint;
            public Vec3 WaypointGoal;
            public int WaypointTick = int.MinValue;

            public int AttackReadyAtTick;

            /// <summary>0 when not winding up.</summary>
            public int AttackLandsAtTick;
            public Player AttackTarget;

            /// <summary>The summon a wind-up is aimed at (ticket 57), by id; 0 when it is aimed at a player or at nothing.</summary>
            public int AttackSummonId;

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
