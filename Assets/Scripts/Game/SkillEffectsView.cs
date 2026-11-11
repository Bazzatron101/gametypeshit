using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox visuals for skills: a sphere per projectile in flight, and a brief flat flash for
    /// the area or cone a skill covered. Reads the simulation only.
    /// </summary>
    public sealed class SkillEffectsView : MonoBehaviour
    {
        private const float FlashSeconds = 0.25f;
        private const float EffectHeight = 1f;

        [SerializeField] private Material projectileMaterial;
        [SerializeField] private Material areaMaterial;
        [SerializeField] private Material coneMaterial;

        private SimulationRunner _runner;
        private readonly Dictionary<int, GameObject> _projectiles = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _liveProjectileIds = new HashSet<int>();

        /// <summary>How many projectile visuals are on screen.</summary>
        public int ActiveProjectileVisuals => _projectiles.Count;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
                if (e is SkillUsed used) Flash(used);

            SyncProjectiles();
        }

        private void Flash(SkillUsed used)
        {
            var skill = used.Skill;
            var player = _runner.World.GetPlayer(used.Player);
            var origin = new Vector3(player.Position.X, 0.05f, player.Position.Z);
            var facing = new Vector3(player.Facing.X, 0f, player.Facing.Z);

            switch (skill.Effect)
            {
                case SkillEffectKind.Area:
                {
                    var disc = Visual(PrimitiveType.Cylinder, areaMaterial, "Area Flash");
                    disc.transform.position = origin;
                    disc.transform.localScale = new Vector3(skill.Radius * 2f, 0.02f, skill.Radius * 2f);
                    Destroy(disc, FlashSeconds);
                    break;
                }
                case SkillEffectKind.Cone:
                {
                    // A flat slab as wide as the cone's far edge, out to its reach.
                    var halfArc = Mathf.Min(skill.ArcDegrees, 180f) * 0.5f * Mathf.Deg2Rad;
                    var width = Mathf.Max(0.5f, 2f * skill.Range * Mathf.Sin(halfArc));
                    var slab = Visual(PrimitiveType.Cube, coneMaterial, "Cone Flash");
                    slab.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                    slab.transform.position = origin + facing * (skill.Range * 0.5f) + Vector3.up * EffectHeight * 0.5f;
                    slab.transform.localScale = new Vector3(width, 0.05f, skill.Range);
                    Destroy(slab, FlashSeconds);
                    break;
                }
            }
        }

        private void SyncProjectiles()
        {
            _liveProjectileIds.Clear();
            foreach (var p in _runner.World.Projectiles)
            {
                _liveProjectileIds.Add(p.Id);
                if (!_projectiles.TryGetValue(p.Id, out var visual))
                {
                    visual = Visual(PrimitiveType.Sphere, projectileMaterial, $"Projectile {p.Id} ({p.Skill.Id})");
                    visual.transform.localScale = Vector3.one * Mathf.Max(0.3f, p.Skill.Radius);
                    _projectiles.Add(p.Id, visual);
                }
                visual.transform.position = new Vector3(p.Position.X, p.Position.Y + EffectHeight, p.Position.Z);
            }

            if (_projectiles.Count == _liveProjectileIds.Count) return;
            var gone = new List<int>();
            foreach (var id in _projectiles.Keys) if (!_liveProjectileIds.Contains(id)) gone.Add(id);
            foreach (var id in gone)
            {
                Destroy(_projectiles[id]);
                _projectiles.Remove(id);
            }
        }

        private GameObject Visual(PrimitiveType shape, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, true);
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
