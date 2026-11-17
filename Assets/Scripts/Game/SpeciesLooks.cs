using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox species looks (ticket 81, ADR 0024): one shared body, with ears, a snout or beak and a tail made
    /// from basic shapes, and the body's proportions nudged per species. Made in code, so there is no art file
    /// to label; real heads and tails replace these later. Parts hang off the body, so they follow its pose,
    /// build and colour, and gear (ticket 34) fits every species.
    /// </summary>
    public static class SpeciesLooks
    {
        /// <summary>How the species nudges the body's proportions (width, height, depth), on top of the build.</summary>
        public static Vector3 Proportions(string speciesId)
        {
            switch (speciesId)
            {
                case "kangaroo": return new Vector3(0.95f, 1.08f, 0.95f);
                case "tasmanian-devil": return new Vector3(1.05f, 0.9f, 1.05f);
                case "sugar-glider": return new Vector3(0.88f, 0.92f, 0.88f);
                case "wombat": return new Vector3(1.18f, 0.86f, 1.18f);
                case "koala": return new Vector3(1.08f, 0.94f, 1.08f);
                case "quokka": return new Vector3(0.95f, 0.9f, 0.95f);
                default: return Vector3.one;
            }
        }

        /// <summary>A short line for menus: the stat it is good at and its perk.</summary>
        public static string Summary(SpeciesDefinition species) =>
            species == null ? "" : $"+{species.BonusAtFirstRank} {species.BonusStat}, +{species.BonusPerRank} more each rank. {species.Description}";

        /// <summary>
        /// Builds the species' parts under <paramref name="body"/> (a unit capsule: 2 tall, 1 wide, centred) and
        /// returns their root, or null for no species. Parts with no colour of their own take the body's.
        /// </summary>
        public static GameObject Build(string speciesId, Renderer body)
        {
            if (speciesId == null || body == null) return null;
            var root = new GameObject("Species " + speciesId);
            root.transform.SetParent(body.transform, false);
            var material = body.sharedMaterial;
            var dark = new Color(0.22f, 0.18f, 0.16f);
            var beak = new Color(0.25f, 0.22f, 0.2f);
            var bill = new Color(0.2f, 0.2f, 0.22f);
            var cream = new Color(0.95f, 0.88f, 0.55f);

            switch (speciesId)
            {
                case "kangaroo":
                    Ears(root, material, null, new Vector3(0.12f, 0.32f, 0.08f), 0.2f, 0.95f);
                    Part(root, material, null, PrimitiveType.Capsule, new Vector3(0f, 0.62f, 0.42f), new Vector3(0.22f, 0.14f, 0.22f), new Vector3(90f, 0f, 0f), "Snout");
                    Part(root, material, null, PrimitiveType.Capsule, new Vector3(0f, -0.72f, -0.62f), new Vector3(0.24f, 0.55f, 0.24f), new Vector3(-60f, 0f, 0f), "Tail");
                    break;
                case "tasmanian-devil":
                    Ears(root, material, dark, new Vector3(0.16f, 0.16f, 0.06f), 0.24f, 0.82f);
                    Part(root, material, dark, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.44f), new Vector3(0.3f, 0.18f, 0.2f), Vector3.zero, "Snout");
                    Part(root, material, cream, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.47f), new Vector3(0.5f, 0.08f, 0.05f), Vector3.zero, "Chest stripe");
                    Part(root, material, dark, PrimitiveType.Capsule, new Vector3(0f, -0.65f, -0.5f), new Vector3(0.14f, 0.25f, 0.14f), new Vector3(-55f, 0f, 0f), "Tail");
                    break;
                case "sugar-glider":
                    Ears(root, material, null, new Vector3(0.14f, 0.18f, 0.05f), 0.24f, 0.86f);
                    Part(root, material, dark, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0.38f), new Vector3(0.06f, 0.35f, 0.08f), Vector3.zero, "Stripe");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(0.5f, 0.1f, 0f), new Vector3(0.12f, 0.9f, 0.5f), Vector3.zero, "Membrane");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(-0.5f, 0.1f, 0f), new Vector3(0.12f, 0.9f, 0.5f), Vector3.zero, "Membrane");
                    Part(root, material, dark, PrimitiveType.Capsule, new Vector3(0f, -0.55f, -0.75f), new Vector3(0.16f, 0.5f, 0.16f), new Vector3(-75f, 0f, 0f), "Tail");
                    break;
                case "wombat":
                    Ears(root, material, null, new Vector3(0.12f, 0.12f, 0.05f), 0.22f, 0.86f);
                    Part(root, material, dark, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.46f), new Vector3(0.34f, 0.22f, 0.12f), Vector3.zero, "Nose");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(0f, -0.45f, -0.48f), new Vector3(0.75f, 0.6f, 0.12f), Vector3.zero, "Rump plate");
                    break;
                case "koala":
                    Part(root, material, null, PrimitiveType.Sphere, new Vector3(0.36f, 0.85f, 0f), new Vector3(0.3f, 0.3f, 0.1f), Vector3.zero, "Ear");
                    Part(root, material, null, PrimitiveType.Sphere, new Vector3(-0.36f, 0.85f, 0f), new Vector3(0.3f, 0.3f, 0.1f), Vector3.zero, "Ear");
                    Part(root, material, dark, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0.46f), new Vector3(0.18f, 0.24f, 0.14f), Vector3.zero, "Nose");
                    break;
                case "cockatoo":
                    Part(root, material, cream, PrimitiveType.Cube, new Vector3(0f, 1.05f, -0.05f), new Vector3(0.06f, 0.35f, 0.2f), new Vector3(-20f, 0f, 0f), "Crest");
                    Part(root, material, cream, PrimitiveType.Cube, new Vector3(0f, 1.0f, -0.22f), new Vector3(0.06f, 0.3f, 0.18f), new Vector3(-40f, 0f, 0f), "Crest");
                    Part(root, material, beak, PrimitiveType.Cube, new Vector3(0f, 0.58f, 0.5f), new Vector3(0.16f, 0.16f, 0.2f), new Vector3(45f, 0f, 0f), "Beak");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(0.52f, 0.05f, -0.05f), new Vector3(0.08f, 0.8f, 0.45f), new Vector3(0f, 0f, -8f), "Wing");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(-0.52f, 0.05f, -0.05f), new Vector3(0.08f, 0.8f, 0.45f), new Vector3(0f, 0f, 8f), "Wing");
                    Part(root, material, null, PrimitiveType.Cube, new Vector3(0f, -0.65f, -0.5f), new Vector3(0.35f, 0.08f, 0.4f), new Vector3(-30f, 0f, 0f), "Tail");
                    break;
                case "platypus":
                    Part(root, material, bill, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.6f), new Vector3(0.36f, 0.08f, 0.38f), Vector3.zero, "Bill");
                    Part(root, material, dark, PrimitiveType.Cube, new Vector3(0f, -0.72f, -0.62f), new Vector3(0.5f, 0.08f, 0.55f), new Vector3(-25f, 0f, 0f), "Tail");
                    break;
                case "quokka":
                    Ears(root, material, null, new Vector3(0.12f, 0.12f, 0.06f), 0.22f, 0.86f);
                    Part(root, material, dark, PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0.47f), new Vector3(0.12f, 0.1f, 0.1f), Vector3.zero, "Nose");
                    Part(root, material, cream, PrimitiveType.Cube, new Vector3(0f, 0.48f, 0.49f), new Vector3(0.18f, 0.03f, 0.03f), Vector3.zero, "Smile");
                    Part(root, material, null, PrimitiveType.Capsule, new Vector3(0f, -0.7f, -0.55f), new Vector3(0.14f, 0.3f, 0.14f), new Vector3(-60f, 0f, 0f), "Tail");
                    break;
            }
            return root;
        }

        private static void Ears(GameObject root, Material material, Color? colour, Vector3 size, float apart, float height)
        {
            Part(root, material, colour, PrimitiveType.Cube, new Vector3(apart, height + size.y * 0.5f, 0f), size, new Vector3(0f, 0f, -10f), "Ear");
            Part(root, material, colour, PrimitiveType.Cube, new Vector3(-apart, height + size.y * 0.5f, 0f), size, new Vector3(0f, 0f, 10f), "Ear");
        }

        private static void Part(GameObject root, Material material, Color? colour, PrimitiveType shape, Vector3 position, Vector3 scale, Vector3 euler, string name)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (colour.HasValue)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", colour.Value);
                block.SetColor("_Color", colour.Value);
                renderer.SetPropertyBlock(block);
            }
            else
            {
                part.AddComponent<FollowBodyTint>().Body = root.transform.parent.GetComponent<Renderer>();
            }
        }

        /// <summary>A part with no colour of its own wears the body's: its fur or feathers (ticket 81).</summary>
        private sealed class FollowBodyTint : MonoBehaviour
        {
            public Renderer Body;
            private Renderer _own;
            private MaterialPropertyBlock _block;

            private void LateUpdate()
            {
                if (Body == null) return;
                _own ??= GetComponent<Renderer>();
                _block ??= new MaterialPropertyBlock();
                Body.GetPropertyBlock(_block);
                _own.SetPropertyBlock(_block);
            }
        }
    }
}
