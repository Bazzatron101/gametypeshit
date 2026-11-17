namespace HunterSystem.Simulation
{
    /// <summary>
    /// The ground (ticket 37): which ground the party is on now, and settling everyone on it at the
    /// end of each tick. Moves during the tick are free; the settle then slides anyone who walked onto
    /// ground they cannot stand on back along the edge, and puts everyone at the ground's height (a
    /// jumping player keeps their height above it).
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>
        /// The ground the party is on: a running gate's rooms; else the world's ground when there is an
        /// overworld (the town is part of it); else the hub's walled floor; else the world's ground (open and flat when the config has none).
        /// </summary>
        public IGround Ground =>
            _gate != null ? _gate.Layout.Ground
            : _config.Overworld == null && _hubGround != null ? _hubGround
            : _config.Ground ?? OpenGround.Instance;

        private void NoteWhereEveryoneSetsOff()
        {
            foreach (var player in _players) player.SetOffFrom = player.Position;
            foreach (var enemy in _enemies) enemy.SetOffFrom = enemy.Position;
        }

        /// <summary>Walls and steep ground keep everyone where they can stand, at the ground's height.</summary>
        private void SettleOnGround()
        {
            var ground = Ground;
            foreach (var player in _players)
            {
                var lift = HeightAboveGround(player);
                var settled = GroundFor(player, ground).Confine(player.SetOffFrom, player.Position); // riders do not climb; flyers cross cliffs (tickets 42, 90)
                player.Position = new Vec3(settled.X, ground.HeightAt(settled) + lift, settled.Z);
            }
            foreach (var enemy in _enemies)
                enemy.Position = OnGround(ground.Confine(enemy.SetOffFrom, enemy.Position));
        }

        private NoClimbingGround _ridingGround;

        /// <summary>The ground as a mounted rider sees it: climbable faces are cliffs (ticket 42).</summary>
        private IGround RidingGround(IGround ground)
        {
            if (_ridingGround == null || !ReferenceEquals(_ridingGround.Inner, ground)) _ridingGround = new NoClimbingGround(ground);
            return _ridingGround;
        }

        /// <summary><paramref name="p"/> at the ground's height there.</summary>
        private Vec3 OnGround(Vec3 p) => new Vec3(p.X, Ground.HeightAt(p), p.Z);
    }
}
