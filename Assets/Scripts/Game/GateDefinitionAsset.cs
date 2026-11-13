using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// A gate: the way into a dungeon, as an ordered chain of room pieces from the kit, entrance
    /// first. The simulation only ever sees the plain <see cref="GateDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Gate Definition", fileName = "NewGate")]
    public sealed class GateDefinitionAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Stable id used in saves and over the network. Never change it once players have it.")] private string id = "new-gate";
        [SerializeField] private string displayName = "New Gate";
        [SerializeField, TextArea(2, 6), Tooltip("What the gate board says about this gate, in the System's voice (ticket 20)")]
        private string intel = "[System intel for this gate: " + TextSceneAsset.Unwritten + "]";
        [SerializeField, Tooltip("In order from the entrance. The same piece may appear more than once.")] private RoomPieceAsset[] rooms = new RoomPieceAsset[0];
        [SerializeField, Min(0), Tooltip("Solo: revives at the latest checkpoint before the gate starts again")] private int soloRevives = 3;
        [SerializeField, Min(0.5f), Tooltip("How close a player must come to a checkpoint to reach it, in metres")] private float checkpointRadius = 2f;
        [SerializeField, Min(0f), Tooltip("Co-op: seconds after the whole party is down before everyone returns at the latest checkpoint")] private float wipeDelaySeconds = 3f;
        [SerializeField, Tooltip("The clear reward: each player draws one they do not have yet")] private ModifierDefinitionAsset[] rewardPool = new ModifierDefinitionAsset[0];

        public string Id => id;
        public string DisplayName => displayName;
        public string Intel => intel;
        public IReadOnlyList<RoomPieceAsset> Rooms => rooms;
        public IReadOnlyList<ModifierDefinitionAsset> RewardPool => rewardPool;

        public GateDefinition ToDefinition(Dictionary<ScriptableObject, EnemyDefinition> enemies)
        {
            var gate = new GateDefinition
            {
                Id = id, Name = displayName, SoloRevives = soloRevives, CheckpointRadius = checkpointRadius, WipeDelaySeconds = wipeDelaySeconds,
            };
            foreach (var room in rooms)
            {
                if (room == null) { Debug.LogWarning($"{name}: an empty entry in the rooms list was skipped.", this); continue; }
                gate.Rooms.Add(room.ToDefinition(enemies));
            }
            foreach (var modifier in rewardPool)
            {
                if (modifier == null) { Debug.LogWarning($"{name}: an empty entry in the reward pool was skipped.", this); continue; }
                gate.RewardPool.Add(modifier.ToDefinition());
            }
            return gate;
        }

        /// <summary>For editor tooling that creates gates in code.</summary>
        public void Set(string gateId, string gateName, RoomPieceAsset[] gateRooms, ModifierDefinitionAsset[] reward = null)
        {
            rewardPool = reward ?? new ModifierDefinitionAsset[0];
            id = gateId;
            displayName = gateName;
            rooms = gateRooms ?? new RoomPieceAsset[0];
        }
    }
}
