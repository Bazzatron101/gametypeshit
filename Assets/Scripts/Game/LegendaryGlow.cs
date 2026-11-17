using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// The special look of a worn Legendary (ticket 61): a soft gold light that pulses on the piece.
    /// Added by <see cref="GearLooks.Wear"/>; presentation only.
    /// </summary>
    public sealed class LegendaryGlow : MonoBehaviour
    {
        public static readonly Color Gold = new Color(1f, 0.74f, 0.2f);

        private Light _light;
        private float _phase;

        /// <summary>Puts a glow on <paramref name="look"/>.</summary>
        public static LegendaryGlow AddTo(GameObject look)
        {
            var holder = new GameObject("Legendary glow");
            holder.transform.SetParent(look.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Gold;
            light.range = 1.6f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            var glow = holder.AddComponent<LegendaryGlow>();
            glow._light = light;
            glow._phase = Random.value * Mathf.PI * 2f;
            return glow;
        }

        private void Update()
        {
            if (_light != null) _light.intensity = 1.2f + 0.6f * Mathf.Sin(Time.time * 2.5f + _phase);
        }
    }
}
