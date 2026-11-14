using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox visuals for skills: a sphere per projectile in flight, and a brief flat flash for
    /// the area or cone a skill covered and for each explosion. Each skill is drawn in its own colour
    /// (<see cref="SkillLook"/>: its class's colour) and its flash opens out and fades over half a
    /// second, so you can see what teammates are doing (ticket 35). Reads the simulation only.
    /// </summary>
    public sealed class SkillEffectsView : MonoBehaviour
    {
        private const float FlashSeconds = 0.5f;
        private const float EffectHeight = 1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color ExplosionColor = new Color(1f, 0.6f, 0.2f);

        [SerializeField] private Material projectileMaterial;
        [SerializeField] private Material areaMaterial;
        [SerializeField] private Material coneMaterial;
        [SerializeField] private Material explosionMaterial;

        private SimulationRunner _runner;
        private readonly Dictionary<int, GameObject> _projectiles = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _liveProjectileIds = new HashSet<int>();
        private readonly List<(Transform shape, Vector3 fullScale, float startedAt)> _flashes = new List<(Transform, Vector3, float)>();
        private MaterialPropertyBlock _props;

        /// <summary>How many projectile visuals are on screen.</summary>
        public int ActiveProjectileVisuals => _projectiles.Count;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
            {
                if (e is SkillUsed used && _runner.World.HasPlayer(used.Player))
                {
                    Flash(used);
                }
                else if (e is Explosion explosion)
                {
                    FlashDisc(explosion.Position, explosion.Radius, explosionMaterial != null ? explosionMaterial : areaMaterial, "Explosion Flash", ExplosionColor);
                }
            }

            SyncProjectiles();
            AnimateFlashes();
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
                    FlashDisc(player.Position, used.Radius, areaMaterial, "Area Flash", SkillLook.SkillColor(skill));
                    break;
                case SkillEffectKind.Cone:
                {
                    // A flat slab as wide as the cone's far edge, out to its reach.
                    var halfArc = Mathf.Min(skill.ArcDegrees, 180f) * 0.5f * Mathf.Deg2Rad;
                    var width = Mathf.Max(0.5f, 2f * used.Range * Mathf.Sin(halfArc));
                    var slab = Visual(PrimitiveType.Cube, coneMaterial, "Cone Flash");
                    slab.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                    slab.transform.position = origin + facing * (used.Range * 0.5f) + Vector3.up * EffectHeight * 0.5f;
                    slab.transform.localScale = new Vector3(width, 0.05f, used.Range);
                    Tint(slab, SkillLook.SkillColor(skill));
                    _flashes.Add((slab.transform, slab.transform.localScale, Time.time));
                    break;
                }
            }
        }

        private void FlashDisc(Vec3 at, float radius, Material material, string name, Color color)
        {
            var disc = Visual(PrimitiveType.Cylinder, material, name);
            disc.transform.position = new Vector3(at.X, 0.05f, at.Z);
            disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Tint(disc, color);
            _flashes.Add((disc.transform, disc.transform.localScale, Time.time));
        }

        /// <summary>A flash opens out quickly to its full size, holds, then thins away.</summary>
        private void AnimateFlashes()
        {
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var (shape, full, startedAt) = _flashes[i];
                var t = (Time.time - startedAt) / FlashSeconds;
                if (shape == null || t >= 1f)
                {
                    if (shape != null) Destroy(shape.gameObject);
                    _flashes.RemoveAt(i);
                    continue;
                }
                var open = 0.6f + 0.4f * Mathf.Min(1f, t * 5f);
                var thin = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                shape.localScale = new Vector3(full.x * open, full.y * Mathf.Max(0.1f, thin), full.z * open);
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
                    visual.transform.localScale = Vector3.one * Mathf.Max(0.35f, p.Skill.Radius);
                    Tint(visual, SkillLook.SkillColor(p.Skill));
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

        private void Tint(GameObject go, Color color)
        {
            _props ??= new MaterialPropertyBlock();
            _props.SetColor(BaseColorId, color);
            go.GetComponent<Renderer>().SetPropertyBlock(_props);
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
