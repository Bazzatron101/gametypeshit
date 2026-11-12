using System;
using System.Linq;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// Where a joining or rejoining player appears. ADR 0003 puts them at the latest activated
    /// checkpoint; until gates have checkpoints (ticket 17), they appear beside the party.
    /// </summary>
    public static class JoinPoint
    {
        private const float Distance = 2f;
        private const float GoldenAngleRadians = 2.39996323f; // spreads successive joiners round the circle

        /// <summary>
        /// Beside the first standing player other than <paramref name="joiner"/> (the host's, in
        /// practice), each joiner at its own angle so nobody lands on anyone else. With nobody to
        /// stand beside, <paramref name="fallback"/>.
        /// </summary>
        public static Vec3 BesideParty(SimulationWorld world, PlayerId joiner, Vec3 fallback)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var anchor = world.PlayerIds.Where(id => id != joiner).Select(world.GetPlayer).FirstOrDefault(p => !p.IsDefeated);
            if (anchor == null) return fallback;

            var angle = joiner.Value * GoldenAngleRadians;
            return anchor.Position + new Vec3((float)Math.Cos(angle) * Distance, 0f, (float)Math.Sin(angle) * Distance);
        }
    }
}
