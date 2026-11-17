using System.Collections.Generic;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Placeholder looks for ticket 61's gear: the four sets and the class weapons. Each set shares a
    /// shape per slot in its own colours; each weapon is built from its kind (sword, daggers, bow,
    /// staff, knuckles, sceptre, rod). Like every placeholder here, they are labelled "AI".
    /// </summary>
    public static partial class GearLookSetup
    {
        private static readonly Dictionary<string, Color> MoreColours = new Dictionary<string, Color>
        {
            ["Gold"] = new Color(0.95f, 0.76f, 0.25f),
            ["Bronze"] = new Color(0.62f, 0.42f, 0.22f),
            ["Moss"] = new Color(0.30f, 0.42f, 0.24f),
            ["Night"] = new Color(0.12f, 0.12f, 0.20f),
            ["Violet"] = new Color(0.45f, 0.25f, 0.65f),
            ["Star"] = new Color(0.22f, 0.30f, 0.62f),
            ["Silver"] = new Color(0.86f, 0.88f, 0.92f),
            ["Bone"] = new Color(0.88f, 0.84f, 0.72f),
            ["Void"] = new Color(0.30f, 0.05f, 0.40f),
            ["Spirit"] = new Color(0.45f, 0.95f, 0.80f),
            ["Jade"] = new Color(0.20f, 0.65f, 0.45f),
            ["Sky"] = new Color(0.60f, 0.85f, 0.95f),
        };

        // --- Armour shapes, one per slot and style; (main, trim) colours per set ----------------

        private static Part[] Helm(string main, string trim) => new[]
        {
            P(PrimitiveType.Sphere, main, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.68f, 1.1f)),
            P(PrimitiveType.Cube, trim, new Vector3(0f, 0.32f, 0f), new Vector3(0.08f, 0.2f, 0.7f)),
        };

        private static Part[] Hat(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, 0.02f, 0f), new Vector3(1.5f, 0.03f, 1.5f)),
            P(PrimitiveType.Cylinder, main, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.18f, 0.9f)),
            P(PrimitiveType.Cylinder, trim, new Vector3(0f, 0.08f, 0f), new Vector3(0.92f, 0.04f, 0.92f)),
        };

        private static Part[] Hood(string main, string trim) => new[]
        {
            P(PrimitiveType.Sphere, main, new Vector3(0f, 0.02f, -0.02f), new Vector3(1.07f, 0.64f, 1.07f)),
            P(PrimitiveType.Cube, trim, new Vector3(0f, -0.05f, 0.5f), new Vector3(0.6f, 0.08f, 0.06f)),
        };

        private static Part[] Circlet(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, -0.02f, 0f), new Vector3(1.04f, 0.04f, 1.04f)),
            P(PrimitiveType.Sphere, trim, new Vector3(0f, 0.02f, 0.52f), Vector3.one * 0.14f),
        };

        private static Part[] Plate(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, 0.15f, 0f), new Vector3(1.12f, 0.38f, 1.12f)),
            P(PrimitiveType.Sphere, trim, new Vector3(-0.54f, 0.52f, 0f), Vector3.one * 0.46f),
            P(PrimitiveType.Sphere, trim, new Vector3(0.54f, 0.52f, 0f), Vector3.one * 0.46f),
        };

        private static Part[] Coat(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, -0.1f, 0f), new Vector3(1.09f, 0.48f, 1.09f)),
            P(PrimitiveType.Cylinder, trim, new Vector3(0f, -0.2f, 0f), new Vector3(1.11f, 0.05f, 1.11f)),
        };

        private static Part[] Vest(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, 0.1f, 0f), new Vector3(1.07f, 0.3f, 1.07f)),
            P(PrimitiveType.Cube, trim, new Vector3(0f, 0.1f, 0.5f), new Vector3(0.08f, 0.55f, 0.08f), new Vector3(0f, 0f, 30f)),
        };

        private static Part[] Robe(string main, string trim) => new[]
        {
            P(PrimitiveType.Cylinder, main, new Vector3(0f, -0.4f, 0f), new Vector3(1.1f, 0.6f, 1.1f)),
            P(PrimitiveType.Cylinder, trim, new Vector3(0f, 0.08f, 0f), new Vector3(1.12f, 0.04f, 1.12f)),
            P(PrimitiveType.Cylinder, trim, new Vector3(0f, -0.98f, 0f), new Vector3(1.14f, 0.03f, 1.14f)),
        };

        private static Part[] Gloves(string main, string trim, float size) => new[]
        {
            P(PrimitiveType.Cube, main, new Vector3(-0.55f, 0f, 0f), Vector3.one * size),
            P(PrimitiveType.Cube, main, new Vector3(0.55f, 0f, 0f), Vector3.one * size),
            P(PrimitiveType.Cube, trim, new Vector3(-0.55f, size * 0.55f, 0f), new Vector3(size + 0.02f, 0.04f, size + 0.02f)),
            P(PrimitiveType.Cube, trim, new Vector3(0.55f, size * 0.55f, 0f), new Vector3(size + 0.02f, 0.04f, size + 0.02f)),
        };

        private static Part[] Boots(string main, string trim, float height) => new[]
        {
            P(PrimitiveType.Cube, main, new Vector3(-0.2f, 0f, 0.06f), new Vector3(0.27f, height, 0.38f)),
            P(PrimitiveType.Cube, main, new Vector3(0.2f, 0f, 0.06f), new Vector3(0.27f, height, 0.38f)),
            P(PrimitiveType.Cube, trim, new Vector3(-0.2f, height * 0.6f, 0f), new Vector3(0.28f, 0.06f, 0.28f)),
            P(PrimitiveType.Cube, trim, new Vector3(0.2f, height * 0.6f, 0f), new Vector3(0.28f, 0.06f, 0.28f)),
        };

        // --- Weapon shapes by kind (the origin is the right hand) ------------------------------

        private static Part[] Sword(string blade, string grip, string guard, float length) => new[]
        {
            P(PrimitiveType.Cube, grip, Vector3.zero, new Vector3(0.06f, 0.22f, 0.06f)),
            P(PrimitiveType.Cube, guard, new Vector3(0f, 0.13f, 0f), new Vector3(0.3f, 0.05f, 0.08f)),
            P(PrimitiveType.Cube, blade, new Vector3(0f, 0.15f + length / 2f, 0f), new Vector3(0.09f, length, 0.03f)),
        };

        private static Part[] Dagger(string blade, string grip) => new[]
        {
            P(PrimitiveType.Cube, grip, Vector3.zero, new Vector3(0.05f, 0.16f, 0.05f)),
            P(PrimitiveType.Cube, blade, new Vector3(0f, 0.27f, 0f), new Vector3(0.06f, 0.36f, 0.02f)),
            P(PrimitiveType.Cube, blade, new Vector3(-1.2f, 0.27f, 0f), new Vector3(0.06f, 0.36f, 0.02f)), // the left hand's
            P(PrimitiveType.Cube, grip, new Vector3(-1.2f, 0f, 0f), new Vector3(0.05f, 0.16f, 0.05f)),
        };

        private static Part[] Bow(string limb, string grip) => new[]
        {
            P(PrimitiveType.Cube, grip, Vector3.zero, new Vector3(0.07f, 0.2f, 0.07f)),
            P(PrimitiveType.Cube, limb, new Vector3(0f, 0.42f, -0.07f), new Vector3(0.05f, 0.68f, 0.05f), new Vector3(-20f, 0f, 0f)),
            P(PrimitiveType.Cube, limb, new Vector3(0f, -0.42f, -0.07f), new Vector3(0.05f, 0.68f, 0.05f), new Vector3(20f, 0f, 0f)),
            P(PrimitiveType.Cube, "String", new Vector3(0f, 0f, -0.19f), new Vector3(0.012f, 1.45f, 0.012f)),
        };

        private static Part[] Staff(string shaft, string orb, float orbSize) => new[]
        {
            P(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0.3f, 0f), new Vector3(0.065f, 0.82f, 0.065f)),
            P(PrimitiveType.Sphere, orb, new Vector3(0f, 1.15f + orbSize / 4f, 0f), Vector3.one * orbSize),
        };

        private static Part[] Knuckles(string main, string trim) => new[]
        {
            P(PrimitiveType.Cube, main, Vector3.zero, new Vector3(0.2f, 0.12f, 0.24f)),
            P(PrimitiveType.Cube, trim, new Vector3(0f, 0f, 0.13f), new Vector3(0.22f, 0.06f, 0.04f)),
            P(PrimitiveType.Cube, main, new Vector3(-1.24f, 0f, 0f), new Vector3(0.2f, 0.12f, 0.24f)),
            P(PrimitiveType.Cube, trim, new Vector3(-1.24f, 0f, 0.13f), new Vector3(0.22f, 0.06f, 0.04f)),
        };

        private static Part[] Sceptre(string shaft, string head) => new[]
        {
            P(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f)),
            P(PrimitiveType.Cylinder, head, new Vector3(0f, 0.65f, 0f), new Vector3(0.22f, 0.05f, 0.22f)),
            P(PrimitiveType.Sphere, head, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.16f),
        };

        private static Part[] Rod(string shaft, string head) => new[]
        {
            P(PrimitiveType.Cylinder, shaft, new Vector3(0f, 0.25f, 0f), new Vector3(0.055f, 0.5f, 0.055f)),
            P(PrimitiveType.Sphere, head, new Vector3(0f, 0.8f, 0f), new Vector3(0.18f, 0.2f, 0.18f)),
            P(PrimitiveType.Cube, shaft, new Vector3(0f, 0.92f, 0f), new Vector3(0.24f, 0.04f, 0.04f), new Vector3(0f, 0f, 20f)),
        };

        private static readonly Dictionary<string, Part[]> MoreGearRecipes = new Dictionary<string, Part[]>
        {
            // Heavy plate: steel, with gold trim on the Rare and Epic pieces.
            ["ironwall-helm"] = Helm("Steel", "Iron"),
            ["ironwall-greaves"] = Boots("Steel", "Iron", 0.24f),
            ["ironwall-gauntlets"] = Gloves("Steel", "Gold", 0.24f),
            ["ironwall-cuirass"] = Plate("Steel", "Gold"),

            // Medium: moss and leather.
            ["wayfarer-hat"] = Hat("Leather", "Moss"),
            ["wayfarer-boots"] = Boots("Leather", "Moss", 0.22f),
            ["wayfarer-gloves"] = Gloves("Leather", "Bronze", 0.21f),
            ["wayfarer-coat"] = Coat("Moss", "Bronze"),

            // Light leathers: night and violet.
            ["nightweave-hood"] = Hood("Night", "Violet"),
            ["nightweave-slippers"] = Boots("Night", "Violet", 0.14f),
            ["nightweave-grips"] = Gloves("Night", "Violet", 0.19f),
            ["nightweave-vest"] = Vest("Night", "Violet"),

            // Robes: star blue and silver.
            ["starweave-circlet"] = Circlet("Silver", "Arcane"),
            ["starweave-sandals"] = Boots("Star", "Silver", 0.12f),
            ["starweave-wraps"] = Gloves("Star", "Silver", 0.19f),
            ["starweave-robe"] = Robe("Star", "Silver"),

            // Weapons: a Rare and a Legendary per class.
            ["knights-longsword"] = Sword("Steel", "Leather", "Iron", 0.95f),
            ["worldsplitter"] = Sword("Gold", "Charcoal", "Ember", 1.2f),
            ["twin-daggers"] = Dagger("Steel", "Leather"),
            ["whisperfang"] = Dagger("Violet", "Night"),
            ["yew-longbow"] = Bow("Wood", "Leather"),
            ["galeheart"] = Bow("Sky", "Gold"),
            ["oak-staff"] = Staff("Wood", "Arcane", 0.18f),
            ["starfall-staff"] = Staff("Star", "Ember", 0.26f),
            ["iron-knuckles"] = Knuckles("Iron", "Steel"),
            ["dragonscale-wraps"] = Knuckles("Jade", "Gold"),
            ["silver-sceptre"] = Sceptre("Silver", "Steel"),
            ["dawnbringer"] = Sceptre("Gold", "Gold"),
            ["bone-rod"] = Rod("Bone", "Bone"),
            ["voidcaller"] = Rod("Charcoal", "Void"),
            ["spirit-staff"] = Staff("Wood", "Spirit", 0.18f),
            ["soulbinder"] = Staff("Charcoal", "Spirit", 0.26f),
        };
    }
}
