using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Damage numbers over enemies (ticket 47): every hit that lands shows how much it took, rising
    /// and fading, so you can tell a swing connected before there are animations. Your own hits are
    /// big and yellow; other players' and teammates' are small and white. Shown, never simulated.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        public const float LifeSeconds = 0.8f;
        private const float StartHeight = 2f;
        private const float RiseMetres = 1.2f;
        private const float HiddenAfterMetres = 45f;

        /// <summary>One number in the air.</summary>
        public readonly struct Popup
        {
            public readonly EnemyId Enemy;
            public readonly int Amount;
            public readonly bool Mine;
            public readonly float BornAt;
            public readonly Vector3 Position; // where the enemy stood when hit; the number stays there

            public Popup(EnemyId enemy, int amount, bool mine, float bornAt, Vector3 position)
            {
                Enemy = enemy; Amount = amount; Mine = mine; BornAt = bornAt; Position = position;
            }
        }

        private SimulationRunner _runner;
        private readonly List<Popup> _popups = new List<Popup>();
        private GUIStyle _mine, _others, _shadowMine, _shadowOthers;

        /// <summary>The numbers in the air now (for tests).</summary>
        public IReadOnlyList<Popup> Popups => _popups;

        private void Start()
        {
            _runner = GetComponent<SimulationRunner>();
            if (_runner == null) _runner = FindAnyObjectByType<SimulationRunner>();
        }

        private void LateUpdate()
        {
            _popups.RemoveAll(p => Time.time - p.BornAt > LifeSeconds);
            if (_runner == null || _runner.World == null) return;
            var me = _runner.MyPlayerId;
            foreach (var e in _runner.FrameEvents)
            {
                if (e is not DamageDealt hit || hit.Amount <= 0 || !_runner.World.HasEnemy(hit.Target)) continue;
                // Small random spread so several numbers on one enemy do not sit on top of each other.
                var jitter = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0f, 0.3f), Random.Range(-0.35f, 0.35f));
                _popups.Add(new Popup(hit.Target, hit.Amount, me.HasValue && hit.Attacker == me.Value, Time.time,
                    _runner.GetRenderPosition(hit.Target) + jitter));
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _popups.Count == 0) return;
            var camera = Camera.main;
            if (camera == null) return;
            EnsureStyles();

            var previous = GUI.color;
            foreach (var p in _popups)
            {
                var t = Mathf.Clamp01((Time.time - p.BornAt) / LifeSeconds);
                var at = p.Position + Vector3.up * (StartHeight + RiseMetres * (1f - (1f - t) * (1f - t))); // rises fast, then slows
                var screen = camera.WorldToScreenPoint(at);
                if (screen.z <= 0f || screen.z > HiddenAfterMetres) continue;

                var text = p.Amount.ToString();
                var style = p.Mine ? _mine : _others;
                var shadow = p.Mine ? _shadowMine : _shadowOthers;
                var pop = p.Mine ? 1f + 0.5f * Mathf.Clamp01(1f - t * 6f) : 1f; // your own numbers pop in big, then settle
                var size = style.CalcSize(new GUIContent(text)) * pop;
                var rect = new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y / 2f, size.x, size.y);
                var alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;

                var matrix = GUI.matrix;
                if (pop > 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), rect.center);
                var unscaled = new Rect(rect.center.x - size.x / pop / 2f, rect.center.y - size.y / pop / 2f, size.x / pop, size.y / pop);
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.85f);
                GUI.Label(new Rect(unscaled.x + 2, unscaled.y + 2, unscaled.width, unscaled.height), text, shadow);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(unscaled, text, style);
                GUI.matrix = matrix;
            }
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_mine != null) return;
            _mine = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _mine.normal.textColor = new Color(1f, 0.85f, 0.2f);
            _others = new GUIStyle(_mine) { fontSize = 17 };
            _others.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            _shadowMine = new GUIStyle(_mine);
            _shadowMine.normal.textColor = Color.black;
            _shadowOthers = new GUIStyle(_others);
            _shadowOthers.normal.textColor = Color.black;
        }
    }
}
