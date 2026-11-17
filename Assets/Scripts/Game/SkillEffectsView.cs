using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox visuals for skills: a sphere per projectile in flight, and a brief flat flash for
    /// the area or cone a skill covered and for each explosion. Each skill is drawn in its own colour
    /// (<see cref="SkillLook"/>: its class's colour) and its flash opens out and fades over half a
    /// second, so you can see what teammates are doing (ticket 35). Teammates' effects are drawn by
    /// <see cref="EffectCulling"/>'s rules (ticket 29): smaller, dimmed, capped and only nearby, so ten players
    /// stay readable. Reads the simulation only.
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
        private readonly List<(Transform shape, Vector3 fullScale, float startedAt, bool teammate)> _flashes = new List<(Transform, Vector3, float, bool)>();
        private MaterialPropertyBlock _props;

        /// <summary>How many projectile visuals are on screen.</summary>
        public int ActiveProjectileVisuals => _projectiles.Count;

        /// <summary>How many flashes are on screen, and how many of them are teammates' (for tests).</summary>
        public int ActiveFlashes => _flashes.Count;
        public int ActiveTeammateFlashes
        {
            get
            {
                var count = 0;
                foreach (var f in _flashes) if (f.teammate) count++;
                return count;
            }
        }

        private PlayerId? Me => _runner.MyPlayerId;

        /// <summary>Where the local player stands, or null with none (then distance culls nothing).</summary>
        private Vec3? MyPosition => Me.HasValue && _runner.World.HasPlayer(Me.Value) ? _runner.World.GetPlayer(Me.Value).Position : (Vec3?)null;

        private static float DistanceFromMe(Vec3 at, Vec3? me) => me.HasValue ? Mathf.Sqrt((at.X - me.Value.X) * (at.X - me.Value.X) + (at.Z - me.Value.Z) * (at.Z - me.Value.Z)) : 0f;

        /// <summary>The culling rule for a new flash by <paramref name="who"/> at <paramref name="at"/>; true to skip it.</summary>
        private bool CullFlash(PlayerId who, Vec3 at, Vec3? me, out bool teammate)
        {
            teammate = EffectCulling.IsTeammate(who, Me);
            return teammate && !EffectCulling.ShowTeammateFlash(ActiveTeammateFlashes, DistanceFromMe(at, me));
        }

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;

            var me = MyPosition;
            foreach (var e in _runner.FrameEvents)
            {
                if (e is SkillUsed used && _runner.World.HasPlayer(used.Player))
                {
                    if (!CullFlash(used.Player, _runner.World.GetPlayer(used.Player).Position, me, out var teammate))
                        Flash(used.Player, used.Skill, used.Range, used.Radius, teammate);
                }
                else if (e is AttackPerformed swing && swing.Weapon != null && _runner.World.HasPlayer(swing.Player))
                {
                    // a weapon's slash or blast (ticket 46); arrows and bolts are drawn in flight
                    if (!CullFlash(swing.Player, _runner.World.GetPlayer(swing.Player).Position, me, out var teammate))
                        Flash(swing.Player, swing.Weapon, swing.Range, swing.Radius, teammate);
                }
                else if (e is Explosion explosion)
                {
                    if (!CullFlash(explosion.Player, explosion.Position, me, out var teammate))
                        FlashDisc(explosion.Position, explosion.Radius, explosionMaterial != null ? explosionMaterial : areaMaterial, "Explosion Flash", ExplosionColor, teammate);
                }
            }

            SyncProjectiles(me);
            AnimateFlashes();
        }

        private void Flash(PlayerId who, SkillDefinition skill, float range, float radius, bool teammate)
        {
            var player = _runner.World.GetPlayer(who);
            var origin = new Vector3(player.Position.X, 0.05f, player.Position.Z);
            var facing = new Vector3(player.Facing.X, 0f, player.Facing.Z);

            switch (skill.Effect)
            {
                case SkillEffectKind.Area:
                    FlashDisc(player.Position, radius, areaMaterial, "Area Flash", SkillLook.SkillColor(skill), teammate);
                    break;
                case SkillEffectKind.Cone:
                {
                    // A flat slab as wide as the cone's far edge, out to its reach.
                    var halfArc = Mathf.Min(skill.ArcDegrees, 180f) * 0.5f * Mathf.Deg2Rad;
                    if (teammate) range *= EffectCulling.TeammateScale;
                    var width = Mathf.Max(0.5f, 2f * range * Mathf.Sin(halfArc));
                    var slab = Visual(PrimitiveType.Cube, coneMaterial, "Cone Flash");
                    slab.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                    slab.transform.position = origin + facing * (range * 0.5f) + Vector3.up * EffectHeight * 0.5f;
                    slab.transform.localScale = new Vector3(width, 0.05f, range);
                    Tint(slab, Shade(SkillLook.SkillColor(skill), teammate));
                    _flashes.Add((slab.transform, slab.transform.localScale, Time.time, teammate));
                    break;
                }
            }
        }

        private void FlashDisc(Vec3 at, float radius, Material material, string name, Color color, bool teammate)
        {
            if (teammate) radius *= EffectCulling.TeammateScale;
            var disc = Visual(PrimitiveType.Cylinder, material, name);
            disc.transform.position = new Vector3(at.X, 0.05f, at.Z);
            disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Tint(disc, Shade(color, teammate));
            _flashes.Add((disc.transform, disc.transform.localScale, Time.time, teammate));
        }

        private static Color Shade(Color color, bool teammate) => teammate ? EffectCulling.Dim(color) : color;

        /// <summary>A flash opens out quickly to its full size, holds, then thins away.</summary>
        private void AnimateFlashes()
        {
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var (shape, full, startedAt, _) = _flashes[i];
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

        private void SyncProjectiles(Vec3? me)
        {
            _liveProjectileIds.Clear();
            foreach (var p in _runner.World.Projectiles)
            {
                var teammate = EffectCulling.IsTeammate(p.Owner, Me);
                if (teammate && !EffectCulling.ShowTeammateProjectile(DistanceFromMe(p.Position, me))) continue; // far off: not drawn (removed below if it was)
                _liveProjectileIds.Add(p.Id);
                if (!_projectiles.TryGetValue(p.Id, out var visual))
                {
                    visual = Visual(PrimitiveType.Sphere, projectileMaterial, $"Projectile {p.Id} ({p.Skill.Id})");
                    visual.transform.localScale = Vector3.one * Mathf.Max(0.35f, p.Skill.Radius) * (teammate ? EffectCulling.TeammateScale : 1f);
                    Tint(visual, Shade(SkillLook.SkillColor(p.Skill), teammate));
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
