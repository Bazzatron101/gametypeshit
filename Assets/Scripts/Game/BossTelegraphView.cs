using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Draws a boss's telegraph on the floor (ticket 18): the exact area its attack will hit, from
    /// the moment the attack starts until it lands, filling up as the hit approaches. An arc for a
    /// sweep, a disc for a slam, a lane for a charge. Read from the world's state rather than from
    /// events, so the host, a guest and a late joiner all see the same marks at the same time.
    /// </summary>
    public sealed class BossTelegraphView : MonoBehaviour
    {
        private const float Lift = 0.04f;
        private const int ArcSegments = 32;

        [SerializeField] private Material areaMaterial;
        [SerializeField] private Material fillMaterial;

        private SimulationRunner _runner;
        private readonly Dictionary<EnemyId, Telegraph> _shown = new Dictionary<EnemyId, Telegraph>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();

        private sealed class Telegraph
        {
            public GameObject Root;
            public MeshFilter Area, Fill;
            public BossAttackDefinition For;
        }

        /// <summary>How many telegraphs are on the floor now (for tests).</summary>
        public int ShownCount
        {
            get
            {
                var count = 0;
                foreach (var t in _shown.Values) if (t.Root.activeSelf) count++;
                return count;
            }
        }

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            var world = _runner != null ? _runner.World : null;
            foreach (var telegraph in _shown.Values) telegraph.Root.SetActive(false);
            if (world == null) return;

            foreach (var id in world.EnemyIds)
            {
                var enemy = world.GetEnemy(id);
                var attack = enemy.CurrentBossAttack;
                if (attack == null || enemy.IsDefeated) continue; // a running charge stays marked: its lane is still dangerous
                if (!_shown.TryGetValue(id, out var telegraph)) _shown[id] = telegraph = Create(id);
                Show(telegraph, enemy, attack, world);
            }

            _gone.Clear();
            foreach (var id in _shown.Keys) if (!world.HasEnemy(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                Destroy(_shown[id].Root);
                _shown.Remove(id);
            }
        }

        private Telegraph Create(EnemyId id)
        {
            var root = new GameObject($"Telegraph (enemy {id.Value})");
            root.transform.SetParent(transform, worldPositionStays: false);
            return new Telegraph { Root = root, Area = Part(root, "Area", areaMaterial, Lift), Fill = Part(root, "Fill", fillMaterial, Lift * 2f) };
        }

        private static MeshFilter Part(GameObject root, string partName, Material material, float lift)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(root.transform, worldPositionStays: false);
            part.transform.localPosition = new Vector3(0f, lift, 0f);
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part.AddComponent<MeshFilter>();
        }

        private static void Show(Telegraph telegraph, EnemyState enemy, BossAttackDefinition attack, SimulationWorld world)
        {
            if (telegraph.For != attack)
            {
                telegraph.For = attack;
                telegraph.Area.sharedMesh = ShapeFor(attack);
                telegraph.Fill.sharedMesh = telegraph.Area.sharedMesh;
            }

            // A charge's lane is drawn only as far as it will really run (a wall may cut it short).
            var lane = attack.Kind == BossAttackKind.Charge && attack.Range > 0f ? Mathf.Clamp01(enemy.AttackReach / attack.Range) : 1f;
            telegraph.Area.transform.localScale = new Vector3(1f, 1f, lane);

            var root = telegraph.Root.transform;
            root.gameObject.SetActive(true);
            root.position = new Vector3(enemy.AttackOrigin.X, 0f, enemy.AttackOrigin.Z);
            root.rotation = Quaternion.LookRotation(new Vector3(enemy.AttackDirection.X, 0f, enemy.AttackDirection.Z), Vector3.up);

            // The fill grows from the boss outward as the hit approaches: full means it lands now.
            var windup = attack.WindupSeconds * (enemy.IsEnraged ? enemy.Definition.Boss.EnragedTimePercent : 100) / 100f;
            var remaining = Mathf.Max(0f, (enemy.AttackLandsAtTick - world.Tick) * world.TickDelta);
            var filled = enemy.IsCharging ? 1f : windup > 0f ? Mathf.Clamp01(1f - remaining / windup) : 1f;
            telegraph.Fill.transform.localScale = attack.Kind == BossAttackKind.Charge
                ? new Vector3(1f, 1f, Mathf.Max(0.01f, filled * lane))
                : new Vector3(Mathf.Max(0.01f, filled), 1f, Mathf.Max(0.01f, filled));
        }

        // Keyed by shape, not by definition object: a guest restoring snapshots gets new definition objects every time.
        private static readonly Dictionary<(BossAttackKind, float, float, float), Mesh> Shapes = new Dictionary<(BossAttackKind, float, float, float), Mesh>();

        /// <summary>The floor area <see cref="BossAttackDefinition.Covers"/> describes, as a flat mesh facing +Z from its origin.</summary>
        private static Mesh ShapeFor(BossAttackDefinition attack)
        {
            var key = (attack.Kind, attack.Range, attack.ArcDegrees, attack.Radius);
            if (Shapes.TryGetValue(key, out var mesh)) return mesh;
            mesh = new Mesh { name = $"Telegraph {attack.Kind}" };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            switch (attack.Kind)
            {
                case BossAttackKind.Charge:
                {
                    var r = attack.Radius;
                    var length = attack.Range + r;
                    vertices.AddRange(new[] { new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, length), new Vector3(-r, 0f, length) });
                    triangles.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
                    break;
                }
                default:
                {
                    var full = attack.Kind == BossAttackKind.Slam;
                    var radius = full ? attack.Radius : attack.Range;
                    var half = full ? Mathf.PI : attack.ArcDegrees * Mathf.Deg2Rad / 2f;
                    vertices.Add(Vector3.zero);
                    for (var i = 0; i <= ArcSegments; i++)
                    {
                        var angle = -half + 2f * half * i / ArcSegments;
                        vertices.Add(new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
                    }
                    for (var i = 1; i <= ArcSegments; i++) triangles.AddRange(new[] { 0, i, i + 1 });
                    break;
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Shapes[key] = mesh;
            return mesh;
        }
    }
}
