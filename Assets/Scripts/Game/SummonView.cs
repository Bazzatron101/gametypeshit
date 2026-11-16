using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Draws summons (ticket 57) as small greybox capsules in their skill's colour, where the simulation has them.
    /// One object per summon, made when it appears and destroyed when it goes. Shown, never simulated.
    /// </summary>
    public sealed class SummonView : MonoBehaviour
    {
        private const float Height = 1.1f;

        private SimulationRunner _runner;
        private readonly Dictionary<int, GameObject> _shown = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _seen = new HashSet<int>();

        /// <summary>How many summons are drawn now (for tests).</summary>
        public int Shown => _shown.Count;

        private void Start()
        {
            _runner = GetComponent<SimulationRunner>();
            if (_runner == null) _runner = FindAnyObjectByType<SimulationRunner>();
        }

        private void LateUpdate()
        {
            var world = _runner != null ? _runner.World : null;
            _seen.Clear();
            if (world != null)
            {
                foreach (var summon in world.Summons)
                {
                    _seen.Add(summon.Id);
                    if (!_shown.TryGetValue(summon.Id, out var body)) _shown[summon.Id] = body = Make(summon);
                    body.transform.position = new Vector3(summon.Position.X, summon.Position.Y + Height / 2f, summon.Position.Z);
                    body.transform.rotation = Quaternion.LookRotation(new Vector3(summon.Facing.X, 0f, summon.Facing.Z).normalized + new Vector3(0f, 0f, 1e-4f));
                }
            }
            var gone = new List<int>();
            foreach (var id in _shown.Keys) if (!_seen.Contains(id)) gone.Add(id);
            foreach (var id in gone)
            {
                Destroy(_shown[id]);
                _shown.Remove(id);
            }
        }

        private GameObject Make(SummonState summon)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"Summon {summon.Id} ({summon.Skill?.Name})";
            Destroy(body.GetComponent<Collider>()); // drawn, never touched
            body.transform.SetParent(transform, worldPositionStays: true);
            body.transform.localScale = new Vector3(0.6f, Height / 2f, 0.6f);
            var renderer = body.GetComponent<Renderer>();
            renderer.material.color = SkillLook.SkillColor(summon.Skill);
            return body;
        }

        private void OnDestroy()
        {
            foreach (var body in _shown.Values) if (body != null) Destroy(body);
            _shown.Clear();
        }
    }
}
