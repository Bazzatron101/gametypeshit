using System;

namespace HunterSystem.Simulation
{
    /// <summary>Identifies one player in the simulation. Single-player is the one-player case.</summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public readonly int Value;

        public PlayerId(int value) => Value = value;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
        public override string ToString() => $"Player{Value}";
    }
}
