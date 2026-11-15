using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// A store's menu (ticket 22). Walk up to a store and press E: it lists your duplicates with
    /// what this store pays for each, and sells one per click. Selling goes to the simulation as a
    /// command, like any input, so the host decides.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private TextScenePlayer _scenes;
        private GUIStyle _title, _label, _button;
        private Vector2 _scroll;

        /// <summary>True while the menu is open (for tests).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The store the local player stands at, or null.</summary>
        public ShopDefinition InReach
        {
            get
            {
                var world = _runner != null ? _runner.World : null;
                var me = _runner != null ? _runner.LocalPlayerId : null;
                if (world == null || !me.HasValue || !world.HasPlayer(me.Value)) return null;
                return world.ShopInReach(me.Value);
            }
        }

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (IsOpen && InReach == null) SetOpen(false); // walked away, or went into a gate
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_scenes == null) _scenes = FindAnyObjectByType<TextScenePlayer>();
            if (_scenes != null && _scenes.Playing != null) return; // a story scene has the screen and the keys
            if (keyboard.eKey.wasPressedThisFrame && (IsOpen || InReach != null)) SetOpen(!IsOpen);
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

        /// <summary>Sells one duplicate of <paramref name="itemId"/> at the store in reach.</summary>
        public void Sell(string itemId)
        {
            var me = _runner != null ? _runner.LocalPlayerId : null;
            if (me.HasValue) _runner.RequestSale(me.Value, itemId);
        }

        private void OnGUI()
        {
            var shop = InReach;
            if (shop == null) return;
            EnsureStyles();
            if (!IsOpen)
            {
                GUI.Label(new Rect(0, Screen.height - 150, Screen.width, 24), $"Press E to trade at the {shop.Name}", _title);
                return;
            }

            var world = _runner.World;
            var me = world.GetPlayer(_runner.LocalPlayerId.Value);
            var area = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 210, 520, 420);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(shop.Name.ToUpperInvariant(), _title);
            GUILayout.Label($"Pays {shop.SellPercent}% of an item's value.   Your gold: {me.Gold}", _label);
            GUILayout.Space(8);

            var owned = me.Duplicates.GroupBy(d => d.Id).ToList();
            if (owned.Count == 0)
            {
                GUILayout.Label("You have nothing to sell. Once you own every reward a gate offers, each clear gives a duplicate to sell here.", _label);
            }
            else
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                foreach (var group in owned)
                {
                    var item = group.First();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{item.Name}  x{group.Count()}", _label, GUILayout.Width(260));
                    if (GUILayout.Button($"Sell for {world.SellPrice(shop, item.Id)} gold", _button, GUILayout.Height(26))) Sell(item.Id);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            if (world.Shops.Any(s => s.SellPercent > shop.SellPercent)) GUILayout.Label("Other stores pay more.", _label);
            GUILayout.Label("Gold buys nothing yet.", _label);
            GUILayout.Label("[E] or [Esc] to close", _label);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
    }
}
