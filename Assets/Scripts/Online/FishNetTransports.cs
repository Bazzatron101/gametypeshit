using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HunterSystem.Netcode;

namespace HunterSystem.Online
{
    /// <summary>
    /// The only Fish-Net message the game sends: an opaque packet carrying one of our own wire
    /// messages (see <see cref="Wire"/>). Fish-Net is used for connections, the Steam relay and
    /// delivery; the game's own sessions do the rest.
    /// </summary>
    public struct NetPacket : IBroadcast
    {
        public byte[] Data;
    }

    /// <summary>
    /// The host side over Fish-Net's server. Connection changes and packets are queued as they arrive
    /// and handed to the <see cref="HostSession"/> when it polls on its tick.
    /// </summary>
    public sealed class FishNetHostTransport : IHostTransport
    {
        private readonly NetworkManager _manager;
        private readonly bool _addressIsIdentity;
        private readonly Queue<(int connection, NetEventKind kind, byte[] payload)> _inbox = new Queue<(int, NetEventKind, byte[])>();

        /// <param name="addressIsIdentity">
        /// True on Steam, where a connection's address is the player's SteamID as verified by Steam,
        /// so it can identify players for rejoining. False on plain UDP, where it is only an IP.
        /// </param>
        public FishNetHostTransport(NetworkManager manager, bool addressIsIdentity)
        {
            _manager = manager;
            _addressIsIdentity = addressIsIdentity;
            _manager.ServerManager.RegisterBroadcast<NetPacket>(OnPacket, requireAuthentication: false);
            _manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public bool TryReceive(out int connection, out NetEventKind kind, out byte[] payload)
        {
            if (_inbox.Count > 0)
            {
                (connection, kind, payload) = _inbox.Dequeue();
                return true;
            }
            (connection, kind, payload) = (0, NetEventKind.Data, null);
            return false;
        }

        public void Send(int connection, byte[] payload)
        {
            if (_manager.ServerManager.Clients.TryGetValue(connection, out var client))
                _manager.ServerManager.Broadcast(client, new NetPacket { Data = payload }, requireAuthenticated: false, Channel.Reliable);
        }

        public string IdentityOf(int connection)
        {
            if (!_addressIsIdentity || !_manager.ServerManager.Clients.TryGetValue(connection, out var client)) return null;
            var address = client.GetAddress();
            return string.IsNullOrEmpty(address) ? null : "steam:" + address;
        }

        private void OnPacket(NetworkConnection from, NetPacket packet, Channel channel) =>
            _inbox.Enqueue((from.ClientId, NetEventKind.Data, packet.Data));

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started) _inbox.Enqueue((connection.ClientId, NetEventKind.Connected, null));
            else if (args.ConnectionState == RemoteConnectionState.Stopped) _inbox.Enqueue((connection.ClientId, NetEventKind.Disconnected, null));
        }
    }

    /// <summary>
    /// The client side over Fish-Net's client. Messages sent before the connection is up (the
    /// session's hello) wait and go out as soon as it is.
    /// </summary>
    public sealed class FishNetClientTransport : IClientTransport
    {
        private readonly NetworkManager _manager;
        private readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        private readonly Queue<byte[]> _waiting = new Queue<byte[]>();

        // Made just before the connection is started, so it begins as connecting rather than closed.
        private LocalConnectionState _state = LocalConnectionState.Starting;
        private bool _sawOwnStart;

        public FishNetClientTransport(NetworkManager manager)
        {
            _manager = manager;
            _manager.ClientManager.RegisterBroadcast<NetPacket>(OnPacket);
            _manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        /// <summary>True while connecting or connected; false once the connection has closed.</summary>
        public bool IsConnected => _state == LocalConnectionState.Starting || _state == LocalConnectionState.Started;

        public bool TryReceive(out byte[] payload)
        {
            if (_inbox.Count > 0)
            {
                payload = _inbox.Dequeue();
                return true;
            }
            payload = null;
            return false;
        }

        public void Send(byte[] payload)
        {
            if (_state != LocalConnectionState.Started)
            {
                _waiting.Enqueue(payload);
                return;
            }
            _manager.ClientManager.Broadcast(new NetPacket { Data = payload }, Channel.Reliable);
        }

        /// <summary>Closes the connection and lets go of Fish-Net, so a later transport (a rejoin) is the only one listening.</summary>
        public void Disconnect()
        {
            _manager.ClientManager.StopConnection();
            _manager.ClientManager.UnregisterBroadcast<NetPacket>(OnPacket);
            _manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _state = LocalConnectionState.Stopped;
        }

        private void OnPacket(NetPacket packet, Channel channel) => _inbox.Enqueue(packet.Data);

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            // Until our own connection has started, a "stopping/stopped" is the previous one's (a rejoin).
            if (!_sawOwnStart && (args.ConnectionState == LocalConnectionState.Stopping || args.ConnectionState == LocalConnectionState.Stopped)) return;
            if (args.ConnectionState == LocalConnectionState.Starting || args.ConnectionState == LocalConnectionState.Started) _sawOwnStart = true;
            _state = args.ConnectionState;
            if (args.ConnectionState != LocalConnectionState.Started) return;
            while (_waiting.Count > 0) _manager.ClientManager.Broadcast(new NetPacket { Data = _waiting.Dequeue() }, Channel.Reliable);
        }
    }
}
