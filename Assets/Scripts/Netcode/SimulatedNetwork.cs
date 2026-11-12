using System.Collections.Generic;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// An in-memory network for tests: every message (and connect and disconnect) arrives a fixed
    /// number of ticks after it was sent, in order. Client-to-host data can be dropped to simulate
    /// packet loss. Call <see cref="Advance"/> once per tick before the host and clients tick.
    /// </summary>
    public sealed class SimulatedNetwork
    {
        private readonly int _latencyTicks;
        private readonly List<(int at, int connection, NetEventKind kind, byte[] payload)> _toHost = new List<(int, int, NetEventKind, byte[])>();
        private readonly Dictionary<int, ClientEnd> _clients = new Dictionary<int, ClientEnd>();
        private int _now;
        private int _nextConnection = 1;

        public SimulatedNetwork(int latencyTicks)
        {
            _latencyTicks = latencyTicks;
            HostEndpoint = new HostEnd(this);
        }

        public IHostTransport HostEndpoint { get; }

        /// <summary>While true, data sent by clients never reaches the host.</summary>
        public bool DropClientMessages { get; set; }

        public void Advance() => _now++;

        /// <summary>Opens a connection; the host hears about it after the latency.</summary>
        public IClientTransport Connect()
        {
            var client = new ClientEnd(this, _nextConnection++);
            _clients.Add(client.Connection, client);
            _toHost.Add((_now + _latencyTicks, client.Connection, NetEventKind.Connected, null));
            return client;
        }

        private sealed class HostEnd : IHostTransport
        {
            private readonly SimulatedNetwork _net;

            public HostEnd(SimulatedNetwork net) => _net = net;

            public bool TryReceive(out int connection, out NetEventKind kind, out byte[] payload)
            {
                for (var i = 0; i < _net._toHost.Count; i++)
                {
                    var m = _net._toHost[i];
                    if (m.at > _net._now) continue;
                    _net._toHost.RemoveAt(i);
                    (connection, kind, payload) = (m.connection, m.kind, m.payload);
                    return true;
                }
                (connection, kind, payload) = (0, NetEventKind.Data, null);
                return false;
            }

            public void Send(int connection, byte[] payload)
            {
                if (_net._clients.TryGetValue(connection, out var client) && client.Open)
                    client.Inbox.Add((_net._now + _net._latencyTicks, payload));
            }
        }

        private sealed class ClientEnd : IClientTransport
        {
            private readonly SimulatedNetwork _net;
            public readonly int Connection;
            public readonly List<(int at, byte[] payload)> Inbox = new List<(int, byte[])>();
            public bool Open = true;

            public ClientEnd(SimulatedNetwork net, int connection)
            {
                _net = net;
                Connection = connection;
            }

            public bool TryReceive(out byte[] payload)
            {
                if (Open && Inbox.Count > 0 && Inbox[0].at <= _net._now)
                {
                    payload = Inbox[0].payload;
                    Inbox.RemoveAt(0);
                    return true;
                }
                payload = null;
                return false;
            }

            public void Send(byte[] payload)
            {
                if (!Open || _net.DropClientMessages) return;
                _net._toHost.Add((_net._now + _net._latencyTicks, Connection, NetEventKind.Data, payload));
            }

            public void Disconnect()
            {
                if (!Open) return;
                Open = false;
                Inbox.Clear();
                _net._toHost.Add((_net._now + _net._latencyTicks, Connection, NetEventKind.Disconnected, null));
            }
        }
    }
}
