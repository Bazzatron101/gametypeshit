namespace HunterSystem.Simulation
{
    /// <summary>
    /// Rank promotions (ticket 25). Ranks are guild licences tied to level bands (spec story 5, 9). The world
    /// has a rank of its own, the host's world's: when the world's owner (the host's character) reaches the next
    /// band, the world is promoted. That opens the rank's regions and lets its promotion scene play once the
    /// party is home. Guests' levels never promote the host's world (spec story 71), but every character's own
    /// rank is its level's band, and the rank that brings the first mount brings it to anyone who reaches it.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private int _worldRank;
        private PlayerId? _worldOwner;

        /// <summary>The host's world's rank, as an index into the progression's ranks: 0 for a new world.</summary>
        public int WorldRankIndex => _worldRank;

        /// <summary>The host's world's rank's id ("E", "D", ...), or null with no ranks configured.</summary>
        public string WorldRank => RankIdAt(_worldRank);

        /// <summary>The player whose level promotes this world: the host's own. None in a world without a host (the arena).</summary>
        public PlayerId? WorldOwner => _worldOwner;

        /// <summary>Host only: whose level promotes this world (the host's own player).</summary>
        public void SetWorldOwner(PlayerId? owner) => _worldOwner = owner;

        /// <summary>
        /// Host only: the rank the host's world has reached (from its save). Unknown or missing ranks leave it at the first.
        /// Regions it opened are restored with the world's open regions, not here.
        /// </summary>
        public void SetWorldRank(string rankId) => _worldRank = System.Math.Max(0, _config.Progression.RankIndexOf(rankId));

        /// <summary>The rank <paramref name="id"/>'s character holds: its level's band.</summary>
        public string RankOf(PlayerId id) => RankIdAt(_config.Progression.RankIndexAt(FindPlayer(id).Level));

        private string RankIdAt(int index)
        {
            var ranks = _config.Progression.Ranks;
            return ranks != null && index >= 0 && index < ranks.Length ? ranks[index]?.Id : null;
        }

        /// <summary>Whether the host's world has reached <paramref name="rankId"/>. A rank that is not configured counts as reached.</summary>
        public bool HasReachedRank(string rankId)
        {
            var index = _config.Progression.RankIndexOf(rankId);
            return index < 0 || _worldRank >= index;
        }

        /// <summary>
        /// Each tick: the world follows its owner's band up one rank at a time, opening each rank's regions;
        /// and anyone who has reached a rank that grants a mount has one.
        /// </summary>
        private void UpdateRanks(int tick)
        {
            var progression = _config.Progression;
            var ranks = progression.Ranks;
            if (ranks == null || ranks.Length == 0) return;

            if (_worldOwner.HasValue && _playersById.TryGetValue(_worldOwner.Value, out var owner))
            {
                var band = progression.RankIndexAt(owner.Level);
                while (_worldRank < band)
                {
                    _worldRank++;
                    var rank = ranks[_worldRank];
                    foreach (var region in rank?.OpensRegions ?? System.Array.Empty<string>())
                        if (region != null && !_openRegions.Contains(region)) _openRegions.Add(region);
                    _events.Add(new RankPromoted(tick, rank?.Id, owner.Id));
                }
            }

            var mountRank = FirstMountRank(ranks);
            if (mountRank < 0) return;
            foreach (var player in _players)
            {
                if (player.HasMount || progression.RankIndexAt(player.Level) < mountRank) continue;
                player.HasMount = true;
                _events.Add(new MountGranted(tick, player.Id));
            }
        }

        private static int FirstMountRank(RankDefinition[] ranks)
        {
            for (var i = 0; i < ranks.Length; i++)
                if (ranks[i] != null && ranks[i].GrantsMount) return i;
            return -1;
        }
    }
}
