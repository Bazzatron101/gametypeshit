using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The test arena's control panel (F1): spawn any enemy from the game's content, clear them,
    /// set the player's level, teach skills, arrange the bar, toggle any modifier, reset cooldowns
    /// and heal, without leaving the scene. Everything goes through the real simulation operations
    /// and commands, and every list comes from <see cref="GameContentAsset"/>, so there is no
    /// arena-only content. Each button has a public method so tests drive the same paths.
    /// </summary>
    public sealed class ArenaPanel : MonoBehaviour
    {
        private static readonly int[] LevelShortcuts = { 5, 10, 20, 40 };
        private static readonly int[] SpawnCounts = { 1, 3, 6 };

        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Tooltip("Body material for spawned enemies")] private Material enemyMaterial;
        [SerializeField, Min(1f), Tooltip("Spawn ring radius around the player, in metres")] private float spawnRadius = 7f;
        [SerializeField] private bool openOnStart = true;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private readonly List<EnemyView> _spawned = new List<EnemyView>();
        private Vector2 _scroll;
        private bool _weaponsGiven;
        private GUIStyle _label, _header;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<EnemyView> SpawnedEnemies => _spawned;
        private PlayerId Player => new PlayerId(playerId);
        private GameContentAsset Content => _runner != null ? _runner.Content : null;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            // The arena hands out XP, levels and modifiers freely: your character comes in, but nothing done here is kept.
            if (_runner != null) _runner.Autosave.Enabled = false;
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
            if (openOnStart && Application.isFocused) SetOpen(true);
        }

        private void Update()
        {
            // Every weapon is in the bag from the start, so each one can be tried (I opens the gear menu).
            if (!_weaponsGiven && _runner != null && _runner.World != null && _runner.World.HasPlayer(Player) && Content != null)
            {
                GiveAllWeapons();
                _weaponsGiven = true;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) SetOpen(!IsOpen);
            _spawned.RemoveAll(v => v == null);
        }

        /// <summary>Opens or closes the panel. While open the cursor is free and the game ignores input.</summary>
        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (_camera == null) return;
            // Closing the panel always returns to play: it is a tuning tool, opened on purpose.
            if (open) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: true);
        }

        // --- Actions (each button calls one of these) --------------------------------------

        /// <summary>Spawns <paramref name="count"/> of an enemy from the game's content in a ring around the player.</summary>
        public void SpawnRing(EnemyDefinitionAsset definition, int count)
        {
            var centre = PlayerPosition();
            foreach (var point in ArenaLayout.Ring(centre, count, spawnRadius))
                _spawned.Add(_runner.SpawnEnemy(definition, point, enemyMaterial));
        }

        /// <summary>Removes every enemy in the arena, placed or spawned.</summary>
        public void ClearEnemies()
        {
            foreach (var view in FindObjectsByType<EnemyView>(FindObjectsSortMode.None)) _runner.DespawnEnemy(view);
            _spawned.Clear();
        }

        /// <summary>Levels the player up to <paramref name="level"/> through the normal XP path (unlocks and prompts included).</summary>
        public void LevelTo(int level) => _runner.World.RaiseToLevel(Player, level);

        public void TeachAllSkills()
        {
            foreach (var skill in Content.Skills.Where(s => s != null)) _runner.World.TeachSkill(Player, skill.Id);
        }

        /// <summary>Puts a known skill (or nothing) in a bar slot, as a loadout command.</summary>
        public void AssignSlot(int slot, string skillId) => _runner.RequestSkillAssignment(Player, new SkillAssignment(slot, skillId));

        /// <summary>Grants the modifier if the player lacks it, else revokes it.</summary>
        public void ToggleModifier(ModifierDefinitionAsset asset)
        {
            if (IsModifierActive(asset)) _runner.World.RevokeModifier(Player, asset.Id);
            else _runner.World.GrantModifier(Player, asset.ToDefinition());
        }

        public bool IsModifierActive(ModifierDefinitionAsset asset) => _runner.World.HasModifier(Player, asset.Id);

        /// <summary>Puts every piece of gear in the game in the bag, so builds can be tried here (I opens the gear menu).</summary>
        public void GiveAllGear()
        {
            foreach (var gear in Content.Gear)
                if (gear != null) _runner.World.GiveGear(Player, gear.Id);
        }

        /// <summary>Puts every weapon in the game in the bag, without changing the one worn. The arena does this as it opens.</summary>
        public void GiveAllWeapons()
        {
            foreach (var gear in Content.Gear)
                if (gear != null && gear.Slot == GearSlot.Weapon) _runner.World.GiveGear(Player, gear.Id);
        }

        /// <summary>Gives a piece (if it is not in the bag yet) and wears it.</summary>
        public void Wear(GearDefinitionAsset gear) => _runner.World.GiveGear(Player, gear.Id, equip: true);

        /// <summary>
        /// Makes the player another class here, for trying each one (normally the choice is permanent).
        /// Their stats, skills and weapon follow the new class; nothing is saved, as the arena never saves.
        /// </summary>
        public void ChangeClass(string classId) => _runner.World.ChangeClassForTesting(Player, classId);

        public void ResetCooldowns() => _runner.World.ResetCooldowns(Player);

        public void Heal() => _runner.World.RestoreHealth(Player);

        /// <summary>A mount for the character (ticket 42), until the D-rank promotion gives one (ticket 25).</summary>
        public void GiveMount() => _runner.World.GrantMount(Player);

        // --- Drawing -----------------------------------------------------------------------

        private Vector3 PlayerPosition()
        {
            var p = _runner.World.GetPlayer(Player).Position;
            return new Vector3(p.X, 0f, p.Z);
        }

        private void OnGUI()
        {
            if (_runner == null || _runner.World == null || !_runner.World.HasPlayer(Player)) return;
            EnsureStyles();
            if (!IsOpen)
            {
                GUI.Label(new Rect(Screen.width - 220, 12, 210, 22), "[F1] Arena panel", _label);
                return;
            }
            if (Content == null)
            {
                GUI.Label(new Rect(Screen.width - 380, 12, 370, 22), "Arena: no GameContent on the runner.", _label);
                return;
            }

            var state = _runner.World.GetPlayer(Player);
            var area = new Rect(Screen.width - 380, 12, 368, Screen.height - 140);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("TEST ARENA   [F1] close", _header);

            GUILayout.Label($"Enemies ({_runner.World.EnemyIds.Count} in the arena)", _header);
            foreach (var enemy in Content.Enemies)
            {
                if (enemy == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(enemy.name, _label, GUILayout.Width(140));
                foreach (var count in SpawnCounts)
                    if (GUILayout.Button($"+{count}")) SpawnRing(enemy, count);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Clear all enemies")) ClearEnemies();

            GUILayout.Label($"Class: {state.Class?.Name ?? "none"} (test only, not saved)", _header);
            GUILayout.BeginHorizontal();
            foreach (var c in _runner.Classes ?? System.Array.Empty<ClassDefinition>())
            {
                GUI.enabled = c != state.Class;
                if (GUILayout.Button(c.Name)) ChangeClass(c.Id);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Level {state.Level}", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1")) LevelTo(state.Level + 1);
            foreach (var level in LevelShortcuts)
                if (GUILayout.Button(level.ToString())) LevelTo(level);
            if (GUILayout.Button("Max")) LevelTo(int.MaxValue);
            GUILayout.EndHorizontal();

            GUILayout.Label("Skill bar (click a slot to cycle known skills)", _header);
            if (GUILayout.Button("Teach every skill")) TeachAllSkills();
            for (var slot = 0; slot < SimulationWorld.SkillSlotCount; slot++)
            {
                var current = state.SkillSlots[slot].Skill;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"[{slot + 1}]", _label, GUILayout.Width(30));
                if (GUILayout.Button(current != null ? current.Name : "(empty)")) AssignSlot(slot, NextSkill(state, current)?.Id);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Modifiers", _header);
            foreach (var modifier in Content.Modifiers)
            {
                if (modifier == null) continue;
                var active = IsModifierActive(modifier);
                if (GUILayout.Toggle(active, " " + modifier.DisplayName) != active) ToggleModifier(modifier);
            }

            GUILayout.Label("Gear (I: the gear menu)", _header);
            if (GUILayout.Button("Give every piece")) GiveAllGear();
            foreach (var gear in Content.Gear)
            {
                if (gear == null) continue;
                if (GUILayout.Button("Wear " + gear.DisplayName)) Wear(gear);
            }

            GUILayout.Label("Tuning", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset cooldowns")) ResetCooldowns();
            if (GUILayout.Button("Heal")) Heal();
            GUILayout.EndHorizontal();
            var hasMount = _runner.World.GetPlayer(Player).HasMount;
            if (GUILayout.Button(hasMount ? "Has a mount (R in the overworld)" : "Give a mount")) GiveMount();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>The known skill after <paramref name="current"/> in learning order, then empty, then the first again.</summary>
        private static SkillDefinition NextSkill(PlayerState state, SkillDefinition current)
        {
            var known = state.KnownSkills;
            if (known.Count == 0) return null;
            if (current == null) return known[0];
            var index = IndexOf(known, current);
            return index + 1 < known.Count ? known[index + 1] : null;
        }

        private static int IndexOf(IReadOnlyList<SkillDefinition> list, SkillDefinition skill)
        {
            for (var i = 0; i < list.Count; i++) if (list[i] == skill) return i;
            return -1;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.35f, 0.75f, 1f);
        }
    }
}
