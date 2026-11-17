using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// A raid's marks on the floor (ticket 29). Read from the world's state, like the boss's telegraphs, so the
    /// host, every guest and a late joiner see the same marks at the same moment. These are never culled (ADR 0023):
    /// <list type="bullet">
    /// <item>while the boss is shielded, a tall beam over every standing crystal, so each group can find its own
    /// from anywhere in the hall;</item>
    /// <item>while a blast is coming, the boss's whole room glows red, brighter as it nears, and the shelter is a
    /// green disc on top: the one place to be.</item>
    /// </list>
    /// </summary>
    public sealed class RaidView : MonoBehaviour
    {
        private const float BeamHeight = 14f;
        private const float BeamWidth = 0.35f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color DangerFrom = new Color(0.35f, 0.08f, 0.06f);
        private static readonly Color DangerTo = new Color(0.95f, 0.15f, 0.08f);
        private static readonly Color ShelterColor = new Color(0.35f, 1f, 0.55f);

        [SerializeField, Tooltip("Any plain lit material; each mark is tinted on top of it")] private Material markMaterial;

        private SimulationRunner _runner;
        private GameObject _danger, _shelter;
        private readonly List<GameObject> _beams = new List<GameObject>();
        private MaterialPropertyBlock _props;

        /// <summary>For tests: whether the shelter is on the floor now, and how many crystal beams stand.</summary>
        public bool ShelterShown => _shelter != null && _shelter.activeSelf;
        public int BeamsShown
        {
            get
            {
                var count = 0;
                foreach (var beam in _beams) if (beam.activeSelf) count++;
                return count;
            }
        }

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            var world = _runner != null ? _runner.World : null;
            var raid = world != null && world.HasGate ? world.Gate.Raid : null;
            ShowBeams(world, raid);
            ShowShelter(world, raid);
        }

        private void ShowBeams(SimulationWorld world, RaidState raid)
        {
            var shown = 0;
            if (raid != null && raid.Phase == RaidPhase.Shielded)
            {
                foreach (var id in raid.Crystals)
                {
                    if (!world.HasEnemy(id)) continue;
                    var crystal = world.GetEnemy(id);
                    if (crystal.IsDefeated) continue;
                    if (shown == _beams.Count) _beams.Add(Mark(PrimitiveType.Cylinder, "Crystal Beam", OnlineViews.CrystalColor));
                    var beam = _beams[shown++];
                    beam.SetActive(true);
                    beam.transform.position = new Vector3(crystal.Position.X, BeamHeight / 2f, crystal.Position.Z);
                    beam.transform.localScale = new Vector3(BeamWidth, BeamHeight / 2f, BeamWidth);
                }
            }
            for (var i = shown; i < _beams.Count; i++) _beams[i].SetActive(false);
        }

        private void ShowShelter(SimulationWorld world, RaidState raid)
        {
            var shelter = raid?.Shelter;
            if (!shelter.HasValue)
            {
                if (_danger != null) _danger.SetActive(false);
                if (_shelter != null) _shelter.SetActive(false);
                return;
            }

            if (_danger == null) _danger = Mark(PrimitiveType.Cube, "Blast Danger", DangerFrom);
            if (_shelter == null) _shelter = Mark(PrimitiveType.Cylinder, "Shelter", ShelterColor);
            _danger.SetActive(true);
            _shelter.SetActive(true);

            var room = world.Gate.Layout.Rooms[raid.Room];
            _danger.transform.position = new Vector3((room.MinX + room.MaxX) / 2f, 0.03f, (room.MinZ + room.MaxZ) / 2f);
            _danger.transform.localScale = new Vector3(room.MaxX - room.MinX, 0.02f, room.MaxZ - room.MinZ);
            var windup = raid.Definition.ShelterWindupSeconds;
            var left = raid.SecondsUntilBlast ?? 0f;
            Tint(_danger, Color.Lerp(DangerFrom, DangerTo, windup > 0f ? Mathf.Clamp01(1f - left / windup) : 1f));

            var radius = raid.Definition.ShelterRadius;
            _shelter.transform.position = new Vector3(shelter.Value.X, 0.06f, shelter.Value.Z);
            _shelter.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        }

        private GameObject Mark(PrimitiveType shape, string markName, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = markName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, worldPositionStays: true);
            if (markMaterial != null) go.GetComponent<Renderer>().sharedMaterial = markMaterial;
            Tint(go, color);
            return go;
        }

        private void Tint(GameObject go, Color color)
        {
            _props ??= new MaterialPropertyBlock();
            _props.SetColor(BaseColorId, color);
            go.GetComponent<Renderer>().SetPropertyBlock(_props);
        }
    }
}
