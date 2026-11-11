using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one player. Its starting transform is the spawn point; after that it
    /// only reads state from the simulation and never writes to it. Place it at the feet;
    /// child visuals (the capsule) sit above. It turns to face the simulation's facing and
    /// briefly shows a swing marker on each light attack.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float SwingShowSeconds = 0.12f;

        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Tooltip("Driven by this machine's keyboard/gamepad.")] private bool isLocal = true;
        [SerializeField, Tooltip("Shown briefly when this player attacks")] private GameObject swingMarker;

        private SimulationRunner _runner;
        private float _swingUntil;

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool IsLocal => isLocal;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            if (swingMarker != null) swingMarker.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
                if (e is AttackPerformed swing && swing.Player == PlayerId) _swingUntil = Time.time + SwingShowSeconds;

            var state = _runner.World.GetPlayer(PlayerId);
            transform.position = _runner.GetRenderPosition(PlayerId);
            transform.rotation = Quaternion.LookRotation(new Vector3(state.Facing.X, 0f, state.Facing.Z), Vector3.up);
            if (swingMarker != null) swingMarker.SetActive(Time.time < _swingUntil);
        }
    }
}
