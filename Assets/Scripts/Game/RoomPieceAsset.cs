using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// One piece of the room kit (ticket 17): a rectangular room with the enemies that wait in it
    /// and, optionally, a checkpoint. Pieces are reused across gates; a gate is a list of them.
    /// The simulation only ever sees the plain <see cref="RoomDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Room Piece", fileName = "NewRoom")]
    public sealed class RoomPieceAsset : ScriptableObject
    {
        [Serializable]
        public struct Spawn
        {
            public EnemyDefinitionAsset enemy;
            [Tooltip("From the room's centre, in metres. +Z is further into the gate.")] public Vector3 offset;
        }

        [SerializeField, Min(1f), Tooltip("Across the gate's direction of travel, in metres")] private float width = 10f;
        [SerializeField, Min(1f), Tooltip("Along the gate's direction of travel, in metres")] private float length = 10f;
        [SerializeField, Tooltip("Enemies that appear when the party reaches this room")] private Spawn[] spawns = new Spawn[0];
        [SerializeField] private bool hasCheckpoint;
        [SerializeField, Tooltip("From the room's centre, in metres")] private Vector3 checkpointOffset;
        [SerializeField, Tooltip("A boss waiting in this room (a gate with a boss is cleared by defeating it)")] private BossDefinitionAsset boss;
        [SerializeField, Tooltip("Where the boss waits, from the room's centre")] private Vector3 bossOffset;

        public float Width => width;
        public float Length => length;
        public IReadOnlyList<Spawn> Spawns => spawns;
        public bool HasCheckpoint => hasCheckpoint;
        public Vector3 CheckpointOffset => checkpointOffset;
        public BossDefinitionAsset Boss => boss;
        public Vector3 BossOffset => bossOffset;

        /// <param name="enemies">
        /// One simulation definition per enemy asset, shared by every room that uses it: snapshots
        /// write each distinct definition once, so sharing keeps them small.
        /// </param>
        public RoomDefinition ToDefinition(Dictionary<ScriptableObject, EnemyDefinition> enemies)
        {
            var room = new RoomDefinition
            {
                Width = width,
                Length = length,
                HasCheckpoint = hasCheckpoint,
                CheckpointOffset = new Vec3(checkpointOffset.x, 0f, checkpointOffset.z),
            };
            foreach (var spawn in spawns)
            {
                if (spawn.enemy == null) { Debug.LogWarning($"{name}: a spawn with no enemy was skipped.", this); continue; }
                room.Spawns.Add(new RoomSpawn(Shared(enemies, spawn.enemy), new Vec3(spawn.offset.x, 0f, spawn.offset.z)));
            }
            if (boss != null)
            {
                if (!enemies.TryGetValue(boss, out var bossDefinition)) enemies[boss] = bossDefinition = boss.ToDefinition(enemies);
                room.Spawns.Add(new RoomSpawn(bossDefinition, new Vec3(bossOffset.x, 0f, bossOffset.z)));
            }
            return room;
        }

        /// <summary>The one simulation definition for an enemy asset, made on first use.</summary>
        internal static EnemyDefinition Shared(Dictionary<ScriptableObject, EnemyDefinition> enemies, EnemyDefinitionAsset asset)
        {
            if (!enemies.TryGetValue(asset, out var definition)) enemies[asset] = definition = asset.ToDefinition();
            return definition;
        }

        /// <summary>For editor tooling that creates pieces in code.</summary>
        public void Set(float roomWidth, float roomLength, Spawn[] roomSpawns, bool checkpoint = false, Vector3 checkpointAt = default,
            BossDefinitionAsset roomBoss = null, Vector3 bossAt = default)
        {
            boss = roomBoss;
            bossOffset = bossAt;
            width = roomWidth;
            length = roomLength;
            spawns = roomSpawns ?? new Spawn[0];
            hasCheckpoint = checkpoint;
            checkpointOffset = checkpointAt;
        }
    }
}
