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
        /// <summary>A raid's boss fight (ticket 29, ADR 0023): crystals that shield the boss, then a final phase of blasts only a shelter is safe from.</summary>
        [Serializable]
        public struct RaidSettings
        {
            [Tooltip("Makes this gate a raid: its boss room runs the phases below")] public bool isRaid;
            [Tooltip("What a crystal is (its health; it never moves or attacks). Must also be in the game content's enemies.")] public EnemyDefinitionAsset crystal;
            [Tooltip("Where crystals stand, from the boss room's centre, in the order they are used")] public Vector3[] crystalPlaces;
            [Min(1), Tooltip("One crystal for every this many players present (rounded up)")] public int playersPerCrystal;
            [Min(0.5f), Tooltip("Once the first crystal breaks, how long the rest have before the broken ones grow back")] public float crystalRegrowSeconds;
            [Range(1, 99), Tooltip("The final phase starts at this percentage of the boss's health")] public int shelterAtHealthPercent;
            [Min(0f), Tooltip("The first blast starts this long after the final phase begins")] public float firstShelterAfterSeconds;
            [Min(1f), Tooltip("From one blast landing to the next one starting")] public float shelterEverySeconds;
            [Min(0.5f), Tooltip("From the shelter being marked to the blast")] public float shelterWindupSeconds;
            [Min(1f), Tooltip("The shelter's radius: it must hold the whole party")] public float shelterRadius;
            [Range(1, 100), Tooltip("Outside the shelter, a player loses this percentage of their maximum health")] public int shelterDamagePercent;
            [Tooltip("Where the shelter is marked, from the boss room's centre, in turn")] public Vector3[] shelterPlaces;
        }

        [SerializeField, Tooltip("Stable id used in saves and over the network. Never change it once players have it.")] private string id = "new-gate";
        [SerializeField] private string displayName = "New Gate";
        [SerializeField, Tooltip("The guild rank this gate is licensed at (\"E\" to \"S\"): the board offers it once the host's world has that rank (ticket 25). Empty for any rank.")]
        private string rank = "";
        [SerializeField, TextArea(2, 6), Tooltip("What the gate board says about this gate, written as the guild's notice (ticket 20)")]
        private string intel = "[Guild notice for this gate: " + TextSceneAsset.Unwritten + "]";
        [SerializeField, Tooltip("In order from the entrance. The same piece may appear more than once.")] private RoomPieceAsset[] rooms = new RoomPieceAsset[0];
        [SerializeField, Min(0), Tooltip("Solo: revives at the latest checkpoint before the gate starts again")] private int soloRevives = 3;
        [SerializeField, Min(0.5f), Tooltip("How close a player must come to a checkpoint to reach it, in metres")] private float checkpointRadius = 2f;
        [SerializeField, Min(0f), Tooltip("Co-op: seconds after the whole party is down before everyone returns at the latest checkpoint")] private float wipeDelaySeconds = 3f;
        [SerializeField, Tooltip("The clear reward: each player draws one they do not have yet")] private ModifierDefinitionAsset[] rewardPool = new ModifierDefinitionAsset[0];
        [SerializeField, Tooltip("Gear in the same reward pool (ticket 23). Each piece must also be in the game content's gear list.")] private GearDefinitionAsset[] gearPool = new GearDefinitionAsset[0];
        [SerializeField, Tooltip("Leave off for a normal gate")] private RaidSettings raid;

        public string Id => id;
        public string DisplayName => displayName;
        public string Rank => rank;
        public string Intel => intel;
        public IReadOnlyList<RoomPieceAsset> Rooms => rooms;
        public IReadOnlyList<ModifierDefinitionAsset> RewardPool => rewardPool;
        public IReadOnlyList<GearDefinitionAsset> GearPool => gearPool;
        public bool IsRaid => raid.isRaid;
        public RaidSettings Raid => raid;

        /// <param name="gear">Gear already made for this config, so the pool and the gear list share definitions.</param>
        public GateDefinition ToDefinition(Dictionary<ScriptableObject, EnemyDefinition> enemies, Dictionary<GearDefinitionAsset, GearDefinition> gear = null)
        {
            var gate = new GateDefinition
            {
                Id = id, Name = displayName, Rank = string.IsNullOrEmpty(rank) ? null : rank, SoloRevives = soloRevives, CheckpointRadius = checkpointRadius, WipeDelaySeconds = wipeDelaySeconds,
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
            foreach (var piece in gearPool ?? new GearDefinitionAsset[0])
            {
                if (piece == null) { Debug.LogWarning($"{name}: an empty entry in the gear pool was skipped.", this); continue; }
                gate.RewardPool.Add(piece.ToDefinition(gear));
            }
            if (raid.isRaid) gate.Raid = RaidDefinition(enemies);
            return gate;
        }

        private RaidDefinition RaidDefinition(Dictionary<ScriptableObject, EnemyDefinition> enemies)
        {
            var definition = new RaidDefinition
            {
                Crystal = raid.crystal != null ? RoomPieceAsset.Shared(enemies, raid.crystal) : null,
                PlayersPerCrystal = raid.playersPerCrystal, CrystalRegrowSeconds = raid.crystalRegrowSeconds,
                ShelterAtHealthPercent = raid.shelterAtHealthPercent, FirstShelterAfterSeconds = raid.firstShelterAfterSeconds,
                ShelterEverySeconds = raid.shelterEverySeconds, ShelterWindupSeconds = raid.shelterWindupSeconds,
                ShelterRadius = raid.shelterRadius, ShelterDamagePercent = raid.shelterDamagePercent,
            };
            foreach (var p in raid.crystalPlaces ?? new Vector3[0]) definition.CrystalPlaces.Add(new Vec3(p.x, 0f, p.z));
            foreach (var p in raid.shelterPlaces ?? new Vector3[0]) definition.ShelterPlaces.Add(new Vec3(p.x, 0f, p.z));
            return definition;
        }

        /// <summary>For editor tooling: makes this gate a raid.</summary>
        public void SetRaid(RaidSettings settings) => raid = settings;

        /// <summary>For editor tooling: the gate's licence rank (ticket 25) and its board notice.</summary>
        public void SetRankAndIntel(string gateRank, string notice)
        {
            rank = gateRank ?? "";
            intel = notice ?? "";
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
