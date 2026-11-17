using System;
using System.Collections.Generic;
using System.IO;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Raid gates (ticket 29, ADR 0023): the boss fight of a gate with a <see cref="RaidDefinition"/> runs in
    /// phases. Shielded until every crystal is broken together, then open, then a final phase of room-wide
    /// blasts that only the shelter is safe from. The raid lives in the boss's room, so a wipe or a solo fall
    /// that resets the room resets the raid with it, and the clear ends it.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private sealed class RaidRun
        {
            public RaidDefinition Definition;
            public int Room;
            public RaidState View;
            public RaidPhase Phase;

            /// <summary>The shielded boss, by id; 0 before the room fills.</summary>
            public int BossId;

            /// <summary>In place order. Emptied when the shield falls.</summary>
            public readonly List<EnemyId> Crystals = new List<EnemyId>();

            /// <summary>Once a crystal is broken: the tick the broken ones grow back. 0 while none is broken.</summary>
            public int RegrowAtTick;

            /// <summary>Final phase: the tick the next blast may start (it waits for the boss to finish an attack).</summary>
            public int NextShelterAtTick;

            /// <summary>The tick the blast being wound up lands; 0 when none is.</summary>
            public int ShelterLandsAtTick;

            /// <summary>How many blasts have been wound up this fight: picks the next shelter's place.</summary>
            public int Shelters;

            public Vec3 ShelterAt;

            public void Reset()
            {
                Phase = RaidPhase.Waiting;
                BossId = 0;
                Crystals.Clear();
                RegrowAtTick = 0;
                NextShelterAtTick = 0;
                ShelterLandsAtTick = 0;
                Shelters = 0;
                ShelterAt = Vec3.Zero;
            }
        }

        private RaidRun NewRaid(GateDefinition definition)
        {
            if (definition.Raid == null) return null;
            if (definition.Raid.Crystal == null) throw new ArgumentException($"Raid gate '{definition.Id}' has no crystal.", nameof(definition));
            if (definition.Raid.CrystalPlaces.Count == 0) throw new ArgumentException($"Raid gate '{definition.Id}' has no crystal places.", nameof(definition));
            var room = definition.Rooms.FindIndex(r => r.Spawns.Exists(s => s.Enemy.Boss != null));
            if (room < 0) throw new ArgumentException($"Raid gate '{definition.Id}' has no boss room.", nameof(definition));
            var raid = new RaidRun { Definition = definition.Raid, Room = room };
            raid.View = new RaidState(this, definition.Raid, room);
            return raid;
        }

        // --- What RaidState reads -------------------------------------------------------------------

        internal RaidState GateRaid() => _gate?.Raid?.View;
        internal RaidPhase RaidPhaseNow() => _gate?.Raid?.Phase ?? RaidPhase.Waiting;
        internal IReadOnlyList<EnemyId> RaidCrystals() => (IReadOnlyList<EnemyId>)_gate?.Raid?.Crystals ?? Array.Empty<EnemyId>();
        internal bool IsRaidCrystal(EnemyId id) => _gate?.Raid != null && _gate.Raid.Crystals.Contains(id);
        internal float? RaidSecondsUntilRegrow() => _gate?.Raid == null || _gate.Raid.RegrowAtTick == 0 ? (float?)null : Math.Max(0, _gate.Raid.RegrowAtTick - Tick) * TickDelta;
        internal Vec3? RaidShelter() => _gate?.Raid == null || _gate.Raid.ShelterLandsAtTick == 0 ? (Vec3?)null : _gate.Raid.ShelterAt;
        internal int RaidShelterLandsAtTick() => _gate?.Raid?.ShelterLandsAtTick ?? 0;

        // --- Rules the rest of the world asks about --------------------------------------------------

        /// <summary>A crystal stands still and never attacks.</summary>
        private bool IsCrystal(Enemy enemy) => _gate?.Raid != null && _gate.Raid.Crystals.Contains(enemy.Id);

        /// <summary>The raid's boss while its crystals stand: no damage gets through.</summary>
        private bool IsShielded(Enemy enemy) => _gate?.Raid != null && _gate.Raid.Phase == RaidPhase.Shielded && _gate.Raid.BossId == enemy.Id.Value;

        /// <summary>The raid's boss while it winds up a blast: it stands and channels, attacking no one.</summary>
        private bool IsChanneling(Enemy boss) => _gate?.Raid != null && _gate.Raid.ShelterLandsAtTick != 0 && _gate.Raid.BossId == boss.Id.Value;

        // --- Each tick ----------------------------------------------------------------------------

        /// <summary>The boss's room has just filled: the crystals rise, scaled like the room's enemies, and the boss is shielded.</summary>
        private void StartRaid(int tick, PartyTier tier)
        {
            var raid = _gate.Raid;
            var floor = _gate.Layout.Rooms[raid.Room];
            var centre = floor.Centre;
            var count = raid.Definition.CrystalsFor(PresentCount());
            raid.Reset();
            foreach (var id in _gate.RoomEnemies[raid.Room])
                if (_enemiesById.TryGetValue(id, out var enemy) && enemy.Definition.Boss != null) raid.BossId = id.Value;
            for (var i = 0; i < count; i++)
            {
                var crystal = SpawnEnemy(raid.Definition.Crystal, floor.Nearest(centre + raid.Definition.CrystalPlaces[i]), tier);
                raid.Crystals.Add(crystal);
                _gate.RoomEnemies[raid.Room].Add(crystal); // the room is not cleared without them, and a wipe takes them away
            }
            raid.Phase = RaidPhase.Shielded;
            _events.Add(new RaidShieldRaised(tick, new EnemyId(raid.BossId), count));
        }

        /// <summary>After the rooms: crystals broken or grown back, the shield, the final phase and its blasts.</summary>
        private void UpdateRaid(int tick)
        {
            var raid = _gate.Raid;
            if (raid == null || raid.Phase == RaidPhase.Waiting || _gate.ClearAnnounced) return;
            if (!_enemiesById.TryGetValue(new EnemyId(raid.BossId), out var boss) || boss.Health <= 0) return;

            switch (raid.Phase)
            {
                case RaidPhase.Shielded:
                    UpdateCrystals(raid, boss, tick);
                    break;
                case RaidPhase.Open:
                    if ((long)boss.Health * 100 > (long)boss.MaxHealth * raid.Definition.ShelterAtHealthPercent) break;
                    raid.Phase = RaidPhase.Final;
                    raid.NextShelterAtTick = tick + SecondsToTicks(raid.Definition.FirstShelterAfterSeconds);
                    _events.Add(new RaidFinalPhase(tick, boss.Id));
                    break;
                case RaidPhase.Final:
                    if (raid.ShelterLandsAtTick != 0)
                    {
                        if (tick >= raid.ShelterLandsAtTick) LandBlast(raid, boss, tick);
                    }
                    else if (tick >= raid.NextShelterAtTick && boss.AttackLandsAtTick == 0 && !boss.Charging)
                    {
                        MarkShelter(raid, boss, tick); // it finishes the attack it is on first
                    }
                    break;
            }
        }

        private void UpdateCrystals(RaidRun raid, Enemy boss, int tick)
        {
            var broken = 0;
            foreach (var id in raid.Crystals) if (IsGone(id)) broken++;

            if (broken == raid.Crystals.Count)
            {
                // All of them, together: the shield falls. The shards are gone at once (nothing more to show).
                foreach (var id in raid.Crystals)
                {
                    if (HasEnemy(id)) DespawnEnemy(id);
                    _gate.RoomEnemies[raid.Room].Remove(id);
                }
                raid.Crystals.Clear();
                raid.RegrowAtTick = 0;
                raid.Phase = RaidPhase.Open;
                _events.Add(new RaidShieldBroken(tick, boss.Id));
                return;
            }

            if (broken == 0)
            {
                raid.RegrowAtTick = 0;
                return;
            }
            if (raid.RegrowAtTick == 0)
            {
                raid.RegrowAtTick = tick + Math.Max(1, SecondsToTicks(raid.Definition.CrystalRegrowSeconds)); // the first one just broke
                return;
            }
            if (tick < raid.RegrowAtTick) return;

            // Too slow: the broken ones grow back whole.
            var regrown = 0;
            foreach (var id in raid.Crystals)
            {
                if (!_enemiesById.TryGetValue(id, out var crystal) || crystal.Health > 0) continue;
                crystal.Health = crystal.MaxHealth;
                regrown++;
            }
            raid.RegrowAtTick = 0;
            _events.Add(new RaidCrystalsRegrew(tick, regrown));
        }

        private void MarkShelter(RaidRun raid, Enemy boss, int tick)
        {
            var floor = _gate.Layout.Rooms[raid.Room];
            var places = raid.Definition.ShelterPlaces;
            var offset = places.Count == 0 ? Vec3.Zero : places[raid.Shelters % places.Count];
            raid.ShelterAt = floor.Nearest(floor.Centre + offset);
            raid.ShelterLandsAtTick = tick + Math.Max(1, SecondsToTicks(raid.Definition.ShelterWindupSeconds));
            raid.Shelters++;
            _events.Add(new RaidShelterMarked(tick, boss.Id, raid.ShelterAt, raid.Definition.ShelterRadius, raid.ShelterLandsAtTick));
        }

        /// <summary>The blast fills the room. Everyone present and standing outside the shelter is hit; nothing else saves them.</summary>
        private void LandBlast(RaidRun raid, Enemy boss, int tick)
        {
            raid.ShelterLandsAtTick = 0;
            raid.NextShelterAtTick = tick + SecondsToTicks(raid.Definition.ShelterEverySeconds);
            var caught = 0;
            foreach (var player in _players)
            {
                if (!player.IsAlive || player.Away || raid.Definition.InShelter(raid.ShelterAt, player.Position)) continue;
                caught++;
                var amount = player.TakeDamage(Math.Max(1, PartyScalingDefinition.Scale(player.MaxHealth, raid.Definition.ShelterDamagePercent)), out var defeated);
                if (amount == 0) continue;
                Dismount(player, tick);
                _events.Add(new PlayerDamaged(tick, boss.Id, player.Id, amount, player.Health));
                if (defeated) _events.Add(new PlayerDefeated(tick, player.Id, boss.Id));
            }
            _events.Add(new RaidBlastLanded(tick, boss.Id, caught));
        }

        /// <summary>The gate is cleared: no blast is left hanging over the room.</summary>
        private void EndRaid()
        {
            var raid = _gate.Raid;
            if (raid == null) return;
            raid.ShelterLandsAtTick = 0;
            raid.RegrowAtTick = 0;
            raid.Crystals.Clear();
        }

        // --- Snapshots ----------------------------------------------------------------------------

        private static void WriteRaid(BinaryWriter w, RaidRun raid)
        {
            w.Write(raid != null);
            if (raid == null) return;
            w.Write((byte)raid.Phase);
            w.Write(raid.BossId);
            w.Write(raid.Crystals.Count);
            foreach (var id in raid.Crystals) w.Write(id.Value); // in place order, not sorted
            w.Write(raid.RegrowAtTick);
            w.Write(raid.NextShelterAtTick);
            w.Write(raid.ShelterLandsAtTick);
            w.Write(raid.Shelters);
            WriteVec(w, raid.ShelterAt);
        }

        private void ReadRaid(BinaryReader r, GateRun run)
        {
            var hasRaid = r.ReadBoolean();
            if (hasRaid != (run.Definition.Raid != null)) throw new FormatException("The gate is a raid on one side only.");
            if (!hasRaid) return;
            var raid = NewRaid(run.Definition);
            var phase = r.ReadByte();
            if (phase > (byte)RaidPhase.Final) throw new FormatException("Unknown raid phase.");
            raid.Phase = (RaidPhase)phase;
            raid.BossId = r.ReadInt32();
            for (int i = 0, n = ReadCount(r); i < n; i++) raid.Crystals.Add(new EnemyId(r.ReadInt32()));
            raid.RegrowAtTick = r.ReadInt32();
            raid.NextShelterAtTick = r.ReadInt32();
            raid.ShelterLandsAtTick = r.ReadInt32();
            raid.Shelters = r.ReadInt32();
            raid.ShelterAt = ReadVec(r);
            run.Raid = raid;
        }
    }
}
