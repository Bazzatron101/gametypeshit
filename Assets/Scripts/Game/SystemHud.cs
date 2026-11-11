using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The System interface, greybox edition: a status panel (level, HP, XP), a level-up
    /// announcement in the System's voice, and a stat screen (Tab) to spend stat points.
    /// Reads from the simulation; stat spending goes back through the runner as commands.
    /// Drawn with IMGUI until the real UI pass.
    /// </summary>
    public sealed class SystemHud : MonoBehaviour
    {
        private const float ToastSeconds = 3.5f;
        private const float ToastFadeSeconds = 0.6f;

        private static readonly Color SystemBlue = new Color(0.35f, 0.75f, 1f);
        private static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.12f, 0.85f);
        private static readonly Color HealthColor = new Color(0.85f, 0.25f, 0.3f);

        private static readonly (Stat stat, string effect)[] StatRows =
        {
            (Stat.Strength, "Attack damage"),
            (Stat.Agility, "Move speed"),
            (Stat.Vitality, "Max health"),
            (Stat.Intelligence, "Skills (coming soon)"),
            (Stat.Sense, "Perception (coming soon)"),
            (Stat.Luck, "Drop odds (coming soon)"),
        };

        [SerializeField, Min(1)] private int playerId = 1;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private float _toastShownAt = float.NegativeInfinity;
        private bool _cursorWasCapturedBeforeMenu;
        private GUIStyle _label, _title, _toastTitle, _toastBody, _button;
        private Texture2D _white;

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool StatScreenOpen { get; private set; }
        public string ToastText { get; private set; } = "";
        public bool ToastVisible => Time.time - _toastShownAt < ToastSeconds;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tabKey.wasPressedThisFrame) SetStatScreenOpen(!StatScreenOpen);
            else if (StatScreenOpen && keyboard.escapeKey.wasPressedThisFrame) SetStatScreenOpen(false);
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;
            foreach (var e in _runner.FrameEvents)
            {
                if (e is PlayerLeveledUp levelUp && levelUp.Player == PlayerId)
                {
                    ToastText = $"You have reached level {levelUp.NewLevel}.  +{levelUp.StatPointsGranted} stat points.  Health restored.";
                    _toastShownAt = Time.time;
                }
            }
        }

        /// <summary>
        /// Opens or closes the stat screen. While open the cursor is free for clicking; closing
        /// re-captures it only if it was captured before (not after the player freed it with Escape).
        /// </summary>
        public void SetStatScreenOpen(bool open)
        {
            if (open == StatScreenOpen) return;
            StatScreenOpen = open;
            if (_camera == null) return;
            if (open)
            {
                _cursorWasCapturedBeforeMenu = Cursor.lockState == CursorLockMode.Locked;
                _camera.ReleaseCursorForMenu();
            }
            else
            {
                _camera.EndMenu(recapture: _cursorWasCapturedBeforeMenu);
            }
        }

        private void OnGUI()
        {
            if (_runner == null || _runner.World == null || !_runner.World.HasPlayer(PlayerId)) return;
            EnsureStyles();
            var state = _runner.World.GetPlayer(PlayerId);

            DrawStatus(state);
            if (ToastVisible) DrawToast();
            if (StatScreenOpen) DrawStatScreen(state);
        }

        private void DrawStatus(PlayerState state)
        {
            var box = new Rect(16, 16, 300, state.UnspentStatPoints > 0 ? 112 : 92);
            Fill(box, PanelColor);
            Outline(box, SystemBlue);

            _title.normal.textColor = SystemBlue;
            GUI.Label(new Rect(box.x + 12, box.y + 6, 280, 24), $"HUNTER    Lv {state.Level}", _title);

            if (state.IsDefeated)
            {
                _label.normal.textColor = HealthColor;
                GUI.Label(new Rect(box.x + 12, box.y + 34, 280, 20), "DEFEATED", _label);
            }
            else
            {
                Bar(new Rect(box.x + 12, box.y + 36, 276, 16), state.Health, state.MaxHealth, HealthColor, $"HP {state.Health} / {state.MaxHealth}");
            }

            var xpText = state.IsAtLevelCap ? "MAX LEVEL" : $"XP {state.Xp} / {state.XpToNextLevel}";
            Bar(new Rect(box.x + 12, box.y + 60, 276, 16), state.IsAtLevelCap ? 1 : state.Xp, state.IsAtLevelCap ? 1 : state.XpToNextLevel, SystemBlue, xpText);

            if (state.UnspentStatPoints > 0)
            {
                _label.normal.textColor = SystemBlue;
                GUI.Label(new Rect(box.x + 12, box.y + 84, 280, 20), $"{state.UnspentStatPoints} stat points to spend  [Tab]", _label);
            }
        }

        private void DrawToast()
        {
            var age = Time.time - _toastShownAt;
            var alpha = Mathf.Clamp01((ToastSeconds - age) / ToastFadeSeconds);
            var box = new Rect((Screen.width - 520) / 2f, 70, 520, 86);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Fill(box, PanelColor);
            Outline(box, SystemBlue);
            _toastTitle.normal.textColor = SystemBlue;
            GUI.Label(new Rect(box.x, box.y + 10, box.width, 30), "[ SYSTEM ]   LEVEL UP", _toastTitle);
            _toastBody.normal.textColor = Color.white;
            GUI.Label(new Rect(box.x, box.y + 46, box.width, 26), ToastText, _toastBody);
            GUI.color = previous;
        }

        private void DrawStatScreen(PlayerState state)
        {
            const float rowHeight = 34f;
            var box = new Rect((Screen.width - 420) / 2f, (Screen.height - 330) / 2f, 420, 120 + StatRows.Length * rowHeight);
            Fill(box, PanelColor);
            Outline(box, SystemBlue);

            _title.normal.textColor = SystemBlue;
            GUI.Label(new Rect(box.x + 16, box.y + 12, 380, 26), $"[ STATUS ]   Level {state.Level}", _title);
            _label.normal.textColor = state.UnspentStatPoints > 0 ? SystemBlue : Color.gray;
            GUI.Label(new Rect(box.x + 16, box.y + 42, 380, 22), $"Unspent points: {state.UnspentStatPoints}", _label);

            for (var i = 0; i < StatRows.Length; i++)
            {
                var (stat, effect) = StatRows[i];
                var y = box.y + 74 + i * rowHeight;
                _label.normal.textColor = Color.white;
                GUI.Label(new Rect(box.x + 16, y, 130, 26), stat.ToString(), _label);
                GUI.Label(new Rect(box.x + 140, y, 40, 26), state.Stats[stat].ToString(), _label);
                _label.normal.textColor = Color.gray;
                GUI.Label(new Rect(box.x + 190, y, 170, 26), effect, _label);

                GUI.enabled = state.UnspentStatPoints > 0;
                if (GUI.Button(new Rect(box.x + 364, y, 40, 26), "+", _button)) _runner.RequestStatAllocation(PlayerId, stat);
                GUI.enabled = true;
            }

            _label.normal.textColor = Color.gray;
            GUI.Label(new Rect(box.x + 16, box.yMax - 32, 380, 22), "[Tab] close", _label);
        }

        private void Bar(Rect rect, int value, int max, Color color, string text)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.6f));
            var fraction = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            Fill(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), color);
            _label.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + 6, rect.y - 2, rect.width, rect.height + 4), text, _label);
        }

        private void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        private void Outline(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), color);
            Fill(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _toastTitle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _toastBody = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        }
    }
}
