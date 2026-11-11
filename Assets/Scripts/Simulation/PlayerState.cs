namespace HunterSystem.Simulation
{
    /// <summary>Read-only snapshot of one player's simulation state.</summary>
    public readonly struct PlayerState
    {
        public readonly PlayerId Id;
        public readonly Vec3 Position;

        /// <summary>Unit vector on the floor plane: the direction the player last moved. Attacks go this way.</summary>
        public readonly Vec3 Facing;

        public PlayerState(PlayerId id, Vec3 position, Vec3 facing)
        {
            Id = id;
            Position = position;
            Facing = facing;
        }
    }
}
