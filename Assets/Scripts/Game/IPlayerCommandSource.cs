using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>
    /// Produces a player's command for the coming ticks. The local keyboard is one source;
    /// tests, bots and (later) the network are others.
    /// </summary>
    public interface IPlayerCommandSource
    {
        PlayerCommand Sample();
    }

    /// <summary>Always returns the same command. Used by tests and as a stand-in for absent input.</summary>
    public sealed class ConstantCommandSource : IPlayerCommandSource
    {
        private readonly PlayerCommand _command;
        public ConstantCommandSource(PlayerCommand command) => _command = command;
        public PlayerCommand Sample() => _command;
    }
}
