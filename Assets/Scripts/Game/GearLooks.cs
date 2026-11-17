using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// How worn gear is drawn on a character (ticket 34). Each piece's look is a prefab on its
    /// <see cref="GearDefinitionAsset"/>, placed at its slot's anchor on the body: the right hand for a
    /// weapon, the top of the head, the chest, both hands, both feet, and the chest for a trinket. A
    /// look is a child of the body, so it follows the body's build, dodge squash and fall. A piece with
    /// no look, or a scene with no content list, gets a plain stand-in shape so every worn piece shows.
    /// The simulation only knows what is worn; this draws it.
    /// </summary>
    public static class GearLooks
    {
        /// <summary>
        /// Where each slot's look sits in the body's own space. The greybox body is Unity's capsule:
        /// 2 m tall and 1 m wide, centred, so y = 1 is the crown and y = -1 the soles.
        /// </summary>
        public static Vector3 Anchor(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Weapon: return new Vector3(0.62f, -0.05f, 0.15f); // right hand
                case GearSlot.Head: return new Vector3(0f, 0.72f, 0f);
                case GearSlot.Body: return new Vector3(0f, 0.05f, 0f);
                case GearSlot.Hands: return new Vector3(0f, -0.1f, 0.05f); // between the hands; a look holds both
                case GearSlot.Feet: return new Vector3(0f, -0.92f, 0.05f); // between the feet; a look holds both
                default: return new Vector3(0f, 0.3f, 0f); // trinket: the chest
            }
        }

        /// <summary>The name a worn look is given under the body, so it can be found by piece.</summary>
        public static string ObjectName(GearDefinition gear) => $"Gear {gear.Slot}: {gear.Id}";

        /// <summary>Each piece's look prefab by gear id, from the game's content list (null content = none).</summary>
        public static Dictionary<string, GameObject> Catalog(GameContentAsset content)
        {
            var looks = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
            if (content == null) return looks;
            foreach (var asset in content.Gear)
                if (asset != null && asset.Look != null && !string.IsNullOrEmpty(asset.Id))
                    looks[asset.Id] = asset.Look;
            return looks;
        }

        /// <summary>
        /// Puts <paramref name="gear"/>'s look on <paramref name="body"/> at its slot's anchor: its prefab
        /// if it has one, else the stand-in. Colliders are removed; gear is drawn, never touched.
        /// </summary>
        public static GameObject Wear(GearDefinition gear, GameObject prefab, Renderer body)
        {
            GameObject look;
            if (prefab != null)
            {
                look = Object.Instantiate(prefab, body.transform, false);
            }
            else
            {
                look = new GameObject();
                look.transform.SetParent(body.transform, false);
                StandIn(gear, look, body.sharedMaterial);
            }
            look.name = ObjectName(gear);
            look.transform.localPosition = Anchor(gear.Slot);
            foreach (var collider in look.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            if (gear.Rarity == Rarity.Legendary) LegendaryGlow.AddTo(look); // ticket 61: a Legendary's special look
            return look;
        }

        /// <summary>A plain shape for a piece with no look yet, in a colour from its id so two pieces differ.</summary>
        private static void StandIn(GearDefinition gear, GameObject parent, Material material)
        {
            var colour = Color.HSVToRGB(Hash(gear.Id) % 360 / 360f, 0.55f, 0.8f);
            void Part(PrimitiveType shape, Vector3 position, Vector3 scale)
            {
                var r = GreyboxVisuals.CreateVisual(parent, shape, "Stand-in", position, scale, material);
                var props = new MaterialPropertyBlock();
                props.SetColor(Shader.PropertyToID("_BaseColor"), colour);
                r.SetPropertyBlock(props);
            }

            switch (gear.Slot)
            {
                case GearSlot.Weapon: Part(PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(0.08f, 0.9f, 0.08f)); break;
                case GearSlot.Head: Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(1.07f, 0.64f, 1.07f)); break; // wider than the head, or it hides inside
                case GearSlot.Body: Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.08f, 0.35f, 1.08f)); break;
                case GearSlot.Hands:
                    Part(PrimitiveType.Cube, new Vector3(-0.55f, 0f, 0f), Vector3.one * 0.2f);
                    Part(PrimitiveType.Cube, new Vector3(0.55f, 0f, 0f), Vector3.one * 0.2f);
                    break;
                case GearSlot.Feet:
                    Part(PrimitiveType.Cube, new Vector3(-0.2f, 0f, 0.05f), new Vector3(0.25f, 0.16f, 0.35f));
                    Part(PrimitiveType.Cube, new Vector3(0.2f, 0f, 0.05f), new Vector3(0.25f, 0.16f, 0.35f));
                    break;
                default: Part(PrimitiveType.Sphere, new Vector3(0f, 0f, 0.5f), Vector3.one * 0.18f); break;
            }
        }

        private static int Hash(string id)
        {
            unchecked
            {
                var h = 17;
                foreach (var c in id ?? "") h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }
    }
}
