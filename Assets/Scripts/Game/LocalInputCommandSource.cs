using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Reads the local player's input from the project-wide actions: "Player/Move" (WASD, arrow
    /// keys or left stick), "Player/Attack" (left mouse button or gamepad west button) and
    /// "Player/Dodge" (space or gamepad east button).
    /// Movement is world-relative for now: up/W is +Z. Camera-relative movement arrives with the camera ticket.
    /// </summary>
    public sealed class LocalInputCommandSource : IPlayerCommandSource
    {
        private readonly InputAction _move;
        private readonly InputAction _attack;
        private readonly InputAction _dodge;

        public LocalInputCommandSource()
        {
            _move = Find("Player/Move");
            _attack = Find("Player/Attack");
            _dodge = Find("Player/Dodge");
        }

        public PlayerCommand Sample()
        {
            var move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            return new PlayerCommand(move.x, move.y, Pressed(_attack), Pressed(_dodge));
        }

        private static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();

        private static InputAction Find(string path)
        {
            var action = InputSystem.actions != null ? InputSystem.actions.FindAction(path) : null;
            if (action == null) Debug.LogWarning($"No project-wide '{path}' action found; that input is ignored.");
            else action.Enable();
            return action;
        }
    }
}
