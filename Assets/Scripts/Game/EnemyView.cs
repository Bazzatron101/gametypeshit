using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one enemy. Its starting transform is the spawn point; after that it
    /// only reads from the simulation. It glows while winding up an attack (the telegraph the
    /// player dodges on), flashes and swells for a moment when hit, and hides its body when defeated.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float HitFlashSeconds = 0.15f;
        private const float HitPopSeconds = 0.15f;
        private const float HitPopScale = 1.25f; // the body swells for a moment when hit (ticket 47)

        [SerializeField] private EnemyDefinitionAsset definition;
        [SerializeField, Tooltip("Hidden on defeat; flashed on hit; tinted during windup")] private Renderer body;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private Color windupColor = new Color(1f, 0.6f, 0.1f);

        private SimulationRunner _runner;
        private readonly BodyTint _tint = new BodyTint();
        private int _windupLandsAtTick;
        private bool _bound;
        private Vector3 _bodyScale;
        private float _poppedAt = -1f;

        public EnemyId Id { get; private set; }

        /// <summary>True once the body is hidden after defeat.</summary>
        public bool ShowsDefeated => body != null && !body.gameObject.activeSelf;

        public EnemyDefinition CreateDefinition() =>
            definition != null ? definition.ToDefinition() : new EnemyDefinition();

        /// <summary>Sets what the view shows and spawns as, for views built in code (the test arena).</summary>
        public void Configure(EnemyDefinitionAsset definitionAsset, Renderer bodyRenderer)
        {
            definition = definitionAsset;
            body = bodyRenderer;
        }

        public void Bind(EnemyId id)
        {
            Id = id;
            _bound = true;
        }

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            if (body != null) _bodyScale = body.transform.localScale;
        }

        private void LateUpdate()
        {
            if (!_bound || _runner == null || _runner.World == null || !_runner.World.HasEnemy(Id)) return;

            foreach (var e in _runner.FrameEvents)
            {
                switch (e)
                {
                    case DamageDealt hit when hit.Target == Id:
                        _tint.Flash(hitFlashColor, HitFlashSeconds);
                        _poppedAt = Time.time;
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
            if (_poppedAt >= 0f)
            {
                var t = (Time.time - _poppedAt) / HitPopSeconds;
                body.transform.localScale = _bodyScale * (t >= 1f ? 1f : Mathf.Lerp(HitPopScale, 1f, t));
                if (t >= 1f) _poppedAt = -1f;
            }
            _tint.Apply(body, _runner.World.Tick < _windupLandsAtTick ? windupColor : (Color?)null);
        }
    }
}
