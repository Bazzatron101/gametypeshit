using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The character creator (ticket 28). A hunter with no name (a new one, or one saved before
    /// names) is made here first: a name and looks (a body colour and a build). The class picker
    /// comes next. The name is permanent; the looks can be changed later in the hub (press L),
    /// which goes to the simulation as a command, so the host decides and everyone sees it.
    /// </summary>
    public sealed class CharacterCreator : MonoBehaviour
    {
        public enum Mode { Closed, Create, Restyle }

        private SimulationRunner _found;
        private SimulationRunner _runner
        {
            get
            {
                if (_found == null) _found = GetComponent<SimulationRunner>(); // found when first needed: tests ask before Start
                if (_found == null) _found = FindAnyObjectByType<SimulationRunner>();
                return _found;
            }
        }
        private ThirdPersonCamera _camera;
        private TextScenePlayer _scenes;
        private GUIStyle _title, _label, _small, _button, _field;
        private bool _holdingCursor;
        private bool _restyling;

        private string _name = "";
        private int _colour;
        private Build _build;
        private bool _createLoaded;

        /// <summary>What the creator is showing now.</summary>
        public Mode Showing =>
            _runner == null ? Mode.Closed
            : _runner.NeedsCreation ? Mode.Create
            : _restyling ? Mode.Restyle
            : Mode.Closed;

        public bool IsOpen => Showing != Mode.Closed;

        private void Start()
        {
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (_runner == null) return;
            if (_restyling && !CanRestyle()) _restyling = false; // left the hub, or a gate started

            var keyboard = Keyboard.current;
            if (keyboard != null && !_runner.NeedsSetup && !SceneHasTheScreen())
            {
                if (keyboard.lKey.wasPressedThisFrame && !_restyling && CanRestyle()) OpenRestyle();
                else if (_restyling && keyboard.escapeKey.wasPressedThisFrame) _restyling = false;
            }

            if (_camera == null || IsOpen == _holdingCursor) return;
            _holdingCursor = IsOpen;
            if (_holdingCursor) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: _runner.PlaysOffline || _runner.IsOnline);
        }

        private bool SceneHasTheScreen()
        {
            if (_scenes == null) _scenes = FindAnyObjectByType<TextScenePlayer>();
            return _scenes != null && _scenes.Playing != null;
        }

        /// <summary>Looks can be changed in the hub, by a hunter who is in the world.</summary>
        public bool CanRestyle()
        {
            var world = _runner != null ? _runner.World : null;
            var me = _runner != null ? _runner.MyPlayerId : null;
            return world != null && me.HasValue && world.HasPlayer(me.Value) && world.InHub && !_runner.NeedsSetup;
        }

        /// <summary>Opens the looks screen in the hub (the L key), starting from how the hunter looks now.</summary>
        public void OpenRestyle()
        {
            if (!CanRestyle()) return;
            var look = _runner.World.GetPlayer(_runner.MyPlayerId.Value).Look;
            _colour = look.Colour;
            _build = look.Build;
            _restyling = true;
        }

        /// <summary>Makes the hunter: the name (permanent) and the looks. The class picker comes next.</summary>
        public void Create(string hunterName, CharacterLook look) => _runner.CreateCharacter(hunterName, look);

        /// <summary>In the hub: wear new looks. Sent as a command; the host applies it.</summary>
        public void Restyle(CharacterLook look)
        {
            var me = _runner.MyPlayerId;
            if (me.HasValue) _runner.RequestLook(me.Value, look);
            _restyling = false;
        }

        private void OnGUI()
        {
            var showing = Showing;
            if (showing == Mode.Closed) return;
            EnsureStyles();

            if (showing == Mode.Create && !_createLoaded)
            {
                // A character from before names keeps the look it may already have (the default otherwise).
                _colour = _runner.Character.Look.Colour;
                _build = _runner.Character.Look.Build;
                _createLoaded = true;
            }

            var width = Mathf.Min(560f, Screen.width - 40f);
            var height = showing == Mode.Create ? 360f : 300f;
            var area = new Rect((Screen.width - width) / 2f, Mathf.Max(20f, (Screen.height - height) / 2f), width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel with the default skin (as the class picker does)
            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));

            if (showing == Mode.Create)
            {
                GUILayout.Label("Make your hunter", _title);
                GUILayout.Label(_runner.Character.IsNew
                    ? "Choose a name and how your hunter looks. The name is permanent; looks can be changed in the hub (L)."
                    : $"Your level {_runner.Character.Level} hunter was made before names. Give them one; it is permanent.", _small);
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", _label, GUILayout.Width(70));
                _name = GUILayout.TextField(_name ?? "", HunterNames.MaxLength + 4, _field, GUILayout.Width(260));
                GUILayout.EndHorizontal();
            }
            else
            {
                var me = _runner.World.GetPlayer(_runner.MyPlayerId.Value);
                GUILayout.Label("Change your looks", _title);
                GUILayout.Label($"{me.Name ?? "Your hunter"}: the name stays; the colour and build can change here in the hub.", _small);
            }

            GUILayout.Space(8);
            DrawColourChoice();
            GUILayout.Space(6);
            DrawBuildChoice();
            GUILayout.Space(10);

            var look = new CharacterLook(_colour, _build);
            GUILayout.BeginHorizontal();
            if (showing == Mode.Create)
            {
                var clean = HunterNames.Clean(_name);
                GUI.enabled = clean != null;
                if (GUILayout.Button(clean != null ? $"Continue as {clean}" : "Type a name to continue", _button, GUILayout.Height(36)))
                    Create(clean, look);
                GUI.enabled = true;
            }
            else
            {
                if (GUILayout.Button("Wear these looks", _button, GUILayout.Height(36))) Restyle(look);
                if (GUILayout.Button("Cancel", _button, GUILayout.Width(120), GUILayout.Height(36))) _restyling = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawColourChoice()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Colour", _label, GUILayout.Width(70));
            var previous = GUI.backgroundColor;
            for (var i = 0; i < CharacterLook.ColourCount; i++)
            {
                GUI.backgroundColor = CharacterLooks.Colour(i);
                var chosen = i == _colour;
                if (GUILayout.Button(chosen ? "✓" : "", _button, GUILayout.Width(44), GUILayout.Height(32))) _colour = i;
            }
            GUI.backgroundColor = previous;
            GUILayout.EndHorizontal();
            GUILayout.Label($"          {CharacterLooks.ColourName(_colour)}", _small);
        }

        private void DrawBuildChoice()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build", _label, GUILayout.Width(70));
            foreach (var build in new[] { Build.Slim, Build.Average, Build.Broad })
            {
                var chosen = build == _build;
                if (GUILayout.Button(chosen ? $"[ {build} ]" : build.ToString(), _button, GUILayout.Width(110), GUILayout.Height(32))) _build = build;
            }
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 16 };
        }
    }
}
