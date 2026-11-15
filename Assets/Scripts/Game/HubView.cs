using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Draws the hub in greybox (ticket 19): its floor, its walls, and the gate board, a tall slab
    /// at the far end. Built once from the hub's layout; shown while the party is in the hub.
    /// </summary>
    public sealed class HubView : MonoBehaviour
    {
        private const float WallHeight = 3f;
        private const float WallThickness = 0.3f;

        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material boardMaterial;
        [SerializeField] private Material shopMaterial;

        private SimulationRunner _runner;
        private GameObject _root;

        /// <summary>True while the hub is drawn (for tests).</summary>
        public bool IsShown => _root != null && _root.activeSelf;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            var world = _runner != null ? _runner.World : null;
            var hub = world?.Hub;
            if (hub == null) return;
            if (_root == null) Build(hub);
            _root.SetActive(world.InHub);
        }

        private void Build(HubDefinition hub)
        {
            _root = new GameObject("Hub");
            var floor = hub.Floor;
            var c = hub.Centre;
            Box("Hub floor", new Vector3(c.X, -0.03f, c.Z), new Vector3(hub.Width, 0.1f, hub.Length), floorMaterial);
            Box("Hub west wall", new Vector3(floor.MinX, WallHeight / 2f, c.Z), new Vector3(WallThickness, WallHeight, hub.Length), wallMaterial);
            Box("Hub east wall", new Vector3(floor.MaxX, WallHeight / 2f, c.Z), new Vector3(WallThickness, WallHeight, hub.Length), wallMaterial);
            Box("Hub near wall", new Vector3(c.X, WallHeight / 2f, floor.MinZ), new Vector3(hub.Width, WallHeight, WallThickness), wallMaterial);
            Box("Hub far wall", new Vector3(c.X, WallHeight / 2f, floor.MaxZ), new Vector3(hub.Width, WallHeight, WallThickness), wallMaterial);
            var board = hub.Board;
            Box("Gate board", new Vector3(board.X, 1.4f, board.Z + 0.6f), new Vector3(3f, 2.8f, 0.3f), boardMaterial);
            // The stores that stand in the hub (the guild's); stores elsewhere are drawn where they stand.
            foreach (var shop in _runner.World.Shops)
                if (floor.Contains(shop.Position)) Box(shop.Name, new Vector3(shop.Position.X, 0.55f, shop.Position.Z), new Vector3(2.4f, 1.1f, 1f), shopMaterial);
        }

        private void Box(string boxName, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            Destroy(box.GetComponent<Collider>()); // the simulation owns walls; these are only to look at
            box.transform.SetParent(_root.transform, worldPositionStays: false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
