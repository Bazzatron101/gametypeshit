using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one enemy. Its starting transform is the spawn point; after that it
    /// only reads from the simulation. It glows while winding up an attack (the telegraph the
    /// player dodges on), flashes when hit, and hides its body when defeated.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float HitFlashSeconds = 0.1f;

        [SerializeField] private EnemyDefinitionAsset definition;
        [SerializeField, Tooltip("Hidden on defeat; flashed on hit; tinted during windup")] private Renderer body;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private Color windupColor = new Color(1f, 0.6f, 0.1f);

        private SimulationRunner _runner;
        private readonly BodyTint _tint = new BodyTint();
        private int _windupLandsAtTick;
        private bool _bound;

        public EnemyId Id { get; private set; }

        /// <summary>True once the body is hidden after defeat.</summary>
        public bool ShowsDefeated => body != null && !body.gameObject.activeSelf;

        public EnemyDefinition CreateDefinition() =>
            definition != null ? definition.ToDefinition() : new EnemyDefinition();

        public void Bind(EnemyId id)
        {
            Id = id;
            _bound = true;
        }

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (!_bound || _runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
            {
                switch (e)
                {
                    case DamageDealt hit when hit.Target == Id:
                        _tint.Flash(hitFlashColor, HitFlashSeconds);
                        break;
                    case EnemyAttackStarted windup when windup.Enemy == Id:
                        _windupLandsAtTick = windup.LandsAtTick;
                        break;
                    case EnemyDefeated defeated when defeated.Enemy == Id && body != null:
                        body.gameObject.SetActive(false);
                        break;
                }
            }

            transform.position = _runner.GetRenderPosition(Id);

            if (body == null) return;
            _tint.Apply(body, _runner.World.Tick < _windupLandsAtTick ? windupColor : (Color?)null);
        }
    }
}
