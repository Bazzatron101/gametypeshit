using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The overworld (ADR 0015, ticket 38): the third kind of place, beside the hub and the gates. In a
    /// game with one, the guild town is part of it: outside a gate the party roams the world freely
    /// and apart, the board and the ready check work anywhere, and walking near a hidden gate finds
    /// it. The host takes the party into a gate from its entrance (everyone ready, wherever they
    /// stand), or from the board for a found gate in an open region; after the run the party comes
    /// back out at that gate's entrance. Found gates and open regions belong to the host's world.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private const float ComingOutRadius = 1.5f;

        private readonly List<string> _foundGates = new List<string>();
        private readonly List<string> _openRegions = new List<string>();

        public OverworldDefinition Overworld => _config.Overworld;

        /// <summary>True while the party is out in the world: the game has an overworld and no gate is running.</summary>
        public bool InOverworld => _config.Overworld != null && _gate == null;

        /// <summary>Gates the host's world has found by walking to them, oldest first. Gates known from the start are not listed.</summary>
        public IReadOnlyList<string> FoundGates => _foundGates;

        /// <summary>Regions the host's world has opened (by rank promotions, ticket 25, or story), beyond those open from the start.</summary>
        public IReadOnlyList<string> OpenRegions => _openRegions;

        /// <summary>Host only: the gates the host's world has already found (from its save), replacing any here.</summary>
        public void SetFoundGates(IEnumerable<string> gateIds) => Replace(_foundGates, gateIds);

        /// <summary>Host only: the regions the host's world has opened (from its save), replacing any here.</summary>
        public void SetOpenRegions(IEnumerable<string> regions) => Replace(_openRegions, regions);

        private static void Replace(List<string> list, IEnumerable<string> ids)
        {
            list.Clear();
            foreach (var id in ids ?? Enumerable.Empty<string>())
                if (id != null && !list.Contains(id)) list.Add(id);
        }

        /// <summary>Whether the party knows where <paramref name="gateId"/> is: found, known from the start, or not on the map at all.</summary>
        public bool IsGateFound(string gateId)
        {
            var site = _config.Overworld?.SiteOf(gateId);
            return site == null || site.KnownFromStart || _foundGates.Contains(gateId);
        }

        /// <summary>Whether the guild gives work in <paramref name="region"/>: open from the start, or opened since.</summary>
        public bool IsRegionOpen(string region)
        {
            var overworld = _config.Overworld;
            return overworld == null || region == null || overworld.StartingRegions.Contains(region) || _openRegions.Contains(region);
        }

        /// <summary>
        /// Whether the guild licenses <paramref name="gateId"/> for this world: it stands in an open region (a gate not on
        /// the map counts as in one), and the world has reached the gate's rank (ticket 25).
        /// </summary>
        public bool IsGateLicensed(string gateId) =>
            IsRegionOpen(_config.Overworld?.SiteOf(gateId)?.Region) && HasReachedRank(GateRankOf(gateId));

        private string GateRankOf(string gateId)
        {
            foreach (var gate in _config.Gates ?? Array.Empty<GateDefinition>())
                if (gate != null && gate.Id == gateId) return gate.Rank;
            return null;
        }

        /// <summary>Whether the board offers <paramref name="gateId"/>: found, and licensed (ADR 0015: the board saves the trip).</summary>
        public bool IsOnBoard(string gateId) => IsGateFound(gateId) && IsGateLicensed(gateId);

        /// <summary>The gate whose entrance <paramref name="player"/> stands at, out in the world; null when there is none.</summary>
        public GateSite GateEntranceNear(PlayerId player)
        {
            if (!InOverworld) return null;
            var at = FindPlayer(player).Position;
            GateSite nearest = null;
            var best = _config.Overworld.EntranceRadius;
            foreach (var site in _config.Overworld.Gates)
            {
                if (site == null) continue;
                var distance = Flat(site.Entrance - at).Length;
                if (distance > best) continue;
                nearest = site;
                best = distance;
            }
            return nearest;
        }

        /// <summary>
        /// The ready check at a gate's entrance: takes the party into the gate whose entrance
        /// <paramref name="starter"/> (the host) stands at, if everyone present is ready, wherever they
        /// stand. Any gate may be entered this way, licensed or not, at the party's own risk. Host only; immediate.
        /// </summary>
        public bool TryStartGateAtEntrance(PlayerId starter, out string reason)
        {
            var site = GateEntranceNear(starter);
            if (site == null)
            {
                reason = "Stand at a gate's entrance to go in.";
                return false;
            }
            if (!CanStartGate(out reason)) return false;
            StartGate(site.GateId);
            return true;
        }

        /// <summary>XP from a gate's enemies, as a percentage: cut in a gate whose region is not open (ADR 0015).</summary>
        private int XpPercentFor(GateDefinition gate) =>
            IsGateLicensed(gate.Id) ? 100 : Math.Max(0, _config.Overworld.UnopenedRegionXpPercent);

        /// <summary>Out in the world: anyone present and standing who comes near a hidden gate finds it.</summary>
        private void FindGates(int tick)
        {
            if (!InOverworld) return;
            var overworld = _config.Overworld;
            var radius = MathF.Max(overworld.FindRadius, overworld.EntranceRadius); // standing at an entrance always finds it
            foreach (var site in overworld.Gates)
            {
                if (site == null || IsGateFound(site.GateId)) continue;
                foreach (var player in _players)
                {
                    if (!player.IsAlive || player.Away || Flat(site.Entrance - player.Position).Length > SenseRadius(player, radius)) continue; // the platypus senses further (ticket 81)
                    _foundGates.Add(site.GateId);
                    _events.Add(new GateFound(tick, site.GateId, player.Id));
                    break;
                }
            }
        }

        /// <summary>Where <paramref name="player"/> stands coming back out of a gate: round its entrance, each at their own place.</summary>
        private Vec3 ComingOutPosition(PlayerId player, GateSite site)
        {
            var angle = player.Value * GoldenAngleRadians;
            var place = site.Entrance + new Vec3(MathF.Cos(angle) * ComingOutRadius, 0f, MathF.Sin(angle) * ComingOutRadius);
            return OnGround(Ground.IsWalkable(place) ? place : site.Entrance);
        }
    }
}
