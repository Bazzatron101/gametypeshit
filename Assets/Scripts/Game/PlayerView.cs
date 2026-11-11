using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one player. Its starting transform is the spawn point; after that it
    /// only reads state from the simulation and never writes to it. Place it at the feet;
    /// child visuals (the capsule) sit above.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Tooltip("Driven by this machine's keyboard/gamepad.")] private bool isLocal = true;

        private SimulationRunner _runner;

        public PlayerId PlayerId => new PlayerId(playerId);
        public bool IsLocal => isLocal;

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_runner != null && _runner.World != null) transform.position = _runner.GetRenderPosition(PlayerId);
        }
    }
}
