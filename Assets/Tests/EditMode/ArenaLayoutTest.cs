using System.Linq;
using HunterSystem.Game;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    public class ArenaLayoutTest
    {
        [Test]
        public void RingPlacesEachSpawnAtTheRadiusAroundTheCentre()
        {
            var centre = new Vector3(2f, 0f, -1f);

            var points = ArenaLayout.Ring(centre, count: 5, radius: 6f);

            Assert.That(points.Length, Is.EqualTo(5));
            Assert.That(points.Select(p => Vector3.Distance(p, centre)), Is.All.EqualTo(6f).Within(1e-4f));
            Assert.That(points.Select(p => p.y), Is.All.EqualTo(0f));
        }

        [Test]
        public void RingSpreadsSpawnsEvenly()
        {
            var points = ArenaLayout.Ring(Vector3.zero, count: 4, radius: 5f);

            var gaps = Enumerable.Range(0, 4).Select(i => Vector3.Distance(points[i], points[(i + 1) % 4])).ToArray();
            Assert.That(gaps, Is.All.EqualTo(gaps[0]).Within(1e-4f));
            Assert.That(points[0].z, Is.EqualTo(5f).Within(1e-4f), "the first one straight ahead");
        }

        [Test]
        public void EmptyRingHasNoPoints()
        {
            Assert.That(ArenaLayout.Ring(Vector3.zero, count: 0, radius: 5f), Is.Empty);
        }
    }
}
