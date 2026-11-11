using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Reads the local player's movement from the project-wide "Player/Move" action
    /// (WASD, arrow keys or left stick). Movement is world-relative for now:
    /// up/W is +Z. Camera-relative movement arrives with the camera ticket.
    /// </summary>
    public sealed class LocalInputCommandSource : IPlayerCommandSource
    {
        private readonly InputAction _move;

        public LocalInputCommandSource()
        {
            _move = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Move") : null;
            if (_move == null) Debug.LogWarning("No project-wide 'Player/Move' action found; local player will not move.");
            else _move.Enable();
        }

        public PlayerCommand Sample()
        {
            if (_move == null) return PlayerCommand.None;
            var v = _move.ReadValue<Vector2>();
            return new PlayerCommand(v.x, v.y);
        }
    }
}
