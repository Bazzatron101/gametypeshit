using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The gate on screen: its name and progress along the top, short notices for what happens in
    /// it (a checkpoint reached, a teammate brought back, a wipe), and, while the local player is
    /// down, what they can do about it. Solo that is a button (or Enter) to revive at the latest
    /// checkpoint, or to start the gate again once the revives are spent. In co-op there is
    /// nothing to press: they wait for a teammate to reach a checkpoint.
    /// </summary>
    public sealed class GateHud : MonoBehaviour
    {
        private const float NoticeSeconds = 4f;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private GUIStyle _title, _label, _button, _notice;
        private string _noticeText;
        private float _noticeUntil;
        private bool _cursorFreed;

        /// <summary>The notice on screen now, or null (for tests).</summary>
        public string CurrentNotice => Time.time < _noticeUntil ? _noticeText : null;

        /// <summary>True while the local player is down in a gate and the panel is up (for tests).</summary>
        public bool DeathPanelOpen { get; private set; }

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private PlayerId? Me => _runner != null ? _runner.LocalPlayerId : null;

        private GateState Gate => _runner != null && _runner.World != null ? _runner.World.Gate : null;

        private void Update()
        {
            var down = IsLocalPlayerDown();
            if (down != DeathPanelOpen) SetDeathPanel(down);
            if (down && Gate.IsSolo && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Respawn();
        }

        private bool IsLocalPlayerDown()
        {
            var gate = Gate;
            var me = Me;
            return gate != null && me.HasValue && _runner.World.HasPlayer(me.Value) && _runner.World.GetPlayer(me.Value).IsDefeated;
        }

        /// <summary>The panel has a button, so the cursor is freed while it is up and captured again after (as the stat screen does).</summary>
        private void SetDeathPanel(bool open)
        {
            DeathPanelOpen = open;
            if (_camera == null) return;
            if (open)
            {
                _cursorFreed = true;
                _camera.ReleaseCursorForMenu(this);
            }
            else if (_cursorFreed)
            {
                _cursorFreed = false;
                _camera.EndMenu(this, recapture: true);
            }
        }

        /// <summary>Asks to come back: a revive, or a fresh attempt when none are left. Only the solo rule answers it.</summary>
        public void Respawn()
        {
            var me = Me;
            if (me.HasValue) _runner.RequestRespawn(me.Value);
        }

        private void LateUpdate()
        {
            var gate = Gate;
            if (gate == null)
            {
                _boss = null;
                return;
            }
            FindBoss();
            var me = Me;
            foreach (var e in _runner.FrameEvents)
            {
                switch (e)
                {
                    case CheckpointActivated c:
                        Notify($"Checkpoint {c.Checkpoint + 1} of {gate.Layout.Checkpoints.Count} reached.");
                        break;
                    case PlayerRevived r when r.Player == me:
                        Notify(r.RevivesLeft == PlayerRevived.NoLimit
                            ? "A teammate reached a checkpoint. You are back."
                            : $"Revived. {r.RevivesLeft} of {gate.Definition.SoloRevives} revives left.");
                        break;
                    case PlayerRevived r:
                        Notify($"Player {r.Player.Value} is back at the checkpoint.");
                        break;
                    case GateWiped _:
                        Notify("The party fell. Back to the last checkpoint; unfinished rooms have refilled.");
                        break;
                    case GateRestarted g:
                        Notify($"Out of revives. The gate starts again (attempt {g.Attempt}).");
                        break;
                    case GateCleared _:
                        Notify($"{gate.Definition.Name} cleared.");
                        break;
                    case BossEnraged b:
                        Notify($"{_runner.World.GetEnemy(b.Enemy).Definition.Name} is enraged! It strikes faster, and help has arrived.");
                        break;
                    case GateRewardGranted r when r.Player == me:
                        Notify(r.Modifier != null
                            ? $"{gate.Definition.Name} cleared.  Reward: {r.Modifier.Name}, kept on your hunter."
                            : $"{gate.Definition.Name} cleared.  You already hold every reward this gate offers.");
                        break;
                }
            }
        }

        private void Notify(string text)
        {
            _noticeText = text;
            _noticeUntil = Time.time + NoticeSeconds;
        }

        private void OnGUI()
        {
            var gate = Gate;
            if (gate == null) return;
            EnsureStyles();

            var cleared = 0;
            var rooms = gate.Layout.Rooms.Count;
            for (var i = 0; i < rooms; i++) if (gate.RoomState(i) == RoomState.Cleared) cleared++;
            var header = $"{gate.Definition.Name}    Rooms cleared {cleared}/{rooms}    Checkpoint {gate.LatestCheckpoint + 1}/{gate.Layout.Checkpoints.Count}";
            if (gate.IsSolo && Me.HasValue) header += $"    Revives {gate.RevivesLeft(Me.Value)}/{gate.Definition.SoloRevives}";
            GUI.Label(new Rect(0, 10, Screen.width, 26), header, _title);

            if (CurrentNotice != null) GUI.Label(new Rect(0, 40, Screen.width, 24), CurrentNotice, _notice);
            DrawBossBar();
            if (DeathPanelOpen) DrawDeathPanel(gate);
        }

        /// <summary>The boss's name and health across the top while it is out and standing.</summary>
        private void DrawBossBar()
        {
            if (!_boss.HasValue || !_runner.World.HasEnemy(_boss.Value)) return;
            {
                var enemy = _runner.World.GetEnemy(_boss.Value);
                var width = Mathf.Min(560f, Screen.width - 40f);
                var area = new Rect((Screen.width - width) / 2f, 70, width, 18);
                GUI.Label(new Rect(area.x, area.y - 22, width, 20), enemy.IsEnraged ? enemy.Definition.Name + "  (ENRAGED)" : enemy.Definition.Name, _title);
                GUI.Box(area, GUIContent.none);
                var fill = new Rect(area.x + 2, area.y + 2, (width - 4) * enemy.Health / (float)enemy.Definition.MaxHealth, area.height - 4);
                var old = GUI.color;
                GUI.color = enemy.IsEnraged ? new Color(1f, 0.35f, 0.2f) : new Color(0.85f, 0.15f, 0.15f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        /// <summary>The standing boss whose health bar is up, if any; found each frame.</summary>
        private EnemyId? _boss;

        private void FindBoss()
        {
            _boss = null;
            var world = _runner.World;
            foreach (var id in world.EnemyIds)
            {
                var enemy = world.GetEnemy(id);
                if (!enemy.IsBoss || enemy.IsDefeated) continue;
                _boss = id;
                return;
            }
        }

        /// <summary>True while a boss's health bar is on screen (for tests).</summary>
        public bool BossShown => _boss.HasValue;

        private void DrawDeathPanel(GateState gate)
        {
            var area = new Rect(Screen.width / 2f - 220, Screen.height / 2f - 80, 440, 160);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("YOU HAVE FALLEN", _title);
            GUILayout.Space(8);
            if (gate.IsSolo)
            {
                var left = gate.RevivesLeft(Me.Value);
                GUILayout.Label(left > 0
                    ? $"You can get up at the last checkpoint. {left} of {gate.Definition.SoloRevives} revives left."
                    : "No revives left. The gate starts again from the entrance; you keep your levels and XP.", _label);
                GUILayout.Space(8);
                if (GUILayout.Button(left > 0 ? "Revive at the checkpoint  [Enter]" : "Start the gate again  [Enter]", _button, GUILayout.Height(34))) Respawn();
            }
            else
            {
                var wipe = gate.SecondsUntilWipe;
                GUILayout.Label(wipe.HasValue
                    ? $"The whole party is down. Back to the last checkpoint in {Mathf.CeilToInt(wipe.Value)}..."
                    : "You will be back when a teammate reaches a checkpoint.", _label);
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _notice = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
    }
}
