using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Asks for a species when the local hunter has none (ticket 81): part of making a hunter, after the name and
    /// looks and before the class; or once, for a hunter saved before species. The choice is permanent and saved
    /// at once. Offline the world waits for it; in the co-op menu, hosting and joining wait for it.
    /// </summary>
    public sealed class SpeciesPicker : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private bool _holdingCursor;
        private Vector2 _scroll;
        private GUIStyle _title, _name, _text;

        public bool IsOpen => _runner != null && _runner.NeedsSpecies && !_runner.NeedsCreation; // after the name and looks (ticket 28)

        private void Start()
        {
            _runner = GetComponent<SimulationRunner>() ?? FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (_camera == null || IsOpen == _holdingCursor) return;
            _holdingCursor = IsOpen;
            if (_holdingCursor) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: _runner.PlaysOffline && !_runner.NeedsSetup);
        }

        public void Choose(string speciesId) => _runner.ChooseSpecies(speciesId);

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            var species = _runner.Species;
            var width = Mathf.Min(760f, Screen.width - 40f);
            var height = Mathf.Min(Screen.height - 40f, 100f + species.Count * 66f);
            var area = new Rect((Screen.width - width) / 2f, Mathf.Max(20f, (Screen.height - height) / 2f), width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel with the default skin (as the class picker does)
            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));
            GUILayout.Label("Choose your species", _title);
            GUILayout.Label(_runner.Character.IsNew
                ? "Every hunter is an Australian animal. Your species gives a stat bonus that grows with each rank, and a small perk. The choice is permanent."
                : "Your hunter was made before species. Pick one: they keep their class and progress. The choice is permanent.", _text);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var s in species)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                if (GUILayout.Button(s.Name, _name, GUILayout.Width(160), GUILayout.Height(48))) Choose(s.Id);
                GUILayout.Label(SpeciesLooks.Summary(s), _text);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _name = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        }
    }
}
