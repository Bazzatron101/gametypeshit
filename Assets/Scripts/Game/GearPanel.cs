using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The gear menu (ticket 23). Press I anywhere: it shows the six slots and everything in the
    /// bag, and puts a piece on or takes it off per click. Changes go to the simulation as commands,
    /// like any input, so the host decides.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private TextScenePlayer _scenes;
        private GUIStyle _title, _label, _small, _button;
        private Vector2 _scroll;

        /// <summary>True while the menu is open (for tests).</summary>
        public bool IsOpen { get; private set; }

        public static string SlotName(GearSlot slot) => slot.ToString();

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_scenes == null) _scenes = FindAnyObjectByType<TextScenePlayer>();
            if (_scenes != null && _scenes.Playing != null) return; // a story scene has the screen and the keys
            if (keyboard.iKey.wasPressedThisFrame) SetOpen(!IsOpen);
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (_camera == null) return;
            if (open) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: true);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            var world = _runner != null ? _runner.World : null;
            var me = _runner != null ? _runner.LocalPlayerId : null;
            if (world == null || !me.HasValue || !world.HasPlayer(me.Value)) return;
            var state = world.GetPlayer(me.Value);
            EnsureStyles();

            var area = new Rect(Screen.width / 2f - 300, Screen.height / 2f - 240, 600, 480);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("GEAR", _title);

            for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
            {
                var worn = state.Worn[slot];
                GUILayout.BeginHorizontal();
                GUILayout.Label(SlotName((GearSlot)slot), _label, GUILayout.Width(80));
                GUILayout.Label(worn != null ? worn.Name : "(empty)", _label, GUILayout.Width(380));
                if (worn != null && GUILayout.Button("Take off", _button, GUILayout.Width(100))) _runner.RequestUnequip(me.Value, (GearSlot)slot);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            var spare = state.Bag.Where(g => state.Worn[(int)g.Slot] != g).ToList();
            if (spare.Count == 0)
            {
                GUILayout.Label(state.Bag.Count == 0
                    ? "Your bag is empty. Gates drop gear when they are cleared."
                    : "You are wearing everything you have.", _label);
            }
            else
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                foreach (var gear in spare)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(460));
                    GUILayout.Label($"{gear.Name}  ({SlotName(gear.Slot)}; {string.Join(", ", gear.Tags)})", _label);
                    if (!string.IsNullOrEmpty(gear.Description)) GUILayout.Label(gear.Description, _small);
                    GUILayout.EndVertical();
                    if (GUILayout.Button("Wear", _button, GUILayout.Width(100))) _runner.RequestEquip(me.Value, gear.Id);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("[I] or [Esc] to close", _label);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
    }
}
