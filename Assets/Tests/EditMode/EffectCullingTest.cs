using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The effect-culling rules (ticket 29, ADR 0023): your own effects bright, everyone else's faint, so ten players'
    /// skills do not bury the fight. Presentation only.
    /// </summary>
    public class EffectCullingTest
    {
        private static readonly PlayerId Me = new PlayerId(1);
        private static readonly PlayerId Friend = new PlayerId(2);

        [Test]
        public void OnlyOtherPlayersAreTeammatesAndWithNoLocalPlayerNobodyIs()
        {
            Assert.That(EffectCulling.IsTeammate(Friend, Me), Is.True);
            Assert.That(EffectCulling.IsTeammate(Me, Me), Is.False);
            Assert.That(EffectCulling.IsTeammate(Friend, null), Is.False, "a headless host culls nothing");
        }

        [Test]
        public void AtMostSixTeammateFlashesAndNoneBeyondThirtyMetres()
        {
            Assert.That(EffectCulling.ShowTeammateFlash(0, 5f), Is.True);
            Assert.That(EffectCulling.ShowTeammateFlash(5, 5f), Is.True);
            Assert.That(EffectCulling.ShowTeammateFlash(6, 5f), Is.False, "six already up");
            Assert.That(EffectCulling.ShowTeammateFlash(0, 30f), Is.True);
            Assert.That(EffectCulling.ShowTeammateFlash(0, 30.5f), Is.False, "too far to matter");
            Assert.That(EffectCulling.ShowTeammateProjectile(31f), Is.False);
        }

        [TestCase(true, 10, true)]
        [TestCase(false, 4, true)]
        [TestCase(false, 5, false)]
        [TestCase(false, 10, false)]
        public void TeammatesDamageNumbersHideInACrowdAndYoursNever(bool mine, int present, bool shown)
        {
            Assert.That(EffectCulling.ShowDamageNumber(mine, present), Is.EqualTo(shown));
        }

        [Test]
        public void ATeammatesColourKeepsItsHueButFades()
        {
            var red = new Color(1f, 0.1f, 0.1f);

            var dim = EffectCulling.Dim(red);

            Assert.That(dim.r, Is.LessThan(red.r));
            Assert.That(dim.r, Is.GreaterThan(dim.g), "still reads as red");
            Assert.That(EffectCulling.TeammateScale, Is.LessThan(1f));
        }
    }
}
