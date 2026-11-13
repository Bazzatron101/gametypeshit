using System;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// Where a joining or rejoining player appears. ADR 0003 puts them at the latest activated
    /// checkpoint of the gate being run; outside a gate they appear beside the party.
    /// </summary>
    public static class JoinPoint
    {
        private const float Distance = 2f;
        private const float GoldenAngleRadians = 2.39996323f; // spreads successive joiners round the circle

        /// <summary>
        /// Where a joining or returning player appears: in a running gate, at the latest checkpoint
        /// the party has activated (ADR 0003); in a game with a hub, in the hub; otherwise beside the party.
        /// </summary>
        public static Vec3 For(SimulationWorld world, PlayerId joiner, Vec3 fallback)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return world.JoinPosition(joiner) ?? BesideParty(world, joiner, fallback);
        }

        /// <summary>
        /// Beside the first standing player other than <paramref name="joiner"/> (the host's, in
        /// practice), each joiner at its own angle so nobody lands on anyone else. With nobody to
        /// stand beside, <paramref name="fallback"/>.
        /// </summary>
        public static Vec3 BesideParty(SimulationWorld world, PlayerId joiner, Vec3 fallback)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (var id in world.PlayerIds)
            {
                if (id == joiner) continue;
                var anchor = world.GetPlayer(id);
                if (anchor.IsDefeated) continue;
                var angle = joiner.Value * GoldenAngleRadians;
                return anchor.Position + new Vec3((float)Math.Cos(angle) * Distance, 0f, (float)Math.Sin(angle) * Distance);
            }
            return fallback;
        }
    }
}
