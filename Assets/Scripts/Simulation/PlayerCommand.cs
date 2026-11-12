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

        public PlayerCommand(float moveX, float moveZ, bool lightAttack = false, bool dodge = false, bool heavyAttack = false,
            Stat? allocateStat = null, int? useSkillSlot = null, SkillAssignment? assignSkill = null, int heldSkillSlots = 0)
        {
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

        public bool IsSkillHeld(int slot) => slot >= 0 && slot < 32 && (HeldSkillSlots & (1 << slot)) != 0;
    }
}
