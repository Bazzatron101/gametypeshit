namespace HunterSystem.Simulation
{
    /// <summary>Read-only snapshot of one player's simulation state.</summary>
    public readonly struct PlayerState
    {
        public readonly PlayerId Id;
        public readonly Vec3 Position;

        public PlayerState(PlayerId id, Vec3 position) { Id = id; Position = position; }

        internal PlayerState WithPosition(Vec3 position) => new PlayerState(Id, position);
    }
}
