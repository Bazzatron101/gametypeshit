using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Online, players and enemies come and go (joins, rejoins, spawns, snapshots), so their views
    /// are made in code to match whatever the world holds: one per player id and one per enemy id.
    /// The local player's view gets the camera. A new world (a rejoin) starts the views afresh.
    /// </summary>
    public sealed class OnlineViews : MonoBehaviour
    {
        [SerializeField] private Material localPlayerMaterial;
        [SerializeField] private Material remotePlayerMaterial;
        [SerializeField] private Material swingMaterial;
        [SerializeField] private Material enemyMaterial;
        [SerializeField] private Material bossMaterial;

        /// <summary>A boss is drawn this much larger than an ordinary enemy, so it reads as the fight's centre.</summary>
        private const float BossScale = 2.2f;

        private SimulationRunner _runner;
        private ThirdPersonCamera _camera;
        private SimulationWorld _builtFor;
        private readonly Dictionary<PlayerId, PlayerView> _players = new Dictionary<PlayerId, PlayerView>();
        private readonly Dictionary<EnemyId, EnemyView> _enemies = new Dictionary<EnemyId, EnemyView>();
        private readonly List<PlayerId> _gonePlayers = new List<PlayerId>();
        private readonly List<EnemyId> _goneEnemies = new List<EnemyId>();

        public int PlayerViewCount => _players.Count;
        public int EnemyViewCount => _enemies.Count;

        private void Start()
        {
            _runner = FindAnyObjectByType<SimulationRunner>();
            _camera = FindAnyObjectByType<ThirdPersonCamera>();
        }

        // Before the views' own LateUpdate, so a new view draws on its first frame.
        private void Update()
        {
            if (_runner == null || !_runner.IsOnline || _runner.World == null) return;
            var world = _runner.World;
            if (world != _builtFor)
            {
                Clear();
                _builtFor = world;
            }

            foreach (var id in world.PlayerIds)
            {
                if (_players.ContainsKey(id)) continue;
                var local = id == _runner.LocalPlayerId;
                var view = GreyboxVisuals.CreatePlayer(local ? $"Player {id.Value} (you)" : $"Player {id.Value}", id, local,
                    local ? localPlayerMaterial : remotePlayerMaterial, swingMaterial);
                view.transform.position = _runner.GetRenderPosition(id);
                _players.Add(id, view);
                if (local && _camera != null) _camera.SetTarget(view.transform);
            }

            foreach (var id in world.EnemyIds)
            {
                if (_enemies.ContainsKey(id)) continue;
                var state = world.GetEnemy(id);
                var view = GreyboxVisuals.CreateEnemy(state.Definition.Name, _runner.GetRenderPosition(id), null,
                    state.IsBoss && bossMaterial != null ? bossMaterial : enemyMaterial);
                if (state.IsBoss) view.transform.localScale = Vector3.one * BossScale;
                view.Bind(id);
                _enemies.Add(id, view);
            }

            _gonePlayers.Clear();
            foreach (var id in _players.Keys) if (!world.HasPlayer(id)) _gonePlayers.Add(id);
            foreach (var id in _gonePlayers)
            {
                Destroy(_players[id].gameObject);
                _players.Remove(id);
            }

            _goneEnemies.Clear();
            foreach (var id in _enemies.Keys) if (!world.HasEnemy(id)) _goneEnemies.Add(id);
            foreach (var id in _goneEnemies)
            {
                Destroy(_enemies[id].gameObject);
                _enemies.Remove(id);
            }
        }

        private void Clear()
        {
            foreach (var view in _players.Values) Destroy(view.gameObject);
            foreach (var view in _enemies.Values) Destroy(view.gameObject);
            _players.Clear();
            _enemies.Clear();
        }
    }
}
