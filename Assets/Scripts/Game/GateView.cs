using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Draws the running gate in greybox: a floor for each room, walls around the outside (with
    /// the ways between rooms left open), and a pillar at each checkpoint that lights up once the
    /// party has reached it. Built from the gate's layout whenever a gate starts, so any chain of
    /// kit pieces draws without a hand-made scene.
    /// </summary>
    public sealed class GateView : MonoBehaviour
    {
        private const float WallHeight = 3f;
        private const float WallThickness = 0.3f;
        private const float FloorLift = 0.02f; // just above the scene's own floor, so the rooms read

        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material checkpointMaterial;
        [SerializeField] private Material checkpointActiveMaterial;

        private SimulationRunner _runner;
        private GateState _builtFor;
        private Transform _root;
        private readonly List<Renderer> _checkpoints = new List<Renderer>();

        /// <summary>How many checkpoint pillars are drawn (for tests).</summary>
        public int CheckpointCount => _checkpoints.Count;

        /// <summary>True while a gate's rooms are drawn.</summary>
        public bool IsBuilt => _root != null;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            var gate = _runner != null && _runner.World != null ? _runner.World.Gate : null;
            if (gate != _builtFor && (gate == null || _builtFor == null || gate.Definition.Id != _builtFor.Definition.Id))
            {
                Clear();
                if (gate != null) Build(gate);
            }
            _builtFor = gate;
            if (gate == null) return;
            for (var c = 0; c < _checkpoints.Count; c++)
                _checkpoints[c].sharedMaterial = gate.IsCheckpointActivated(c) ? checkpointActiveMaterial : checkpointMaterial;
        }

        private void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _checkpoints.Clear();
        }

        private void Build(GateState gate)
        {
            _root = new GameObject($"Gate ({gate.Definition.Name})").transform;
            var rooms = gate.Layout.Rooms;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                var centre = room.Centre;
                Box($"Room {i} floor", new Vector3(centre.X, FloorLift - 0.05f, centre.Z), new Vector3(room.MaxX - room.MinX, 0.1f, room.MaxZ - room.MinZ), floorMaterial);

                // Side walls run the room's whole length.
                Wall($"Room {i} west wall", room.MinX, room.MinZ, room.MinX, room.MaxZ);
                Wall($"Room {i} east wall", room.MaxX, room.MinZ, room.MaxX, room.MaxZ);

                // End walls leave open whatever the neighbouring room's width overlaps: that is the way through.
                EndWall($"Room {i} near wall", room, room.MinZ, i > 0 ? rooms[i - 1] : (RoomRect?)null);
                EndWall($"Room {i} far wall", room, room.MaxZ, i < rooms.Count - 1 ? rooms[i + 1] : (RoomRect?)null);
            }

            foreach (var checkpoint in gate.Layout.Checkpoints)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Checkpoint {_checkpoints.Count}";
                Destroy(pillar.GetComponent<Collider>());
                pillar.transform.SetParent(_root, worldPositionStays: false);
                pillar.transform.position = new Vector3(checkpoint.Position.X, 1.25f, checkpoint.Position.Z);
                pillar.transform.localScale = new Vector3(0.5f, 1.25f, 0.5f);
                var renderer = pillar.GetComponent<Renderer>();
                renderer.sharedMaterial = checkpointMaterial;
                _checkpoints.Add(renderer);
            }
        }

        private void EndWall(string wallName, RoomRect room, float z, RoomRect? neighbour)
        {
            if (!neighbour.HasValue)
            {
                Wall(wallName, room.MinX, z, room.MaxX, z);
                return;
            }
            var openFrom = Mathf.Max(room.MinX, neighbour.Value.MinX);
            var openTo = Mathf.Min(room.MaxX, neighbour.Value.MaxX);
            if (openFrom > room.MinX) Wall(wallName + " (west part)", room.MinX, z, openFrom, z);
            if (openTo < room.MaxX) Wall(wallName + " (east part)", openTo, z, room.MaxX, z);
        }

        private void Wall(string wallName, float fromX, float fromZ, float toX, float toZ)
        {
            var size = new Vector3(Mathf.Abs(toX - fromX) + WallThickness, WallHeight, Mathf.Abs(toZ - fromZ) + WallThickness);
            Box(wallName, new Vector3((fromX + toX) / 2f, WallHeight / 2f, (fromZ + toZ) / 2f), size, wallMaterial);
        }

        private void Box(string boxName, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            Destroy(box.GetComponent<Collider>()); // the simulation owns walls; these are only to look at
            box.transform.SetParent(_root, worldPositionStays: false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
