namespace HunterSystem.Netcode
{
    public enum NetEventKind
    {
        Connected,
        Data,
        Disconnected,
    }

    /// <summary>
    /// The host's side of whatever carries the bytes: Fish-Net over Steam in the game, a simulated
    /// network in tests. Polled once per tick, so the sessions stay deterministic and engine-free.
    /// </summary>
    public interface IHostTransport
    {
        bool TryReceive(out int connection, out NetEventKind kind, out byte[] payload);
        void Send(int connection, byte[] payload);
    }

    /// <summary>A client's side of the connection to the host.</summary>
    public interface IClientTransport
    {
        bool TryReceive(out byte[] payload);
        void Send(byte[] payload);
        void Disconnect();
    }
}
