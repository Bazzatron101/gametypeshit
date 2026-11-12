using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one player. Its starting transform is the spawn point; after that it
    /// only reads state from the simulation and never writes to it. Place it at the feet;
    /// child visuals (the capsule) sit above. It turns to the simulation's facing, shows a swing
    /// marker on each light attack, flashes when hit, squashes while dodging and lies down when defeated.
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

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool IsLocal => isLocal;

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
            if (_runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
            {
                switch (e)
                {
                    case AttackPerformed swing when swing.Player == PlayerId:
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
        }

        private void PoseBody(PlayerState state)
        {
            var t = body.transform;
            if (state.IsDefeated)
            {
                t.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying on the floor
                t.localPosition = new Vector3(0f, _bodyScale.x * 0.5f, 0f);
                t.localScale = _bodyScale;
            }
            else
            {
                t.localRotation = Quaternion.identity;
                t.localPosition = state.IsDodging ? new Vector3(0f, _bodyPosition.y * 0.55f, 0f) : _bodyPosition;
                t.localScale = state.IsDodging ? Vector3.Scale(_bodyScale, new Vector3(1.15f, 0.55f, 1.15f)) : _bodyScale;
            }

            Color? bodyTint = null;
            if (state.IsWindingUp) bodyTint = windupColor;
            else if (state.ChargingSlot.HasValue) bodyTint = Color.Lerp(Color.white, chargeColor, state.ChargeFraction);
            _tint.Apply(body, bodyTint);
        }
    }
}
