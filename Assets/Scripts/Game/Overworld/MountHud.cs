using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Says what is happening with the mount (ticket 42), which otherwise gives no sign: when the
    /// character gets one, and, when R does nothing, why (no mount yet, mid-jump or mid-attack, in a gate). Added by <see cref="OverworldScene"/>.
    /// </summary>
    public sealed class MountHud : MonoBehaviour
    {
        private const float ShowSeconds = 4f;

        private SimulationRunner _runner;
        private bool? _hadMount;
        private bool _wasFlying;
        private string _message;
        private float _until;
        private GUIStyle _style;

        private void Update()
        {
            if (_runner == null) _runner = FindAnyObjectByType<SimulationRunner>();
            var world = _runner != null ? _runner.World : null;
            var me = _runner != null ? _runner.LocalPlayerId : null;
            if (world == null || !me.HasValue || !world.HasPlayer(me.Value)) return;
            var state = world.GetPlayer(me.Value);

            if (_hadMount == false && state.HasMount) Show($"You can {Verb(state.Travel)} now. Press R.");
            _hadMount = state.HasMount;

            // Taking off (ticket 90): how high this rank lets them go.
            if (state.IsFlying && !_wasFlying)
                Show(world.FlightCeiling(me.Value) > state.FlightHeight + 0.01f
                    ? $"Flying. Hold Space to climb (up to {world.FlightCeiling(me.Value):0} m at your rank) and C to dive."
                    : "Flying low. Higher ranks let you climb and dive.");
            _wasFlying = state.IsFlying;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.rKey.wasPressedThisFrame) return;
            if (state.IsMounted)
            {
                if (state.IsFlying && !world.CanLand(me.Value)) Show("Fly over ground you can stand on to land.");
                return;
            }
            switch (world.WhyCannotMount(me.Value))
            {
                case MountBlock.NoMount:
                    Show(Debug.isDebugBuild ? $"You cannot {Verb(state.Travel)} yet. Press F2 to allow it (testing), or reach D rank." : $"You cannot {Verb(state.Travel)} yet. It comes with the D-rank promotion.");
                    break;
                case MountBlock.Busy:
                    Show("Land, and finish your attack or dodge, then press R.");
                    break;
                case MountBlock.NotInOverworld:
                    Show($"You can only {Verb(state.Travel)} in the open world, not inside gates.");
                    break;
            }
        }

        /// <summary>The species' word for mounting up (ticket 90).</summary>
        public static string Verb(TravelStyle style) => style == TravelStyle.Fly ? "fly" : style == TravelStyle.Bicycle ? "ride your bicycle" : "run at full pace";

        private void Show(string message)
        {
            _message = message;
            _until = Time.unscaledTime + ShowSeconds;
        }

        private void OnGUI()
        {
            if (_message == null || Time.unscaledTime > _until) return;
            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Box(new Rect(Screen.width / 2f - 300f, Screen.height - 160f, 600f, 48f), _message, _style);
        }
    }
}
