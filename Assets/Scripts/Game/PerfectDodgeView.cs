using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Shows a perfect dodge: a hit that landed while the player was inside a dodge's
    /// invulnerability. A burst ring around whoever did it, so everyone sees it, and for the local
    /// player a "PERFECT DODGE" banner. Presentation only: it changes nothing in the fight. In
    /// co-op it follows the host's word, so a guest sees it once the host has confirmed the dodge.
    /// </summary>
    public sealed class PerfectDodgeView : MonoBehaviour
    {
        private const float RingSeconds = 0.45f;
        private const float RingFromRadius = 0.6f;
        private const float RingToRadius = 2.8f;
        private const float BannerSeconds = 1.1f;

        [SerializeField] private Material ringMaterial;

        private SimulationRunner _runner;
        private readonly List<(Transform ring, float startedAt)> _rings = new List<(Transform, float)>();
        private float _bannerShownAt = float.NegativeInfinity;
        private int _streak;
        private GUIStyle _banner, _sub;

        /// <summary>True while the banner is on screen (for tests).</summary>
        public bool BannerShown => Time.time - _bannerShownAt < BannerSeconds;

        /// <summary>How many perfect dodges the local player has made in a row within the banner's time (shown as "x2", "x3").</summary>
        public int Streak => BannerShown ? _streak : 0;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_runner != null && _runner.World != null)
            {
                foreach (var e in _runner.FrameEvents)
                {
                    if (!(e is AttackDodged dodged) || !_runner.World.HasPlayer(dodged.Player)) continue;
                    Burst(_runner.GetRenderPosition(dodged.Player));
                    if (dodged.Player == LocalPlayer) ShowBanner();
                }
            }

            for (var i = _rings.Count - 1; i >= 0; i--)
            {
                var (ring, startedAt) = _rings[i];
                var t = (Time.time - startedAt) / RingSeconds;
                if (t >= 1f)
                {
                    Destroy(ring.gameObject);
                    _rings.RemoveAt(i);
                    continue;
                }
                var radius = Mathf.Lerp(RingFromRadius, RingToRadius, 1f - (1f - t) * (1f - t)); // quick out, slowing
                ring.localScale = new Vector3(radius * 2f, 0.02f * (1f - t) + 0.005f, radius * 2f);
            }
        }

        /// <summary>The local player: online, the one on this machine; offline, the scene's player 1.</summary>
        private PlayerId LocalPlayer => _runner.LocalPlayerId ?? new PlayerId(1);

        private void Burst(Vector3 at)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Perfect dodge";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, worldPositionStays: false);
            ring.transform.position = new Vector3(at.x, 0.08f, at.z);
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            _rings.Add((ring.transform, Time.time));
        }

        private void ShowBanner()
        {
            _streak = BannerShown ? _streak + 1 : 1;
            _bannerShownAt = Time.time;
        }

        private void OnGUI()
        {
            if (!BannerShown) return;
            EnsureStyles();
            var t = (Time.time - _bannerShownAt) / BannerSeconds;
            var old = GUI.color;
            GUI.color = new Color(0.55f, 0.95f, 1f, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
            var top = Screen.height * 0.28f - 30f * Mathf.Min(1f, t * 6f); // a small lift as it appears
            GUI.Label(new Rect(0, top, Screen.width, 50), "PERFECT DODGE", _banner);
            if (_streak > 1) GUI.Label(new Rect(0, top + 48, Screen.width, 28), "x" + _streak, _sub);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_banner != null) return;
            _banner = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
