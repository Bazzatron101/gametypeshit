using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    public enum BossAttackKind : byte
    {
        /// <summary>A wide arc in front. The answer: get behind it.</summary>
        Sweep,

        /// <summary>A circle all around it. The answer: get out.</summary>
        Slam,

        /// <summary>A run along a straight line at someone out of reach. The answer: step aside.</summary>
        Charge,
    }

    /// <summary>
    /// One of a boss's attacks. Every attack is aimed when it starts (its telegraph) and lands
    /// after <see cref="WindupSeconds"/> on whoever is then inside the marked area, so it is read
    /// and answered by moving, or by dodging through it.
    /// </summary>
    public sealed class BossAttackDefinition
    {
        public BossAttackKind Kind { get; set; }
        public int Damage { get; set; } = 20;
        public float WindupSeconds { get; set; } = 0.8f;

        /// <summary>After the attack lands (or a charge ends) before the next can start.</summary>
        public float CooldownSeconds { get; set; } = 1.5f;

        /// <summary>Sweep: how far it reaches. Charge: how far it runs.</summary>
        public float Range { get; set; } = 4f;

        /// <summary>Sweep: the arc's full width.</summary>
        public float ArcDegrees { get; set; } = 120f;

        /// <summary>Slam: the circle's radius. Charge: how close to its path a player is hit (half the lane's width).</summary>
        public float Radius { get; set; } = 3f;

        /// <summary>Charge: metres per second.</summary>
        public float Speed { get; set; } = 10f;

        /// <summary>
        /// Whether <paramref name="point"/> is inside the area this attack marks when started at
        /// <paramref name="origin"/> facing <paramref name="direction"/> (unit length, on the floor).
        /// The simulation hits with it, the view draws it, and bots avoid it.
        /// </summary>
        /// <param name="reach">A charge's actual length (shorter than <see cref="Range"/> when a wall is in the way); null for the full range.</param>
        public bool Covers(Vec3 origin, Vec3 direction, Vec3 point, float? reach = null)
        {
            var offset = new Vec3(point.X - origin.X, 0f, point.Z - origin.Z);
            var distance = offset.Length;
            switch (Kind)
            {
                case BossAttackKind.Slam:
                    return distance <= Radius;
                case BossAttackKind.Sweep:
                    if (distance > Range) return false;
                    if (distance < 1e-4f) return true;
                    var cos = (offset.X * direction.X + offset.Z * direction.Z) / distance;
                    return cos >= (float)Math.Cos(ArcDegrees * Math.PI / 360.0);
                case BossAttackKind.Charge:
                    var along = offset.X * direction.X + offset.Z * direction.Z;
                    if (along < -Radius || along > (reach ?? Range) + Radius) return false;
                    var across = Math.Abs(offset.X * direction.Z - offset.Z * direction.X);
                    return across <= Radius;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// What makes an enemy a boss: a set of telegraphed attacks it chooses between by where the
    /// players are, a slow turn (so getting behind it works), and an enrage partway through.
    /// </summary>
    public sealed class BossDefinition
    {
        /// <summary>At most one of each kind is used.</summary>
        public List<BossAttackDefinition> Attacks { get; } = new List<BossAttackDefinition>();

        /// <summary>How fast it turns to face its target, in degrees per second. Slow enough that getting behind it is a real answer.</summary>
        public float TurnDegreesPerSecond { get; set; } = 60f;

        /// <summary>It enrages once, when its health falls to this percentage of its maximum or below. 0: never.</summary>
        public int EnrageAtHealthPercent { get; set; } = 50;

        /// <summary>Enraged, its windups and cooldowns take this percentage of their usual time.</summary>
        public int EnragedTimePercent { get; set; } = 70;

        /// <summary>Enemies that join the fight when it enrages, placed relative to the boss.</summary>
        public List<RoomSpawn> EnrageAdds { get; } = new List<RoomSpawn>();

        public BossAttackDefinition Attack(BossAttackKind kind)
        {
            foreach (var attack in Attacks) if (attack.Kind == kind) return attack;
            return null;
        }
    }
}
