using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Greybox-only stand-in for gear: F5, F6, F7, F8 (and so on) toggle the game's modifiers (from
    /// the runner's content list, in order) on the local player, so synergies can be tried before
    /// items exist. Remove once gear grants modifiers.
    /// </summary>
    public sealed class DebugModifierToggles : MonoBehaviour
    {
        private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12 };

        [SerializeField, Min(1)] private int playerId = 1;
        private SimulationRunner _runner;

        private System.Collections.Generic.IReadOnlyList<ModifierDefinitionAsset> Modifiers =>
            _runner != null && _runner.Content != null ? _runner.Content.Modifiers : System.Array.Empty<ModifierDefinitionAsset>();

        public int Count => Modifiers.Count;
        public static string KeyName(int index) => index < Keys.Length ? Keys[index].ToString() : "";
        public ModifierDefinitionAsset this[int index] => Modifiers[index];

        private void Awake() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _runner == null || _runner.World == null) return;
            for (var i = 0; i < Count && i < Keys.Length; i++)
                if (keyboard[Keys[i]].wasPressedThisFrame) Toggle(i);
        }

        /// <summary>
        /// Queues a grant if the player lacks the modifier, a revoke otherwise; it applies on the next
        /// tick. Returns whether it will be active.
        /// </summary>
        public bool Toggle(int index)
        {
            var asset = Modifiers[index];
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
            var asset = Modifiers[index];
            if (asset == null || _runner == null || _runner.World == null) return false;
            return _runner.World.HasModifier(new PlayerId(playerId), asset.Id);
        }
    }
}
