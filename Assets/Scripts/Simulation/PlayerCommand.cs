using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// One player's input for one tick, in world space. Movement is on the floor plane (X/Z) and
    /// is clamped to length 1, so diagonals are not faster and analog input keeps partial speeds.
    /// The game layer turns camera-relative stick input into this world direction. Buttons are
    /// true only on the tick that should act on the press.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public static readonly PlayerCommand None = default;

        /// <summary>This command with a different stat allocation (or none).</summary>
        public PlayerCommand WithAllocateStat(Stat? stat) =>
            new PlayerCommand(MoveX, MoveZ, LightAttack, Dodge, HeavyAttack, stat, UseSkillSlot, AssignSkill, HeldSkillSlots, Respawn, SetReady, Jump, Sprint, AimYawDegrees, SellItemId, EquipGearId, UnequipSlot, ChangeLook);

        /// <summary>This command with a piece of gear to wear (or none).</summary>
        public PlayerCommand WithEquipGear(string gearId) =>
            new PlayerCommand(MoveX, MoveZ, LightAttack, Dodge, HeavyAttack, AllocateStat, UseSkillSlot, AssignSkill, HeldSkillSlots, Respawn, SetReady, Jump, Sprint, AimYawDegrees, SellItemId, gearId, UnequipSlot, ChangeLook);

        public readonly float MoveX, MoveZ;
        public readonly bool LightAttack;
        public readonly bool HeavyAttack;
        public readonly bool Dodge;

        /// <summary>Spend one unspent stat point on this stat, or null for none. One point per tick.</summary>
        public readonly Stat? AllocateStat;

        /// <summary>Use the skill in this bar slot (0 to 5, keys 1 to 6), or null for none. This is the press.</summary>
        public readonly int? UseSkillSlot;

        /// <summary>Which skill keys are held down this tick: bit N is slot N. A charge lasts while its own key stays held.</summary>
        public readonly int HeldSkillSlots;

        /// <summary>Rearrange the skill bar (a loadout change), or null for none. One per tick.</summary>
        public readonly SkillAssignment? AssignSkill;

        /// <summary>
        /// A defeated player asking to come back (a press). In a gate, solo, it spends a revive or,
        /// with none left, starts the gate again. In co-op and outside a gate it does nothing.
        /// </summary>
        public readonly bool Respawn;

        /// <summary>In the hub: mark this player ready (true) or not (false) for the next gate; null for no change.</summary>
        public readonly bool? SetReady;

        /// <summary>A jump (the press).</summary>
        public readonly bool Jump;

        /// <summary>The sprint key is held.</summary>
        public readonly bool Sprint;

        /// <summary>At a store: sell one duplicate of this item (a press), or null for none (ticket 22).</summary>
        public readonly string SellItemId;

        /// <summary>Wear this piece from the bag in its slot (a press), or null for none (ticket 23). What was there goes back to the bag.</summary>
        public readonly string EquipGearId;

        /// <summary>Take off whatever is worn in this slot (a press), or null for none. Applied before <see cref="EquipGearId"/>.</summary>
        public readonly GearSlot? UnequipSlot;

        /// <summary>In the hub: change how the player looks (a press), or null for none (ticket 28).</summary>
        public readonly CharacterLook? ChangeLook;

        /// <summary>Steps of the aim's yaw in a full turn. The aim is stored in these steps, so it is exactly the same after the wire.</summary>
        public const int AimSteps = 65536;

        /// <summary>
        /// Where the player is looking (the camera), as a yaw in <see cref="AimSteps"/> steps
        /// clockwise from +Z seen from above; meaningful only when <see cref="HasAim"/>. Skills fire
        /// this way. A source with no camera (a bot) sends none, and skills go where the player faces.
        /// </summary>
        public readonly ushort AimYaw;
        public readonly bool HasAim;

        /// <summary>The aim in degrees, or null for none.</summary>
        public float? AimYawDegrees => HasAim ? AimYaw * (360f / AimSteps) : (float?)null;

        /// <summary>The aim as a direction on the floor (length 1); +Z when there is none.</summary>
        public Vec3 AimDirection
        {
            get
            {
                var radians = (HasAim ? AimYaw : 0) * (2f * MathF.PI / AimSteps);
                return new Vec3(MathF.Sin(radians), 0f, MathF.Cos(radians));
            }
        }

        public PlayerCommand(float moveX, float moveZ, bool lightAttack = false, bool dodge = false, bool heavyAttack = false,
            Stat? allocateStat = null, int? useSkillSlot = null, SkillAssignment? assignSkill = null, int heldSkillSlots = 0,
            bool respawn = false, bool? setReady = null, bool jump = false, bool sprint = false, float? aimYawDegrees = null, string sellItemId = null,
            string equipGearId = null, GearSlot? unequipSlot = null, CharacterLook? changeLook = null)
        {
            ChangeLook = changeLook;
            SellItemId = sellItemId;
            EquipGearId = equipGearId;
            UnequipSlot = unequipSlot;
            HasAim = aimYawDegrees.HasValue && !float.IsNaN(aimYawDegrees.Value) && !float.IsInfinity(aimYawDegrees.Value);
            AimYaw = HasAim ? ToAimSteps(aimYawDegrees.Value) : (ushort)0;
            Jump = jump;
            Sprint = sprint;
            Respawn = respawn;
            SetReady = setReady;
            var length = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
            if (length > 1f) { moveX /= length; moveZ /= length; }
            MoveX = moveX;
            MoveZ = moveZ;
            LightAttack = lightAttack;
            HeavyAttack = heavyAttack;
            Dodge = dodge;
            AllocateStat = allocateStat;
            UseSkillSlot = useSkillSlot;
            AssignSkill = assignSkill;
            HeldSkillSlots = heldSkillSlots;
        }

        private static ushort ToAimSteps(float degrees)
        {
            var turn = degrees / 360f;
            turn -= MathF.Floor(turn); // into [0, 1)
            return (ushort)((int)MathF.Round(turn * AimSteps) % AimSteps);
        }

        public bool IsSkillHeld(int slot) => slot >= 0 && slot < 32 && (HeldSkillSlots & (1 << slot)) != 0;
    }
}
