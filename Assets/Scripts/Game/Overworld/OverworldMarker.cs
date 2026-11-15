using System.Collections.Generic;
using UnityEngine;

namespace HunterSystem.Game
{
    public enum OverworldMarkerKind
    {
        /// <summary>A gate's entrance (ticket 38): which gate, its region, and whether the guild knows it from the start.</summary>
        Gate,

        /// <summary>The guild town: its centre (the hub's centre, where the board stands) and how far its edge reaches.</summary>
        Town,

        /// <summary>A road: this marker and its children, in order, are the points along it.</summary>
        Road,

        /// <summary>A named place worth seeing from afar (a hilltop, a ruin).</summary>
        Landmark,
    }

    /// <summary>
    /// A hand-placed point in the overworld (ticket 39). The bake reads every marker into the overworld
    /// asset, so the simulation knows them as data, not only as scene objects.
    /// </summary>
    public sealed class OverworldMarker : MonoBehaviour
    {
        [SerializeField] private OverworldMarkerKind kind;
        [SerializeField, Tooltip("Gate: the gate this entrance leads into")] private GateDefinitionAsset gate;
        [SerializeField, Tooltip("Gate: the region it stands in (ticket 36)")] private string region = "greenmarch";
        [SerializeField, Tooltip("Gate: known to the guild from the start (story gates, each region's first)")] private bool knownFromStart;
        [SerializeField, Min(0f), Tooltip("Town: how far its edge reaches. Road: its half-width.")] private float radius = 40f;
        [SerializeField, Tooltip("Landmark: its name")] private string label;

        public OverworldMarkerKind Kind => kind;
        public GateDefinitionAsset Gate => gate;
        public string Region => region;
        public bool KnownFromStart => knownFromStart;
        public float Radius => radius;
        public string Label => string.IsNullOrEmpty(label) ? name : label;

        /// <summary>A road's points: this marker, then its children in order.</summary>
        public IEnumerable<Vector3> RoadPoints()
        {
            yield return transform.position;
            foreach (Transform child in transform) yield return child.position;
        }

        /// <summary>For editor tooling that places markers in code.</summary>
        public void Set(OverworldMarkerKind markerKind, GateDefinitionAsset markerGate = null, string markerRegion = null, bool known = false, float markerRadius = 40f, string markerLabel = null)
        {
            kind = markerKind;
            gate = markerGate;
            region = markerRegion ?? region;
            knownFromStart = known;
            radius = markerRadius;
            label = markerLabel;
        }

        private void OnDrawGizmos()
        {
            switch (kind)
            {
                case OverworldMarkerKind.Gate:
                    Gizmos.color = knownFromStart ? new Color(0.3f, 0.9f, 1f) : new Color(0.8f, 0.4f, 1f);
                    Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, new Vector3(3f, 4f, 3f));
                    break;
                case OverworldMarkerKind.Town:
                    Gizmos.color = new Color(1f, 0.85f, 0.3f);
                    Gizmos.DrawWireSphere(transform.position, radius);
                    break;
                case OverworldMarkerKind.Road:
                    Gizmos.color = new Color(0.75f, 0.6f, 0.4f);
                    Vector3? previous = null;
                    foreach (var p in RoadPoints())
                    {
                        if (previous.HasValue) Gizmos.DrawLine(previous.Value + Vector3.up, p + Vector3.up);
                        previous = p;
                    }
                    break;
                case OverworldMarkerKind.Landmark:
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(transform.position, 3f);
                    break;
            }
        }
    }
}
