using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// What is drawn when many players fight together (ticket 29, ADR 0023), so ten players' skills do not bury the
    /// fight. The developer's rule: your own effects bright, everyone else's faint.
    /// <list type="bullet">
    /// <item><b>Yours:</b> every flash, projectile and damage number, as before.</item>
    /// <item><b>Teammates' skill and weapon flashes:</b> smaller and dimmed toward the floor's grey, at most
    /// <see cref="MaxTeammateFlashes"/> on screen at once (newer ones are skipped while that many are up), and none
    /// further than <see cref="TeammateRange"/> from you.</item>
    /// <item><b>Teammates' projectiles:</b> drawn smaller, and not beyond <see cref="TeammateRange"/>.</item>
    /// <item><b>Teammates' damage numbers:</b> hidden once <see cref="CrowdSize"/> or more players are present.</item>
    /// <item><b>Never culled:</b> boss telegraphs, raid marks (crystal beams, the shelter), enemies' own wind-ups.</item>
    /// </list>
    /// Presentation only: the simulation is never touched, so culling cannot change what happens.
    /// </summary>
    public static class EffectCulling
    {
        public const int MaxTeammateFlashes = 6;
        public const float TeammateRange = 30f;
        public const int CrowdSize = 5;

        /// <summary>A teammate's flash or projectile is drawn at this fraction of its size.</summary>
        public const float TeammateScale = 0.65f;

        /// <summary>How far a teammate's colour is pulled toward the floor's grey (0: not at all, 1: all the way).</summary>
        public const float TeammateDim = 0.55f;

        private static readonly Color Floor = new Color(0.45f, 0.45f, 0.48f);

        /// <summary>Whether a teammate's new flash is drawn, given how many of theirs are up now and how far it is from you.</summary>
        public static bool ShowTeammateFlash(int teammateFlashesUp, float distanceFromMe) =>
            teammateFlashesUp < MaxTeammateFlashes && distanceFromMe <= TeammateRange;

        /// <summary>Whether a teammate's projectile is drawn this frame.</summary>
        public static bool ShowTeammateProjectile(float distanceFromMe) => distanceFromMe <= TeammateRange;

        /// <summary>Whether a damage number is drawn: always your own; others' only while the party is small.</summary>
        public static bool ShowDamageNumber(bool mine, int playersPresent) => mine || playersPresent < CrowdSize;

        /// <summary>A teammate's colour: the same hue, faded toward the floor.</summary>
        public static Color Dim(Color color) => Color.Lerp(color, Floor, TeammateDim);

        /// <summary>Whether <paramref name="player"/> is someone else: a teammate, culled. With no local player (a headless host) nothing is.</summary>
        public static bool IsTeammate(PlayerId player, PlayerId? me) => me.HasValue && player != me.Value;

        /// <summary>Players present (not away) in <paramref name="world"/>.</summary>
        public static int PlayersPresent(SimulationWorld world)
        {
            var count = 0;
            foreach (var id in world.PlayerIds) if (!world.GetPlayer(id).IsAway) count++;
            return count;
        }
    }
}
