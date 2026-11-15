using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Names above characters (ticket 28): every other player and bot teammate shows their hunter's
    /// name; your own does not, so it does not clutter your view. Names fade with distance and are
    /// hidden behind the camera or far away.
    /// </summary>
    public sealed class Nameplates : MonoBehaviour
    {
        private const float HeightAboveFeet = 2.5f;
        private const float FullUntilMetres = 18f;
        private const float HiddenAfterMetres = 40f;

        private SimulationRunner _runner;
        private GUIStyle _style, _shadow;
        private readonly List<(PlayerId id, string name)> _shown = new List<(PlayerId, string)>();

        /// <summary>The names drawn last frame, by player (for tests).</summary>
        public IReadOnlyList<(PlayerId id, string name)> Shown => _shown;

        private void Start()
        {
            _runner = GetComponent<SimulationRunner>();
            if (_runner == null) _runner = FindAnyObjectByType<SimulationRunner>();
        }

        /// <summary>Whose names are drawn: everyone with a name in the world, except this machine's own hunter.</summary>
        public static IEnumerable<(PlayerId id, string name)> Whose(SimulationWorld world, PlayerId? me)
        {
            if (world == null) yield break;
            foreach (var id in world.PlayerIds)
            {
                if (me.HasValue && id == me.Value) continue;
                var name = world.GetPlayer(id).Name;
                if (!string.IsNullOrEmpty(name)) yield return (id, name);
            }
        }

        private void LateUpdate()
        {
            _shown.Clear();
            if (_runner == null || _runner.World == null) return;
            _shown.AddRange(Whose(_runner.World, _runner.MyPlayerId));
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _shown.Count == 0) return;
            var camera = Camera.main;
            if (camera == null) return;
            EnsureStyles();

            var previous = GUI.color;
            foreach (var (id, name) in _shown)
            {
                if (!_runner.World.HasPlayer(id)) continue;
                var head = _runner.GetRenderPosition(id) + Vector3.up * HeightAboveFeet;
                var screen = camera.WorldToScreenPoint(head);
                if (screen.z <= 0f || screen.z > HiddenAfterMetres) continue; // behind the camera, or too far
                var alpha = Mathf.Clamp01(1f - (screen.z - FullUntilMetres) / (HiddenAfterMetres - FullUntilMetres));
                var state = _runner.World.GetPlayer(id);
                if (state.IsDefeated) alpha *= 0.5f;

                var size = _style.CalcSize(new GUIContent(name));
                var rect = new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y, size.x, size.y);
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), name, _shadow);
                var colour = CharacterLooks.Colour(state.Look);
                GUI.color = new Color(Mathf.Lerp(colour.r, 1f, 0.6f), Mathf.Lerp(colour.g, 1f, 0.6f), Mathf.Lerp(colour.b, 1f, 0.6f), alpha);
                GUI.Label(rect, name, _style);
            }
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _style.normal.textColor = Color.white;
            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = Color.black;
        }
    }
}
