using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Reads the local player's input from the project-wide actions and turns it into a
    /// world-space command:
    /// "Player/Move" (WASD, arrow keys or left stick, relative to the camera),
    /// "Player/Attack" (left mouse or gamepad west), "Player/HeavyAttack" (right mouse or
    /// right trigger), "Player/Dodge" (space or gamepad east) and "Player/Skill1" to "Skill6"
    /// (keys 1 to 6). All input is ignored while the
    /// cursor is free (a menu is open, or after Escape).
    /// </summary>
    public sealed class LocalInputCommandSource : IPlayerCommandSource
    {
        private readonly InputAction _move;
        private readonly InputAction _attack;
        private readonly InputAction _heavyAttack;
        private readonly InputAction _dodge;
        private readonly InputAction[] _skills = new InputAction[SimulationWorld.SkillSlotCount];

        public LocalInputCommandSource()
        {
            _move = Find("Player/Move");
            _attack = Find("Player/Attack");
            _heavyAttack = Find("Player/HeavyAttack");
            _dodge = Find("Player/Dodge");
            for (var i = 0; i < _skills.Length; i++) _skills[i] = Find($"Player/Skill{i + 1}");
        }

        public PlayerCommand Sample()
        {
            // Input counts only while the game has the mouse: not in a menu, not after Escape.
            // Input is sampled before the camera re-locks the cursor, so the click that captures
            // the mouse does not also swing.
            if (Cursor.lockState != CursorLockMode.Locked) return PlayerCommand.None;

            var stick = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            var camera = Camera.main;
            var move = camera != null ? CameraMath.CameraRelativeMove(stick, camera.transform.eulerAngles.y) : stick;
            int? skillSlot = null;
            var held = 0;
            for (var i = 0; i < _skills.Length; i++)
            {
                if (skillSlot == null && Pressed(_skills[i])) skillSlot = i;
                if (_skills[i] != null && _skills[i].IsPressed()) held |= 1 << i;
            }
            return new PlayerCommand(move.x, move.y,
                lightAttack: Pressed(_attack), dodge: Pressed(_dodge), heavyAttack: Pressed(_heavyAttack),
                useSkillSlot: skillSlot, heldSkillSlots: held);
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
