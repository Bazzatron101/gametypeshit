using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Placeholder on-screen readout of the local player's health and state, until the System
    /// interface UI arrives (ticket 06). Reads from the simulation only.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField, Min(1)] private int playerId = 1;

        private SimulationRunner _runner;
        private GUIStyle _style;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void OnGUI()
        {
            if (_runner == null || _runner.World == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };

            var state = _runner.World.GetPlayer(new PlayerId(playerId));
            var text = state.IsDefeated
                ? "DEFEATED"
                : $"HP {state.Health} / {state.MaxHealth}{(state.IsInvulnerable ? "   (dodging)" : "")}";
            _style.normal.textColor = state.IsDefeated ? new Color(1f, 0.3f, 0.3f) : Color.white;
            GUI.Label(new Rect(16, 12, 600, 40), text, _style);
        }
    }
}
