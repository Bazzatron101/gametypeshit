using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The System interface, greybox edition: a status panel (level, HP, XP), System
    /// announcements (level-ups, new skills) shown one after another, the six-slot skill bar with
    /// cooldowns, and a stat screen (Tab) to spend stat points.
    /// Reads from the simulation; stat spending goes back through the runner as commands.
    /// Drawn with IMGUI until the real UI pass.
    /// </summary>
    public sealed class SystemHud : MonoBehaviour
    {

        private static readonly Color SystemBlue = new Color(0.35f, 0.75f, 1f);
        private static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.12f, 0.85f);
        private static readonly Color HealthColor = new Color(0.85f, 0.25f, 0.3f);

        private static readonly (Stat stat, string effect)[] StatRows =
        {
            (Stat.Strength, "Attack damage"),
            (Stat.Agility, "Move speed"),
            (Stat.Vitality, "Max health"),
            (Stat.Intelligence, "Magic skill damage"),
            (Stat.Sense, "Perception (coming soon)"),
            (Stat.Luck, "Drop odds (coming soon)"),
        };

        [SerializeField, Min(1)] private int playerId = 1;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private DebugModifierToggles _toggles;
        private readonly AnnouncementQueue _announcements = new AnnouncementQueue();
        private float _counterShownAt = float.NegativeInfinity;
        private const float CounterSeconds = 1.2f;
        private bool _cursorWasCapturedBeforeMenu;
        private GUIStyle _label, _title, _toastTitle, _toastBody, _button, _skillName;
        private Texture2D _white;

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool StatScreenOpen { get; private set; }

        /// <summary>The announcement on screen now, if any.</summary>
        public AnnouncementQueue.Announcement? CurrentAnnouncement => _announcements.Current(Time.time);

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
            _toggles = FindAnyObjectByType<DebugModifierToggles>();
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
                switch (e)
                {
                    case PlayerLeveledUp levelUp when levelUp.Player == PlayerId:
                        _announcements.Enqueue("LEVEL UP",
                            $"You have reached level {levelUp.NewLevel}.  +{levelUp.StatPointsGranted} stat points.  Health restored.", Time.time);
                        break;
                    case SkillChargeUnlocked charge when charge.Player == PlayerId:
                        _announcements.Enqueue("CHARGE UNLOCKED",
                            $"Hold the key to charge  {charge.Skill.Name}  for {ChargeDescription(charge.Skill)}.", Time.time);
                        break;
                    case SkillUsed used when used.Player == PlayerId && used.IsCounter:
                        _counterShownAt = Time.time;
                        break;
                    case SkillUnlocked unlocked when unlocked.Player == PlayerId:
                        _announcements.Enqueue("NEW SKILL ACQUIRED", unlocked.Slot.HasValue
                            ? $"{unlocked.Skill.Name}  is ready on key [{unlocked.Slot.Value + 1}]."
                            : $"{unlocked.Skill.Name}  learned. Your skill bar is full.", Time.time);
                        break;
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
                _cursorWasCapturedBeforeMenu = Cursor.lockState == CursorLockMode.Locked || _camera.InMenu;
                _camera.ReleaseCursorForMenu(this);
            }
            else
            {
                _camera.EndMenu(this, recapture: _cursorWasCapturedBeforeMenu);
            }
        }

        private void OnGUI()
        {
            if (_runner == null || _runner.World == null || !_runner.World.HasPlayer(PlayerId)) return;
            EnsureStyles();
            var state = _runner.World.GetPlayer(PlayerId);

            DrawStatus(state);
            DrawModifiers(state);
            DrawSkillBar(state);
            DrawChargeMeter(state);
            DrawCounterBanner();
            var announcement = CurrentAnnouncement;
            if (announcement.HasValue) DrawAnnouncement(announcement.Value);
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

        private void DrawAnnouncement(AnnouncementQueue.Announcement announcement)
        {
            var box = new Rect((Screen.width - 520) / 2f, 70, 520, 86);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, announcement.Alpha);
            Fill(box, PanelColor);
            Outline(box, SystemBlue);
            _toastTitle.normal.textColor = SystemBlue;
            GUI.Label(new Rect(box.x, box.y + 10, box.width, 30), $"[ SYSTEM ]   {announcement.Title}", _toastTitle);
            _toastBody.normal.textColor = Color.white;
            GUI.Label(new Rect(box.x, box.y + 46, box.width, 26), announcement.Body, _toastBody);
            GUI.color = previous;
        }

        /// <summary>Active modifiers under the status panel, with the greybox toggle keys.</summary>
        private void DrawModifiers(PlayerState state)
        {
            var toggles = _toggles;
            var rows = toggles != null ? toggles.Count : 0;
            if (rows == 0 && state.ActiveModifiers.Count == 0) return;

            var box = new Rect(16, 140, 300, 30 + Mathf.Max(rows, state.ActiveModifiers.Count) * 20);
            Fill(box, PanelColor);
            Outline(box, SystemBlue);
            _label.normal.textColor = SystemBlue;
            GUI.Label(new Rect(box.x + 12, box.y + 4, 280, 20), "MODIFIERS", _label);

            if (rows > 0)
            {
                for (var i = 0; i < rows; i++)
                {
                    var asset = toggles[i];
                    if (asset == null) continue;
                    var active = toggles.IsActive(i);
                    _label.normal.textColor = active ? Color.white : Color.gray;
                    GUI.Label(new Rect(box.x + 12, box.y + 24 + i * 20, 280, 20),
                        $"[{DebugModifierToggles.KeyName(i)}] {(active ? "ON " : "off")}  {asset.DisplayName}", _label);
                }
            }
            else
            {
                for (var i = 0; i < state.ActiveModifiers.Count; i++)
                {
                    _label.normal.textColor = Color.white;
                    GUI.Label(new Rect(box.x + 12, box.y + 24 + i * 20, 280, 20), state.ActiveModifiers[i].Name, _label);
                }
            }
        }

        private static string ChargeDescription(SkillDefinition skill)
        {
            if (skill.ChargedSizePercent > 0 && skill.ChargedDamagePercent > 0)
                return $"up to +{skill.ChargedSizePercent}% size and +{skill.ChargedDamagePercent}% damage";
            return skill.ChargedSizePercent > 0 ? $"up to +{skill.ChargedSizePercent}% size" : $"up to +{skill.ChargedDamagePercent}% damage";
        }

        /// <summary>A meter above the skill bar while a skill is charging.</summary>
        private void DrawChargeMeter(PlayerState state)
        {
            if (!state.ChargingSlot.HasValue) return;
            var skill = state.SkillSlots[state.ChargingSlot.Value].Skill;
            var full = state.ChargeFraction >= 1f;
            var rect = new Rect((Screen.width - 300) / 2f, Screen.height - 140, 300, 18);
            Bar(rect, Mathf.RoundToInt(state.ChargeFraction * 100), 100, full ? Color.white : SystemBlue,
                full ? $"{skill?.Name}  FULL CHARGE" : $"Charging {skill?.Name}");
        }

        /// <summary>A brief flash when a perfect dodge launches the charge.</summary>
        private void DrawCounterBanner()
        {
            if (Time.time - _counterShownAt < CounterSeconds)
            {
                _toastTitle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40), "PERFECT DODGE  —  COUNTER!", _toastTitle);
            }
        }

        /// <summary>Six slots along the bottom: key, skill name, and a darkened sweep with seconds left while cooling down.</summary>
        private void DrawSkillBar(PlayerState state)
        {
            const float size = 78f, gap = 8f;
            var width = SimulationWorld.SkillSlotCount * size + (SimulationWorld.SkillSlotCount - 1) * gap;
            var x0 = (Screen.width - width) / 2f;
            var y = Screen.height - size - 18f;
            var tickRate = 1f / _runner.World.TickDelta;

            for (var i = 0; i < SimulationWorld.SkillSlotCount; i++)
            {
                var slot = state.SkillSlots[i];
                var box = new Rect(x0 + i * (size + gap), y, size, size);
                Fill(box, PanelColor);
                Outline(box, slot.IsEmpty ? new Color(0.3f, 0.35f, 0.45f) : SystemBlue);

                _label.normal.textColor = Color.gray;
                GUI.Label(new Rect(box.x + 6, box.y + 2, 30, 18), (i + 1).ToString(), _label);
                if (slot.IsEmpty) continue;

                var remaining = slot.TicksUntilReady(_runner.World.Tick) / tickRate;
                if (remaining > 0f)
                {
                    var total = Mathf.Max(slot.CooldownTicks / tickRate, 0.0001f);
                    var fraction = Mathf.Clamp01(remaining / total);
                    Fill(new Rect(box.x, box.yMax - box.height * fraction, box.width, box.height * fraction), new Color(0f, 0f, 0f, 0.6f));
                }

                _skillName.normal.textColor = remaining > 0f ? Color.gray : Color.white;
                GUI.Label(new Rect(box.x + 4, box.y + 22, box.width - 8, 34), slot.Skill.Name, _skillName);
                if (slot.ChargeUnlocked)
                {
                    _label.normal.textColor = SystemBlue;
                    GUI.Label(new Rect(box.xMax - 40, box.yMax - 22, 38, 20), "HOLD", _label); // bottom right, clear of the name
                }
                if (remaining > 0f)
                {
                    _label.normal.textColor = Color.white;
                    GUI.Label(new Rect(box.x + 6, box.yMax - 22, box.width - 8, 20), remaining.ToString("0.0") + "s", _label);
                }
            }
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
            _skillName = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        }
    }
}
