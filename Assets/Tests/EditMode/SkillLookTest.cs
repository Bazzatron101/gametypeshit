using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>Skills are drawn in their class's colour, each a little different (ticket 35).</summary>
    public class SkillLookTest
    {
        private static float Hue(Color c)
        {
            Color.RGBToHSV(c, out var h, out _, out _);
            return h;
        }

        private static float HueGap(float a, float b) => Mathf.Min(Mathf.Abs(a - b), 1f - Mathf.Abs(a - b));

        [Test]
        public void EveryClassHasItsOwnClearlyDifferentColour()
        {
            var hues = new[] { "warrior", "rogue", "archer", "wizard", null }.Select(id => Hue(SkillLook.ClassColor(id))).ToArray();
            for (var i = 0; i < hues.Length; i++)
                for (var j = i + 1; j < hues.Length; j++)
                    Assert.That(HueGap(hues[i], hues[j]), Is.GreaterThan(0.1f), $"classes {i} and {j}");
        }

        [Test]
        public void ASkillStaysCloseToItsClassColourAndAlwaysLooksTheSame()
        {
            foreach (var classId in new[] { "warrior", "rogue", "archer", "wizard" })
            {
                foreach (var id in new[] { "a", "rending-slash", "quick-shot", "fire-bolt", "monarchs-decree" })
                {
                    var skill = new SkillDefinition { Id = id, ClassId = classId };
                    var colour = SkillLook.SkillColor(skill);
                    Assert.That(HueGap(Hue(colour), Hue(SkillLook.ClassColor(classId))), Is.LessThanOrEqualTo(0.051f), $"{classId}/{id}");
                    Assert.That(SkillLook.SkillColor(new SkillDefinition { Id = id, ClassId = classId }), Is.EqualTo(colour), "the same every time");
                }
            }
        }

        [Test]
        public void TwoSkillsOfOneClassCanBeToldApart()
        {
            var a = SkillLook.SkillColor(new SkillDefinition { Id = "rending-slash", ClassId = "warrior" });
            var b = SkillLook.SkillColor(new SkillDefinition { Id = "ground-slam", ClassId = "warrior" });
            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
