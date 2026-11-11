using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox-only stand-in for gear: F5, F6, F7, F8 (and so on) toggle the listed modifiers on the
    /// local player, so synergies can be tried before items exist. Remove once gear grants modifiers.
    /// </summary>
    public sealed class DebugModifierToggles : MonoBehaviour
    {
        private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };

        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Tooltip("Toggled by F5, F6, F7... in list order")] private ModifierDefinitionAsset[] modifiers = new ModifierDefinitionAsset[0];

        private SimulationRunner _runner;

        public int Count => modifiers.Length;
        public static string KeyName(int index) => index < Keys.Length ? Keys[index].ToString() : "";
        public ModifierDefinitionAsset this[int index] => modifiers[index];

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _runner == null || _runner.World == null) return;
            for (var i = 0; i < modifiers.Length && i < Keys.Length; i++)
                if (keyboard[Keys[i]].wasPressedThisFrame) Toggle(i);
        }

        /// <summary>
        /// Queues a grant if the player lacks the modifier, a revoke otherwise; it applies on the next
        /// tick. Returns whether it will be active.
        /// </summary>
        public bool Toggle(int index)
        {
            var asset = modifiers[index];
            if (asset == null) return false;
            var id = new PlayerId(playerId);
            if (IsActive(index))
            {
                _runner.World.RevokeModifier(id, asset.Id);
                return false;
            }
            _runner.World.GrantModifier(id, asset.ToDefinition());
            return true;
        }

        /// <summary>Whether the modifier at <paramref name="index"/> is in effect on the player now.</summary>
        public bool IsActive(int index)
        {
            var asset = modifiers[index];
            if (asset == null || _runner == null || _runner.World == null) return false;
            foreach (var m in _runner.World.GetPlayer(new PlayerId(playerId)).ActiveModifiers)
                if (m.Id == asset.Id) return true;
            return false;
        }
    }
}
