using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one player. Its starting transform is the spawn point; after that it
    /// only reads state from the simulation and never writes to it. Place it at the feet;
    /// child visuals (the capsule) sit above. It turns to the simulation's facing, shows a swing
    /// marker on each light attack, flashes when hit, squashes while dodging and lies down when defeated.
    /// What the player wears is drawn on the body (ticket 34, see <see cref="GearLooks"/>); online, other
    /// players' gear comes from host snapshots, so everyone sees everyone's own gear.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float SwingShowSeconds = 0.12f;
        private const float HitFlashSeconds = 0.15f;

        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Tooltip("Driven by this machine's keyboard/gamepad.")] private bool isLocal = true;
        [SerializeField, Tooltip("Shown briefly when this player attacks")] private GameObject swingMarker;
        [SerializeField, Tooltip("Flashed on hit, squashed on dodge, laid down on defeat")] private Renderer body;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField, Tooltip("Body tint while winding up a heavy attack")] private Color windupColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField, Tooltip("Body tint at full charge; it builds up from the normal colour")] private Color chargeColor = new Color(0.55f, 0.9f, 1f);
        [SerializeField, Tooltip("Swing marker scale for a heavy attack, relative to a light one")] private float heavySwingScale = 1.6f;

        private SimulationRunner _runner;
        private readonly BodyTint _tint = new BodyTint();
        private float _swingUntil;
        private Vector3 _swingScale;
        private Vector3 _bodyScale;
        private Vector3 _bodyPosition;
        private GameObject _mount;
        private System.Collections.Generic.Dictionary<string, GameObject> _gearLooks;
        private readonly string[] _shownGear = new string[GearDefinition.SlotCount]; // by id: a new world (a rejoin) has new definitions of the same pieces
        private readonly GameObject[] _gearObjects = new GameObject[GearDefinition.SlotCount];

        /// <summary>How much higher a rider sits (ticket 42).</summary>
        private const float SaddleHeight = 0.9f;

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool IsLocal => isLocal;

        /// <summary>The look drawn for the piece worn in <paramref name="slot"/>, or null when nothing is shown there.</summary>
        public GameObject WornLook(GearSlot slot) => _gearObjects[(int)slot];

        /// <summary>Sets up a view built in code (online, players appear as they join).</summary>
        public void Configure(PlayerId id, bool local, Renderer bodyRenderer, GameObject swing)
        {
            playerId = id.Value;
            isLocal = local;
            body = bodyRenderer;
            swingMarker = swing;
        }

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            if (swingMarker != null)
            {
                _swingScale = swingMarker.transform.localScale;
                swingMarker.SetActive(false);
            }
            if (body != null)
            {
                _bodyScale = body.transform.localScale;
                _bodyPosition = body.transform.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null || !_runner.World.HasPlayer(PlayerId)) return;

            foreach (var e in _runner.FrameEvents)
            {
                switch (e)
                {
                    case AttackPerformed swing when swing.Player == PlayerId && (swing.Weapon == null || swing.Weapon.Effect == SkillEffectKind.Cone):
                        // The swing marker is for slashes; arrows, bolts and blasts are drawn by SkillEffectsView (ticket 46).
                        _swingUntil = Time.time + SwingShowSeconds;
                        if (swingMarker != null)
                            swingMarker.transform.localScale = swing.Kind == AttackKind.Heavy
                                ? Vector3.Scale(_swingScale, new Vector3(heavySwingScale, 1f, heavySwingScale))
                                : _swingScale;
                        break;
                    case PlayerDamaged hit when hit.Target == PlayerId:
                        _tint.Flash(hitFlashColor, HitFlashSeconds);
                        break;
                }
            }

            var state = _runner.World.GetPlayer(PlayerId);
            transform.position = _runner.GetRenderPosition(PlayerId);
            transform.rotation = Quaternion.LookRotation(new Vector3(state.Facing.X, 0f, state.Facing.Z), Vector3.up);
            if (swingMarker != null) swingMarker.SetActive(!state.IsDefeated && Time.time < _swingUntil);
            if (body != null) PoseBody(state);
            if (body != null) ShowGear(state);
            ShowMount(state.IsMounted && !state.IsDefeated);
        }

        /// <summary>A greybox mount under a rider (ticket 42): a long brown block, until there is a model.</summary>
        private void ShowMount(bool riding)
        {
            if (riding && _mount == null && body != null)
            {
                _mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _mount.name = "Mount";
                Destroy(_mount.GetComponent<Collider>());
                _mount.transform.SetParent(transform, false);
                _mount.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                _mount.transform.localScale = new Vector3(0.9f, 1.1f, 2.2f);
                var renderer = _mount.GetComponent<Renderer>();
                renderer.sharedMaterial = body.sharedMaterial;
                renderer.material.color = new Color(0.45f, 0.3f, 0.18f);
            }
            if (_mount != null) _mount.SetActive(riding);
            if (riding && body != null) body.transform.localPosition += Vector3.up * SaddleHeight;
        }

        /// <summary>Draws each worn piece on the body, swapping a look only when what is worn changes (ticket 34).</summary>
        private void ShowGear(PlayerState state)
        {
            for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
            {
                var worn = slot < state.Worn.Count ? state.Worn[slot] : null;
                if (worn?.Id == _shownGear[slot]) continue;
                if (_gearObjects[slot] != null) Destroy(_gearObjects[slot]);
                _gearObjects[slot] = null;
                _shownGear[slot] = worn?.Id;
                if (worn == null) continue;
                _gearLooks ??= GearLooks.Catalog(_runner.Content);
                _gearLooks.TryGetValue(worn.Id, out var prefab);
                _gearObjects[slot] = GearLooks.Wear(worn, prefab, body);
            }
        }

        private void PoseBody(PlayerState state)
        {
            var t = body.transform;
            var build = CharacterLooks.Scale(state.Look.Build); // ticket 28: slim, average or broad
            var bodyScale = Vector3.Scale(_bodyScale, build);
            var bodyPosition = new Vector3(_bodyPosition.x, _bodyPosition.y * build.y, _bodyPosition.z);
            if (state.IsDefeated)
            {
                t.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying on the floor
                t.localPosition = new Vector3(0f, bodyScale.x * 0.5f, 0f);
                t.localScale = bodyScale;
            }
            else
            {
                t.localRotation = Quaternion.identity;
                t.localPosition = state.IsDodging ? new Vector3(0f, bodyPosition.y * 0.55f, 0f) : bodyPosition;
                t.localScale = state.IsDodging ? Vector3.Scale(bodyScale, new Vector3(1.15f, 0.55f, 1.15f)) : bodyScale;
            }

            var own = CharacterLooks.Colour(state.Look); // the colour chosen in the creator (ticket 28)
            Color? bodyTint = own;
            if (state.IsWindingUp) bodyTint = windupColor;
            else if (state.ChargingSlot.HasValue) bodyTint = Color.Lerp(own, chargeColor, state.ChargeFraction);
            _tint.Apply(body, bodyTint);
        }
    }
}
