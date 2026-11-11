using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Presentation for one enemy. Its starting transform is the spawn point; after that it
    /// only reads from the simulation. It flashes when hit and hides its body when defeated.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float HitFlashSeconds = 0.1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private EnemyDefinitionAsset definition;
        [SerializeField, Tooltip("Hidden on defeat; flashed on hit")] private Renderer body;
        [SerializeField] private Color hitFlashColor = Color.white;

        private SimulationRunner _runner;
        private MaterialPropertyBlock _props;
        private float _flashUntil;
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

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _props = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (!_bound || _runner == null || _runner.World == null) return;

            foreach (var e in _runner.FrameEvents)
            {
                if (e is DamageDealt hit && hit.Target == Id) _flashUntil = Time.time + HitFlashSeconds;
                else if (e is EnemyDefeated defeated && defeated.Enemy == Id && body != null) body.gameObject.SetActive(false);
            }

            transform.position = _runner.GetRenderPosition(Id);

            if (body != null)
            {
                if (Time.time < _flashUntil) _props.SetColor(BaseColorId, hitFlashColor);
                else _props.Clear();
                body.SetPropertyBlock(_props);
            }
        }
    }
}
