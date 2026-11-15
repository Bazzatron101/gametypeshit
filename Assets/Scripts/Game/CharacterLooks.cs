using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// What a <see cref="CharacterLook"/> means on screen (ticket 28): the palette of body colours and
    /// how each build scales the body. The simulation only keeps the choice; this draws it.
    /// </summary>
    public static class CharacterLooks
    {
        private static readonly Color[] Palette =
        {
            new Color(0.25f, 0.55f, 0.95f), // blue
            new Color(0.85f, 0.25f, 0.25f), // red
            new Color(0.30f, 0.75f, 0.35f), // green
            new Color(0.95f, 0.75f, 0.20f), // gold
            new Color(0.60f, 0.35f, 0.85f), // violet
            new Color(0.95f, 0.50f, 0.20f), // orange
            new Color(0.25f, 0.80f, 0.80f), // teal
            new Color(0.85f, 0.85f, 0.85f), // white
        };

        private static readonly string[] ColourNames = { "Blue", "Red", "Green", "Gold", "Violet", "Orange", "Teal", "White" };

        public static Color Colour(CharacterLook look) => Palette[look.Colour % Palette.Length];

        public static Color Colour(int index) => Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];

        public static string ColourName(int index) => ColourNames[Mathf.Clamp(index, 0, ColourNames.Length - 1)];

        /// <summary>The body's scale for a build, relative to the average (width, height, depth).</summary>
        public static Vector3 Scale(Build build)
        {
            switch (build)
            {
                case Build.Slim: return new Vector3(0.82f, 1.06f, 0.82f);
                case Build.Broad: return new Vector3(1.25f, 0.96f, 1.25f);
                default: return Vector3.one;
            }
        }
    }
}
