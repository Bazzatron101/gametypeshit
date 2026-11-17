using System;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// What the overworld is dressed with (ticket 49): the nature scattered on every tile as it streams in
    /// (trees, bushes, rocks, grass), and the buildings the bake lays out in towns and villages. The art is
    /// placeholder (labelled AI, made by Hunter System > Overworld > Make Placeholder Environment Art): swap a
    /// kind's mesh and materials here for real art and nothing else changes. Tune the numbers here; the
    /// nature follows at once, the buildings at the next bake.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Overworld Environment", fileName = "Environment")]
    public sealed class EnvironmentAsset : ScriptableObject
    {
        /// <summary>One kind of scattered nature: a pine, a rock, a grass tuft.</summary>
        [Serializable]
        public sealed class PropKind
        {
            public string id = "";
            public Mesh mesh;
            [Tooltip("One per submesh")] public Material[] materials = new Material[0];
            [Min(0f), Tooltip("How many per hectare (100 × 100 m) where it grows at all")] public float perHectare = 10f;
            [Range(0f, 1f), Tooltip("Share of the ground it grows on: 1 everywhere, lower for patches (woods, clearings)")] public float cover = 0.5f;
            [Min(1f), Tooltip("How big its patches are, in metres")] public float patchSize = 120f;
            [Tooltip("Patches of kinds with the same group line up (a wood's pines and bushes); different groups are independent")] public int patchGroup;
            [Min(0.01f)] public float minScale = 0.8f;
            [Min(0.01f)] public float maxScale = 1.3f;
            [Range(0f, 60f), Tooltip("Steepest ground it stands on, in degrees")] public float maxSlope = 25f;
            [Min(0f), Tooltip("Metres it keeps from roads, towns, buildings and gate entrances")] public float clearance = 4f;
            [Min(0f), Tooltip("Sunk this far into the ground, so slopes show no gap")] public float sink = 0.2f;
            [Min(10f), Tooltip("Drawn only on tiles this close to the camera")] public float drawDistance = 600f;
            public bool castShadows = true;
            [Min(0f), Tooltip("Grows only within this many metres of steep ground (mountain sides): boulders under the peaks. 0 = anywhere.")] public float nearSteepWithin;
            [Range(5f, 80f), Tooltip("What counts as steep ground for Near Steep Within, in degrees")] public float steepDegrees = 30f;
        }

        /// <summary>One kind of building, for town layouts.</summary>
        [Serializable]
        public sealed class BuildingKind
        {
            public string id = "";
            public Mesh mesh;
            [Tooltip("One per submesh")] public Material[] materials = new Material[0];
            [Tooltip("Width (X) and depth (Z) of the ground it blocks, in metres. Its door faces +Z.")] public Vector2 footprint = new Vector2(6f, 6f);
            [Tooltip("What it is in a layout: hall, inn, shop, house, well, tower or keep")] public string role = "house";
        }

        /// <summary>
        /// A region's look for its buildings (ticket 84): each material listed is swapped for another, so the same
        /// house is thatch and timber in one region and stone and slate in the next.
        /// </summary>
        [Serializable]
        public sealed class RegionLook
        {
            [Tooltip("The region's id, as gates and places name it: greenmarch, brokenstone, saltreach, fenward, ashen, riftlands")] public string region = "";
            public MaterialSwap[] swaps = new MaterialSwap[0];
        }

        [Serializable]
        public sealed class MaterialSwap
        {
            public Material from;
            public Material to;
        }

        [Tooltip("Changes every tile's scatter at once")] public int seed = 49;
        public PropKind[] props = new PropKind[0];
        public BuildingKind[] buildings = new BuildingKind[0];
        [Tooltip("Each region's materials for its buildings (ticket 84); a region not listed keeps the kinds' own")] public RegionLook[] regionLooks = new RegionLook[0];

        [Header("Towns")]
        [Min(0f), Tooltip("Open ground kept round a town's centre (the square, the board, where players stand)")] public float squareRadius = 12f;
        [Min(0f), Tooltip("Metres a building keeps from a road's edge")] public float roadClearance = 3f;
        [Min(0f), Tooltip("Metres between buildings")] public float buildingGap = 3f;

        /// <summary>A building kind's materials as they look in <paramref name="region"/>.</summary>
        public Material[] MaterialsFor(BuildingKind kind, string region)
        {
            if (kind == null) return new Material[0];
            RegionLook look = null;
            foreach (var l in regionLooks ?? new RegionLook[0]) if (l != null && l.region == region) { look = l; break; }
            if (look == null || look.swaps == null || look.swaps.Length == 0) return kind.materials;
            var result = (Material[])kind.materials.Clone();
            for (var i = 0; i < result.Length; i++)
                foreach (var swap in look.swaps)
                    if (swap != null && swap.from != null && swap.to != null && result[i] == swap.from) { result[i] = swap.to; break; }
            return result;
        }

        public BuildingKind Building(string id)
        {
            foreach (var b in buildings) if (b != null && b.id == id) return b;
            return null;
        }
    }
}
