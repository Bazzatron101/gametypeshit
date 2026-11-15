using System;
using System.Text;

namespace HunterSystem.Simulation
{
    /// <summary>A hunter's build (ticket 28): how tall and broad their body is drawn. Looks only; no effect on play.</summary>
    public enum Build : byte
    {
        Average = 0,
        Slim = 1,
        Broad = 2,
    }

    /// <summary>
    /// How a hunter looks (ticket 28): a body colour, picked from the game's palette by index, and a
    /// build. Chosen in the character creator and changeable later in the hub. It changes nothing in
    /// play; the simulation only keeps it, saves it and sends it to everyone in the session.
    /// </summary>
    public readonly struct CharacterLook : IEquatable<CharacterLook>
    {
        /// <summary>How many body colours the palette has. The colours themselves are the game layer's.</summary>
        public const int ColourCount = 8;

        public static int BuildCount => 3;

        /// <summary>Index into the palette, 0 to <see cref="ColourCount"/> - 1.</summary>
        public readonly byte Colour;

        public readonly Build Build;

        /// <summary>Values outside the choices (a damaged save, a dishonest client) become the nearest valid ones.</summary>
        public CharacterLook(int colour, Build build)
        {
            Colour = (byte)Math.Min(Math.Max(colour, 0), ColourCount - 1);
            Build = (int)build >= 0 && (int)build < BuildCount ? build : Build.Average;
        }

        public bool Equals(CharacterLook other) => Colour == other.Colour && Build == other.Build;
        public override bool Equals(object obj) => obj is CharacterLook other && Equals(other);
        public override int GetHashCode() => Colour * 31 + (int)Build;
        public static bool operator ==(CharacterLook a, CharacterLook b) => a.Equals(b);
        public static bool operator !=(CharacterLook a, CharacterLook b) => !a.Equals(b);
        public override string ToString() => $"colour {Colour}, {Build}";
    }

    /// <summary>The rules for a hunter's name (ticket 28). The name is chosen once, when the character is made.</summary>
    public static class HunterNames
    {
        public const int MaxLength = 16;

        /// <summary>
        /// The name as the game keeps it, or null if nothing usable is left. Letters, digits, spaces,
        /// apostrophes, hyphens and underscores are kept; anything else is dropped. Runs of spaces become
        /// one, the ends are trimmed, and it is cut to <see cref="MaxLength"/> characters.
        /// </summary>
        public static string Clean(string raw)
        {
            if (raw == null) return null;
            var sb = new StringBuilder(raw.Length);
            var lastWasSpace = true; // drops leading spaces
            foreach (var ch in raw)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                    continue;
                }
                if (!char.IsLetterOrDigit(ch) && ch != '\'' && ch != '-' && ch != '_') continue;
                sb.Append(ch);
                lastWasSpace = false;
            }
            var name = sb.ToString().Trim();
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength).TrimEnd();
            return name.Length > 0 ? name : null;
        }
    }

    /// <summary>A player changed how they look, in the hub (ticket 28).</summary>
    public sealed class LookChanged : SimulationEvent
    {
        public PlayerId Player { get; }
        public CharacterLook Look { get; }

        public LookChanged(int tick, PlayerId player, CharacterLook look) : base(tick)
        {
            Player = player;
            Look = look;
        }
    }
}
