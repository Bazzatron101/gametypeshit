using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// One player's input for one tick. Movement is on the floor plane (X/Z) and is clamped
    /// to length 1, so diagonals are not faster and analog input keeps partial speeds.
    /// Buttons are true only on the tick that should act on the press.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public static readonly PlayerCommand None = default;

        public readonly float MoveX, MoveZ;
        public readonly bool LightAttack;
        public readonly bool Dodge;

        public PlayerCommand(float moveX, float moveZ, bool lightAttack = false, bool dodge = false)
        {
            var length = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
            if (length > 1f) { moveX /= length; moveZ /= length; }
            MoveX = moveX;
            MoveZ = moveZ;
            LightAttack = lightAttack;
            Dodge = dodge;
        }
    }
}
