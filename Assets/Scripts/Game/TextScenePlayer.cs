using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Plays text-only story scenes (ticket 20) when the simulation starts one: a panel across the
    /// bottom of the screen with the speaker's name and the line, revealed letter by letter. Space,
    /// Enter or a click finishes the line or moves on; Esc skips the rest. Each player reads at their
    /// own pace; the game carries on underneath (scenes play in the safe hub).
    /// </summary>
    public sealed class TextScenePlayer : MonoBehaviour
    {
        [SerializeField, Min(5f), Tooltip("How fast a line is revealed, in characters per second")] private float charactersPerSecond = 45f;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private TextSceneAsset _scene;
        private readonly System.Collections.Generic.Queue<TextSceneAsset> _waiting = new System.Collections.Generic.Queue<TextSceneAsset>();
        private int _line;
        private float _shownSince;
        private GUIStyle _speaker, _text, _hint;

        /// <summary>The scene being read, or null.</summary>
        public TextSceneAsset Playing => _scene;

        /// <summary>Scenes waiting their turn (two can start on the same tick).</summary>
        public int Waiting => _waiting.Count;

        /// <summary>Which line of it is on screen.</summary>
        public int LineIndex => _line;

        /// <summary>True while the current line is still being revealed.</summary>
        public bool Revealing => _scene != null && Revealed(CurrentText) < CurrentText.Length;

        private string CurrentText => _scene != null && _line < _scene.Lines.Count ? _scene.Lines[_line].text ?? "" : "";

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.Content == null) return;
            foreach (var e in _runner.FrameEvents)
            {
                if (!(e is StorySceneStarted started)) continue;
                foreach (var scene in _runner.Content.Scenes)
                    if (scene != null && scene.Id == started.SceneId) Play(scene);
            }
        }

        private void Update()
        {
            if (_scene == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Skip();
            else if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
                     (mouse != null && mouse.leftButton.wasPressedThisFrame)) Advance();
        }

        /// <summary>Plays <paramref name="scene"/> now, or after the one being read.</summary>
        public void Play(TextSceneAsset scene)
        {
            if (scene == null || scene.Lines.Count == 0) return;
            if (_scene != null)
            {
                _waiting.Enqueue(scene);
                return;
            }
            if (_camera != null) _camera.ReleaseCursorForMenu(this);
            var board = FindAnyObjectByType<GateBoard>();
            if (board != null) board.SetOpen(false); // one panel at a time
            Begin(scene);
        }

        private void Begin(TextSceneAsset scene)
        {
            _scene = scene;
            _line = 0;
            _shownSince = Time.unscaledTime;
        }

        /// <summary>Finishes revealing the line, or moves to the next; after the last, the scene ends.</summary>
        public void Advance()
        {
            if (_scene == null) return;
            if (Revealing)
            {
                _shownSince = float.NegativeInfinity; // show it all
                return;
            }
            _line++;
            _shownSince = Time.unscaledTime;
            if (_line >= _scene.Lines.Count) End(recapture: true);
        }

        /// <summary>Skips the rest of this scene (Esc). The cursor is left free, as Esc leaves it everywhere else.</summary>
        public void Skip() => End(recapture: false);

        private void End(bool recapture)
        {
            if (_scene == null) return;
            if (_waiting.Count > 0)
            {
                Begin(_waiting.Dequeue()); // the next one, still in the same menu
                return;
            }
            _scene = null;
            if (_camera != null) _camera.EndMenu(this, recapture);
        }

        private int Revealed(string text) =>
            float.IsNegativeInfinity(_shownSince) ? text.Length : Mathf.Min(text.Length, (int)((Time.unscaledTime - _shownSince) * charactersPerSecond));

        private void OnGUI()
        {
            if (_scene == null) return;
            EnsureStyles();
            var line = _scene.Lines[_line];
            var width = Mathf.Min(900f, Screen.width - 40f);
            var area = new Rect((Screen.width - width) / 2f, Screen.height - 220, width, 190);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel with the default skin
            var inner = new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24);
            if (!string.IsNullOrEmpty(line.speaker)) GUI.Label(new Rect(inner.x, inner.y, inner.width, 24), line.speaker, _speaker);
            var text = CurrentText;
            GUI.Label(new Rect(inner.x, inner.y + 30, inner.width, inner.height - 56), text.Substring(0, Revealed(text)), _text);
            GUI.Label(new Rect(inner.x, inner.yMax - 22, inner.width, 22),
                $"{_line + 1}/{_scene.Lines.Count}    [Space] continue    [Esc] skip", _hint);
        }

        private void EnsureStyles()
        {
            if (_speaker != null) return;
            _speaker = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerRight };
        }
    }
}
