using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// The gate board in the hub (tickets 19 and 20). Walk up to it and press E: it lists the gates
    /// (with the ones this host's world has cleared) and the System's intel on each, shows who in the
    /// party is ready, and lets each player mark themselves ready. The host takes the party in once everyone present is ready.
    /// Readiness goes to the simulation as a command, like any input; starting is the host's.
    /// </summary>
    public sealed class GateBoard : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private GUIStyle _title, _label, _button, _intel;
        private string _message;
        private TextScenePlayer _scenes;

        /// <summary>True while the board's panel is open (for tests).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>True when the local player stands close enough to read the board.</summary>
        public bool InReach
        {
            get
            {
                var world = _runner != null ? _runner.World : null;
                var me = _runner != null ? _runner.LocalPlayerId : null;
                if (world == null || !world.InHub || !me.HasValue || !world.HasPlayer(me.Value)) return false;
                return world.IsNearBoard(me.Value);
            }
        }

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (IsOpen && !InReach) SetOpen(false); // walked away, or the party left the hub
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_scenes == null) _scenes = FindAnyObjectByType<TextScenePlayer>();
            if (_scenes != null && _scenes.Playing != null) return; // a story scene has the screen and the keys
            if (keyboard.eKey.wasPressedThisFrame && (IsOpen || InReach)) SetOpen(!IsOpen);
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            _message = null;
            if (_camera == null) return;
            if (open) _camera.ReleaseCursorForMenu(this);
            else _camera.EndMenu(this, recapture: true);
        }

        /// <summary>The local player says they are ready, or not (the board's button).</summary>
        public void SetReady(bool ready)
        {
            var me = _runner.LocalPlayerId;
            if (me.HasValue) _runner.RequestReady(me.Value, ready);
        }

        /// <summary>The host takes the party into <paramref name="gateId"/>; false (with a message) if someone is not ready.</summary>
        public bool StartGate(string gateId)
        {
            if (_runner.Host == null) return false;
            if (_runner.World.TryStartGateFromHub(gateId, out var reason))
            {
                SetOpen(false);
                return true;
            }
            _message = reason;
            return false;
        }

        private void OnGUI()
        {
            var world = _runner != null ? _runner.World : null;
            if (world == null || !world.InHub) return;
            EnsureStyles();
            if (!IsOpen)
            {
                if (InReach) GUI.Label(new Rect(0, Screen.height - 120, Screen.width, 24), "Press E to read the gate board", _title);
                return;
            }

            var me = _runner.LocalPlayerId;
            var area = new Rect(Screen.width / 2f - 280, Screen.height / 2f - 230, 560, 460);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("GATE BOARD", _title);
            GUILayout.Space(6);

            GUILayout.Label("The party", _label);
            foreach (var id in world.PlayerIds)
            {
                var player = world.GetPlayer(id);
                var who = id == me ? $"Player {id.Value} (you)" : $"Player {id.Value}";
                GUILayout.Label($"   {who}  -  {(player.IsAway ? "away" : player.IsReady ? "READY" : "not ready")}", _label);
            }
            var meReady = me.HasValue && world.GetPlayer(me.Value).IsReady;
            if (GUILayout.Button(meReady ? "I'm not ready" : "I'm ready", _button, GUILayout.Height(30))) SetReady(!meReady);

            GUILayout.Space(10);
            GUILayout.Label("Gates", _label);
            var gates = _runner.Content != null ? _runner.Content.Gates.Where(g => g != null).ToList() : new System.Collections.Generic.List<GateDefinitionAsset>();
            var cleared = world.ClearedGates; // the host's world's clears, as every player's world knows them
            var canStart = world.CanStartGate(out var waiting);
            foreach (var gate in gates)
            {
                GUILayout.BeginHorizontal();
                var mark = cleared != null && cleared.Contains(gate.Id) ? "  (cleared)" : "";
                GUILayout.Label($"   {gate.DisplayName}{mark}", _label, GUILayout.Width(300));
                if (_runner.Host != null)
                {
                    GUI.enabled = canStart;
                    if (GUILayout.Button("Enter", _button, GUILayout.Height(26))) StartGate(gate.Id);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(gate.Intel)) GUILayout.Label("      " + gate.Intel, _intel);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_message ?? (_runner.Host == null ? "The host chooses the gate."
                : canStart ? "Everyone is ready." : waiting), _label);
            GUILayout.Label("[E] or [Esc] to close", _label);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _intel = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Italic };
        }
    }
}
