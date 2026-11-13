using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// The hub's layout (ticket 19): a small walled floor where the party gathers, with the gate
    /// board, and how long the way home stays open after a clear. One per game, in the content list.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Hub", fileName = "Hub")]
    public sealed class HubAsset : ScriptableObject
    {
        [SerializeField, Min(6f)] private float width = 24f;
        [SerializeField, Min(6f)] private float length = 20f;
        [SerializeField, Tooltip("Where the hub is in the world; gates are laid out from the origin along +Z, so keep it clear of them")] private Vector3 centre = new Vector3(0f, 0f, -60f);
        [SerializeField, Tooltip("The gate board, from the hub's centre")] private Vector3 boardOffset = new Vector3(0f, 0f, 7f);
        [SerializeField, Min(1f), Tooltip("How close a player stands to read the board")] private float boardRadius = 3f;
        [SerializeField, Min(1f), Tooltip("After a clear, seconds before the party is brought home if nobody uses the portal")] private float returnCountdownSeconds = 30f;
        [SerializeField, Min(0.5f), Tooltip("How close a player must come to the portal home to go through")] private float portalRadius = 1.5f;

        public HubDefinition ToDefinition() => new HubDefinition
        {
            Width = width,
            Length = length,
            Centre = new Vec3(centre.x, 0f, centre.z),
            BoardOffset = new Vec3(boardOffset.x, 0f, boardOffset.z),
            BoardRadius = boardRadius,
            ReturnCountdownSeconds = returnCountdownSeconds,
            PortalRadius = portalRadius,
        };
    }
}
