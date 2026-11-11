using System;

namespace HunterSystem.Simulation
{
    /// <summary>Identifies one enemy instance. Assigned by the simulation when the enemy spawns.</summary>
    public readonly struct EnemyId : IEquatable<EnemyId>
    {
        public readonly int Value;

        public EnemyId(int value) => Value = value;

        public bool Equals(EnemyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EnemyId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(EnemyId a, EnemyId b) => a.Value == b.Value;
        public static bool operator !=(EnemyId a, EnemyId b) => a.Value != b.Value;
        public override string ToString() => $"Enemy{Value}";
    }
}
