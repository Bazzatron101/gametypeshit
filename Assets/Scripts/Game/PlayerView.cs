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
        private GameObject _speciesParts;
        private string _shownSpecies;
        private System.Collections.Generic.Dictionary<string, GameObject> _gearLooks;
        private readonly string[] _shownGear = new string[GearDefinition.SlotCount]; // by id: a new world (a rejoin) has new definitions of the same pieces
        private readonly GameObject[] _gearObjects = new GameObject[GearDefinition.SlotCount];

        /// <summary>How much higher a rider sits (ticket 42).</summary>
        private const float BicycleSeatHeight = 0.45f;

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
            if (body != null) ShowSpecies(state);
            if (body != null) ShowGear(state);
            ShowMount(state.IsMounted && !state.IsDefeated, state.Travel);
        }

        private TravelStyle? _mountStyle;

        /// <summary>
        /// How a mounted hunter is drawn (tickets 42 and 90), in greybox until there are models: a bicycle under a
        /// slow species; a fast one leans into a run on its own legs; a flyer lies level in the air.
        /// </summary>
        private void ShowMount(bool riding, TravelStyle style)
        {
            if (body == null) return;
            if (_mount != null && _mountStyle != style)
            {
                Destroy(_mount);
                _mount = null;
            }
            if (riding && _mount == null && style == TravelStyle.Bicycle)
            {
                _mount = TravelLooks.Bicycle(transform, body.sharedMaterial);
                _mountStyle = style;
            }
            if (_mount != null) _mount.SetActive(riding);
            if (!riding) return;
            var t = body.transform;
            switch (style)
            {
                case TravelStyle.Bicycle:
                    t.localPosition += Vector3.up * BicycleSeatHeight;
                    t.localRotation = Quaternion.Euler(15f, 0f, 0f) * t.localRotation; // leaning to the handlebars
                    break;
                case TravelStyle.Run:
                    t.localRotation = Quaternion.Euler(25f, 0f, 0f) * t.localRotation; // bounding or galloping
                    t.localPosition += Vector3.up * (0.15f * Mathf.Abs(Mathf.Sin(Time.time * 9f))); // a bound in each stride
                    break;
                case TravelStyle.Fly:
                    t.localRotation = Quaternion.Euler(70f, 0f, 0f) * t.localRotation; // body level, wings out
                    t.localPosition += Vector3.up * (0.1f * Mathf.Sin(Time.time * 3f)); // a gentle bob
                    break;
            }
        }

        /// <summary>The species' ears, snout or beak and tail (ticket 81), rebuilt only when the species changes.</summary>
        private void ShowSpecies(PlayerState state)
        {
            var id = state.Species?.Id;
            if (id == _shownSpecies) return;
            if (_speciesParts != null) Destroy(_speciesParts);
            _shownSpecies = id;
            _speciesParts = SpeciesLooks.Build(id, body);
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
            var build = Vector3.Scale(CharacterLooks.Scale(state.Look.Build), SpeciesLooks.Proportions(state.Species?.Id)); // ticket 28: slim, average or broad; ticket 81: the species' shape
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
