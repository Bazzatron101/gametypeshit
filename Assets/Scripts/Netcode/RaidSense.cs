using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// What a bot knows about a raid (ticket 29), shared by the stress-test bots and bot teammates. People would
    /// talk this through; bots read it from the world, which every one of them sees alike:
    /// <list type="bullet">
    /// <item><b>Shelter first.</b> While a blast is coming, get into the shelter and stay there.</item>
    /// <item><b>Split up.</b> While the boss is shielded, each player present takes a crystal by their place among
    /// the players present (sorted by id), so ten split three, three, two, two.</item>
    /// <item><b>Break together.</b> A group brings its crystal down to <see cref="HoldFraction"/> of its health,
    /// then holds until every crystal still standing is that low too, and then everyone breaks theirs. A group
    /// whose crystal is already broken helps the one with the most health left.</item>
    /// </list>
    /// </summary>
    public static class RaidSense
    {
        /// <summary>Groups stop hitting their crystal at this fraction of its health until all are this low.</summary>
        public const float HoldFraction = 0.3f;

        /// <summary>A bot heading for the shelter aims to stand within this fraction of its radius from the centre.</summary>
        private const float ShelterAim = 0.55f;

        /// <summary>
        /// A blast is coming: the way into the shelter, or standing still once inside. False when no blast is coming.
        /// </summary>
        public static bool TryShelter(SimulationWorld world, Vec3 at, out PlayerCommand command)
        {
            command = PlayerCommand.None;
            var raid = world.HasGate ? world.Gate.Raid : null;
            var shelter = raid?.Shelter;
            if (!shelter.HasValue) return false;
            var offset = new Vec3(shelter.Value.X - at.X, 0f, shelter.Value.Z - at.Z);
            var distance = offset.Length;
            if (distance <= raid.Definition.ShelterRadius * ShelterAim) return true; // in: wait it out
            command = new PlayerCommand(offset.X / distance, offset.Z / distance, sprint: true);
            return true;
        }

        /// <summary>
        /// While the boss is shielded: the crystal this player should be at, and whether to hold off hitting it for
        /// now. Null when the raid is not in its shield phase (or there is no raid).
        /// </summary>
        public static EnemyId? CrystalFor(SimulationWorld world, PlayerId me, out bool hold)
        {
            hold = false;
            var raid = world.HasGate ? world.Gate.Raid : null;
            if (raid == null || raid.Phase != RaidPhase.Shielded || raid.Crystals.Count == 0) return null;

            var crystals = raid.Crystals;
            var mine = crystals[PlaceAmongPresent(world, me) % crystals.Count];
            var fractions = new List<(EnemyId id, float fraction)>(crystals.Count);
            foreach (var id in crystals)
            {
                if (!world.HasEnemy(id)) continue;
                var crystal = world.GetEnemy(id);
                if (!crystal.IsDefeated) fractions.Add((id, crystal.MaxHealth > 0 ? crystal.Health / (float)crystal.MaxHealth : 0f));
            }
            if (fractions.Count == 0) return null;

            // Ours is down: help whichever standing crystal has the most health left.
            if (!fractions.Exists(f => f.id == mine))
            {
                var most = fractions[0];
                foreach (var f in fractions) if (f.fraction > most.fraction) most = f;
                mine = most.id;
            }

            var ours = fractions.Find(f => f.id == mine).fraction;
            if (ours > HoldFraction) return mine; // still plenty to do
            foreach (var f in fractions)
            {
                if (f.fraction <= HoldFraction) continue;
                hold = true; // another group is not ready yet
                break;
            }
            return mine;
        }

        /// <summary>A shielded boss is not worth hitting: bots leave it alone until its shield is down.</summary>
        public static bool IsShieldedBoss(SimulationWorld world, EnemyState enemy) =>
            enemy.IsBoss && world.HasGate && world.Gate.Raid != null && world.Gate.Raid.IsBossShielded;

        /// <summary>Where <paramref name="me"/> comes among the players present, by id: the same answer on every machine.</summary>
        private static int PlaceAmongPresent(SimulationWorld world, PlayerId me)
        {
            var place = 0;
            foreach (var id in world.PlayerIds)
            {
                if (id.Value >= me.Value) continue;
                if (!world.GetPlayer(id).IsAway) place++;
            }
            return place;
        }
    }
}
