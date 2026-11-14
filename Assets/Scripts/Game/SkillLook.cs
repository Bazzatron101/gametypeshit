using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// How a skill looks on screen (ticket 35): a colour from its class, so you can tell at a glance
    /// whose kind of skill it was (Warrior red, Rogue purple, Archer green, Wizard blue), shifted a
    /// little per skill so two skills of one class still differ. Skills with no class are pale gold.
    /// </summary>
    public static class SkillLook
    {
        public static Color ClassColor(string classId)
        {
            switch (classId)
            {
                case "warrior": return Color.HSVToRGB(0.02f, 0.8f, 0.95f);
                case "rogue": return Color.HSVToRGB(0.78f, 0.65f, 0.9f);
                case "archer": return Color.HSVToRGB(0.32f, 0.7f, 0.85f);
                case "wizard": return Color.HSVToRGB(0.58f, 0.75f, 1f);
                default: return Color.HSVToRGB(0.13f, 0.45f, 1f);
            }
        }

        /// <summary>The skill's colour: its class's hue, nudged by up to ±0.05 by its id.</summary>
        public static Color SkillColor(SkillDefinition skill)
        {
            if (skill == null) return ClassColor(null);
            Color.RGBToHSV(ClassColor(skill.ClassId), out var h, out var s, out var v);
            var nudge = (Hash(skill.Id) % 11 - 5) * 0.01f;
            return Color.HSVToRGB(Mathf.Repeat(h + nudge, 1f), s, v);
        }

        /// <summary>A stable hash of the id (string.GetHashCode differs between runs).</summary>
        private static int Hash(string id)
        {
            unchecked
            {
                var h = 17;
                foreach (var c in id ?? "") h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }
    }
}
