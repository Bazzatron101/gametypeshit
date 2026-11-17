using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The gear screen (ticket 23, reworked in ticket 48). Press I anywhere. On the left, the six worn
    /// slots, each with what it does; click a slot to see only the bag's pieces for it. On the right,
    /// the bag, filtered by slot, every piece with what it does and an "Equip" button. Picking a piece
    /// shows it in full and what would change against the piece worn in its slot. Changes go to the
    /// simulation as commands, like any input, so the host decides.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private TextScenePlayer _scenes;
        private GUIStyle _title, _label, _small, _button, _item, _heading, _selected, _good, _bad;
        private Vector2 _scroll;
        private GearSlot? _filter;   // null: every slot
        private string _pickedId;    // the piece shown in the details box

        /// <summary>True while the menu is open (for tests).</summary>
        public bool IsOpen { get; private set; }

        public static string SlotName(GearSlot slot) => slot.ToString();

        /// <summary>The slot the bag is filtered to, or null for every slot (for tests).</summary>
        public GearSlot? Filter => _filter;

        /// <summary>Shows only the bag's pieces for one slot, or every piece (null).</summary>
        public void SetFilter(GearSlot? slot) => _filter = slot;

        /// <summary>The bag's pieces the screen lists: not worn, in the filtered slot, by slot then name.</summary>
        public static List<GearDefinition> Listed(PlayerState state, GearSlot? filter) =>
            state.Bag.Where(g => state.Worn[(int)g.Slot] != g && (!filter.HasValue || g.Slot == filter.Value))
                .OrderBy(g => g.Slot).ThenBy(g => g.Name).ToList();

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

            var width = Mathf.Min(980f, Screen.width - 40f);
            var height = Mathf.Min(640f, Screen.height - 40f);
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice: a darker panel behind the text
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 10, area.width - 24, area.height - 20));
            GUILayout.Label("GEAR", _title);
            GUILayout.BeginHorizontal();

            // --- Left: what you wear -------------------------------------------------------
            GUILayout.BeginVertical(GUILayout.Width(width * 0.42f));
            GUILayout.Label("Wearing (click a slot to see your spares for it)", _heading);
            for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
            {
                var worn = state.Worn[slot];
                var filtered = _filter.HasValue && (int)_filter.Value == slot;
                var spare = state.Bag.Count(g => (int)g.Slot == slot && worn != g);
                GUILayout.BeginVertical(filtered ? _selected : GUI.skin.box);
                GUILayout.BeginHorizontal();
                var slotLabel = SlotName((GearSlot)slot) + (spare > 0 ? $" ({spare})" : "");
                if (GUILayout.Button(slotLabel, _button, GUILayout.Width(110))) _filter = filtered ? (GearSlot?)null : (GearSlot)slot;
                GUILayout.Label(worn != null ? GearText.ColouredName(worn) : "— empty —", _label);
                if (worn != null && GUILayout.Button("Take off", _button, GUILayout.Width(80))) _runner.RequestUnequip(me.Value, (GearSlot)slot);
                GUILayout.EndHorizontal();
                if (worn != null)
                    foreach (var line in GearText.Effects(worn, SkillName)) GUILayout.Label("  " + line, _small);
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // --- Right: the bag, then the picked piece -------------------------------------
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bag", _heading, GUILayout.Width(40));
            if (GUILayout.Toggle(!_filter.HasValue, "All", _button) && _filter.HasValue) _filter = null;
            for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
            {
                var on = _filter.HasValue && (int)_filter.Value == slot;
                if (GUILayout.Toggle(on, SlotName((GearSlot)slot), _button) != on) _filter = on ? (GearSlot?)null : (GearSlot)slot;
            }
            GUILayout.EndHorizontal();

            var listed = Listed(state, _filter);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height * 0.42f));
            if (listed.Count == 0)
            {
                GUILayout.Label(state.Bag.Count == 0
                    ? "Your bag is empty. Gates drop gear when they are cleared."
                    : _filter.HasValue ? $"Nothing else for {SlotName(_filter.Value)} in your bag." : "You are wearing everything you have.", _label);
            }
            foreach (var gear in listed)
            {
                GUILayout.BeginHorizontal(gear.Id == _pickedId ? _selected : GUIStyle.none);
                var summary = GearText.Effects(gear, SkillName)[0];
                if (GUILayout.Button($"<b>{GearText.ColouredName(gear)}</b>   <size=11>{gear.Rarity} {SlotName(gear.Slot)}  ·  {summary}</size>", _item))
                    _pickedId = gear.Id == _pickedId ? null : gear.Id;
                var canWear = gear.UsableBy(state.Class?.Id); // another class's weapon stays in the bag (ticket 61)
                GUI.enabled = canWear;
                if (GUILayout.Button(canWear ? "Equip" : "Not yours", _button, GUILayout.Width(70), GUILayout.Height(34))) Equip(me.Value, gear);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            // The picked piece in full, and what changes if you wear it.
            var picked = state.Bag.FirstOrDefault(g => g.Id == _pickedId);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            if (picked == null)
            {
                GUILayout.Label("Click a piece to see what it does and how it compares with what you wear.", _small);
            }
            else
            {
                var wornNow = state.Worn[(int)picked.Slot];
                var tags = picked.Tags != null && picked.Tags.Length > 0 ? "; " + string.Join(", ", picked.Tags) : "";
                GUILayout.Label($"{GearText.ColouredName(picked)}  ({SlotName(picked.Slot)}{tags})", _heading);
                GUILayout.Label(GearText.RarityLine(picked, ClassName), _small);
                if (!string.IsNullOrEmpty(picked.Description)) GUILayout.Label(picked.Description, _small);
                foreach (var line in GearText.Effects(picked, SkillName)) GUILayout.Label("• " + line, _label);
                if (wornNow == picked)
                {
                    GUILayout.Label("You are wearing this.", _small);
                }
                else
                {
                    GUILayout.Space(4);
                    GUILayout.Label(wornNow != null ? $"Compared with your {wornNow.Name}:" : "Compared with wearing nothing:", _heading);
                    foreach (var line in GearText.Compare(picked, wornNow, SkillName))
                        GUILayout.Label(line, line.StartsWith("−") ? _bad : line.StartsWith("+") ? _good : _label);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.Label("[I] or [Esc] to close", _small);
            GUILayout.EndArea();
        }

        private void Equip(PlayerId me, GearDefinition gear)
        {
            _runner.RequestEquip(me, gear.Id);
            if (_pickedId == gear.Id) _pickedId = null;
        }

        private string ClassName(string id)
        {
            var content = _runner != null ? _runner.Content : null;
            var found = content != null ? content.Classes.FirstOrDefault(c => c != null && c.Id == id) : null;
            return found != null ? found.DisplayName : id;
        }

        private string SkillName(string id)
        {
            var content = _runner != null ? _runner.Content : null;
            var skill = content != null ? content.Skills.FirstOrDefault(s => s != null && s.Id == id) : null;
            return skill != null ? skill.DisplayName : null;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            _heading = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            _heading.normal.textColor = new Color(0.35f, 0.75f, 1f);
            _item = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
            _selected = new GUIStyle(GUI.skin.box);
            _selected.normal.background = Solid(new Color(0.2f, 0.45f, 0.75f, 0.55f));
            _good = new GUIStyle(_label);
            _good.normal.textColor = new Color(0.45f, 0.95f, 0.45f);
            _bad = new GUIStyle(_label);
            _bad.normal.textColor = new Color(1f, 0.5f, 0.45f);
        }

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }
    }
}
