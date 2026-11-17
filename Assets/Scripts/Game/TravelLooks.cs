using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox travel looks (ticket 90): a bicycle made from cylinders and boxes, until there is a model. Made in
    /// code, so there is no art file to label "AI" yet.
    /// </summary>
    public static class TravelLooks
    {
        private static readonly Color Frame = new Color(0.75f, 0.2f, 0.15f);
        private static readonly Color Tyre = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color Metal = new Color(0.7f, 0.7f, 0.72f);

        /// <summary>A bicycle under the hunter, facing their way. Named "Mount", like the mount it stands in for.</summary>
        public static GameObject Bicycle(Transform hunter, Material material)
        {
            var bike = new GameObject("Mount");
            bike.transform.SetParent(hunter, false);
            Part(bike, material, Tyre, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0.65f), new Vector3(0.8f, 0.04f, 0.8f), new Vector3(0f, 0f, 90f), "Front wheel");
            Part(bike, material, Tyre, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, -0.65f), new Vector3(0.8f, 0.04f, 0.8f), new Vector3(0f, 0f, 90f), "Back wheel");
            Part(bike, material, Frame, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(0.08f, 0.08f, 1.1f), Vector3.zero, "Top tube");
            Part(bike, material, Frame, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.1f), new Vector3(0.08f, 0.5f, 0.08f), new Vector3(35f, 0f, 0f), "Down tube");
            Part(bike, material, Frame, PrimitiveType.Cube, new Vector3(0f, 0.6f, -0.3f), new Vector3(0.08f, 0.45f, 0.08f), new Vector3(-15f, 0f, 0f), "Seat tube");
            Part(bike, material, Tyre, PrimitiveType.Cube, new Vector3(0f, 0.85f, -0.35f), new Vector3(0.2f, 0.06f, 0.3f), Vector3.zero, "Seat");
            Part(bike, material, Metal, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0.6f), new Vector3(0.06f, 0.55f, 0.06f), new Vector3(-15f, 0f, 0f), "Fork");
            Part(bike, material, Metal, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0.55f), new Vector3(0.04f, 0.3f, 0.04f), new Vector3(0f, 0f, 90f), "Handlebars");
            return bike;
        }

        private static void Part(GameObject root, Material material, Color colour, PrimitiveType shape, Vector3 position, Vector3 scale, Vector3 euler, string name)
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
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            block.SetColor("_Color", colour);
            renderer.SetPropertyBlock(block);
        }
    }
}
