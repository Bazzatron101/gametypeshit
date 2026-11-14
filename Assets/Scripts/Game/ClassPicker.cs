using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Asks for a class when the local character has none (ticket 24): a new character, or one
    /// saved before classes existed. The choice is permanent and saved at once. Offline the world
    /// waits for it; in the co-op menu, hosting and joining wait for it.
    /// </summary>
    public sealed class ClassPicker : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private bool _holdingCursor;
        private GUIStyle _title, _name, _text;

        public bool IsOpen => _runner != null && _runner.NeedsClass;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (_camera == null || IsOpen == _holdingCursor) return;
            _holdingCursor = IsOpen;
            if (_holdingCursor) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: _runner.PlaysOffline);
        }

        public void Choose(string classId) => _runner.ChooseClass(classId);

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            var classes = _runner.Classes;
            var width = Mathf.Min(720f, Screen.width - 40f);
            var height = 90f + classes.Count * 78f;
            var area = new Rect((Screen.width - width) / 2f, Mathf.Max(20f, (Screen.height - height) / 2f), width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel with the default skin (as the story scenes do)
            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));
            GUILayout.Label("Choose your class", _title);
            GUILayout.Label(_runner.Character.IsNew
                ? "Your hunter's class decides their skills and how they grow. The choice is permanent."
                : $"Your level {_runner.Character.Level} hunter was made before classes. Their class gives the stats and skills their levels have earned. The choice is permanent.", _text);
            foreach (var c in classes)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                if (GUILayout.Button(c.Name, _name, GUILayout.Width(130), GUILayout.Height(56))) Choose(c.Id);
                GUILayout.Label(c.Description ?? "", _text);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _name = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        }
    }
}
