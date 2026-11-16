using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The pick of a specialty (ticket 26), once the local hunter has passed their class's trial. It opens
    /// by itself when the party is home, and K opens it again after "Decide later". The pick is permanent,
    /// and it goes to the host as a command like any other. A specialty with nothing in it yet (a stub) is
    /// shown but cannot be picked.
    /// </summary>
    public sealed class SpecialtyPicker : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private TextScenePlayer _scenes;
        private bool _holdingCursor;
        private bool _later;
        private string _sent;
        private GUIStyle _title, _name, _text, _hint;

        /// <summary>The local hunter may pick now: trial passed, nothing picked, out of a gate (home, out in the world, or the arena), and no story scene showing.</summary>
        public bool CanPick
        {
            get
            {
                var world = _runner != null ? _runner.World : null;
                var me = _runner != null ? _runner.LocalPlayerId : null;
                if (world == null || world.Gate != null || !me.HasValue || !world.HasPlayer(me.Value)) return false;
                if (_scenes != null && _scenes.Playing != null) return false;
                return world.GetPlayer(me.Value).CanChooseSpecialty;
            }
        }

        public bool IsOpen => CanPick && !_later && _sent == null;

        private void Start()
        {
            _runner = GetComponent<SimulationRunner>() ?? FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (_scenes == null) _scenes = FindAnyObjectByType<TextScenePlayer>();
            var world = _runner != null ? _runner.World : null;
            if (world != null && world.Gate != null) _later = false; // "later" lasts until the next time the party comes home
            if (!CanPick) _sent = null; // the pick landed (or the hunter left): a new pick is a new question
            var keyboard = Keyboard.current;
            if (keyboard != null && CanPick && _later && keyboard.kKey.wasPressedThisFrame) _later = false;

            if (_camera == null || IsOpen == _holdingCursor) return;
            _holdingCursor = IsOpen;
            if (_holdingCursor) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: true);
        }

        /// <summary>Takes <paramref name="specialtyId"/> for good: sent with the next tick, and the host decides.</summary>
        public void Choose(string specialtyId)
        {
            var me = _runner.LocalPlayerId;
            if (!me.HasValue) return;
            _runner.RequestSpecialty(me.Value, specialtyId);
            _sent = specialtyId;
        }

        /// <summary>Closes the picker until the party next comes home, or K.</summary>
        public void DecideLater() => _later = true;

        private void OnGUI()
        {
            if (!CanPick) return;
            EnsureStyles();
            if (!IsOpen)
            {
                if (_later) GUI.Label(new Rect(0, 60, Screen.width, 24), "Your specialty is waiting: press K to choose", _hint);
                return;
            }

            var world = _runner.World;
            var me = world.GetPlayer(_runner.LocalPlayerId.Value);
            var specialties = world.SpecialtyOptions(me.Id);
            var width = Mathf.Min(760f, Screen.width - 40f);
            var height = 150f + specialties.Count * 120f;
            var area = new Rect((Screen.width - width) / 2f, Mathf.Max(20f, (Screen.height - height) / 2f), width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel with the default skin (as the class picker does)
            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));
            GUILayout.Label($"Choose your {me.Class?.Name ?? "class"}'s {me.SpecialtyPickWaiting}-rank specialty", _title);
            GUILayout.Label(me.Specialties.Count == 0
                ? "You passed the trial. Your specialty brings new skills and changes how your class's skills work. Later trials branch from it. The choice is permanent."
                : $"You passed the trial. These paths branch from {me.Specialties[me.Specialties.Count - 1].Name}. The choice is permanent.", _text);
            foreach (var s in specialties)
            {
                var built = world.IsSpecialtyBuilt(s);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUI.enabled = built;
                if (GUILayout.Button(built ? s.Name : $"{s.Name}\n(not built yet)", _name, GUILayout.Width(150), GUILayout.Height(90))) Choose(s.Id);
                GUI.enabled = true;
                GUILayout.BeginVertical();
                GUILayout.Label(s.Description ?? "", _text);
                var skills = _runner.Content != null ? _runner.Content.Skills.Where(k => k != null && k.Specialty == s.Id).Select(k => k.DisplayName).ToList() : null;
                var passives = s.Passives.Select(p => p.Name).ToList();
                if (skills != null && skills.Count > 0) GUILayout.Label("Skills: " + string.Join(", ", skills), _text);
                if (passives.Count > 0) GUILayout.Label("Always on: " + string.Join(", ", passives), _text);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Decide later (K)", GUILayout.Height(28))) DecideLater();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _name = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
