using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Raw.Unreliable.NoAck.Udp;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

public sealed class UdpRawUnreliableNoAckConformanceAdapter : IRawUnreliableNoAckConformanceAdapter
{
    private readonly ILogger _logger;

    public UdpRawUnreliableNoAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableNoAckConformanceFixture CreateFixture(
        RawUnreliableNoAckConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        var port = GetFreePort();
        return new Fixture(port, _logger, memory);
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class Fixture : IRawUnreliableNoAckConformanceFixture
    {
        private readonly int _port;
        private readonly ILogger _logger;
        private readonly IMemoryRental _memory;
        private readonly List<IRawUnreliableNoAckClient> _clients = [];
        private bool _disposed;

        public Fixture(int port, ILogger logger, IMemoryRental memory)
        {
            _port = port;
            _logger = logger;
            _memory = memory;
            Server = new RawUnreliableNoAckUdpServer(IPAddress.Loopback, port, _logger, _memory);
        }

        public IRawUnreliableNoAckServer Server { get; }

        public IRawUnreliableNoAckClient CreateClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var client = new RawUnreliableNoAckUdpClient(IPAddress.Loopback, _port, _logger, _memory);
            _clients.Add(client);
            return client;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            ResetGates(Server);
            foreach (var client in _clients)
                ResetGates(client);

            foreach (var client in _clients)
                client.Stop();
            Server.Stop();
        }

        private static void ResetGates(ITransport transport)
        {
            var controls = new List<IControl>();
            transport.GetControls(controls);

            foreach (var control in controls)
            {
                if (control is IConformanceControl transportControl)
                {
                    transportControl.BeforeStopStateTransitionGate.Reset();
                    transportControl.BeforeStoppedCallbackGate.Reset();
                }

                if (control is IRawUnreliableNoAckConformanceControl unreliableControl)
                {
                    unreliableControl.BeforeSendCommitGate.Reset();
                    unreliableControl.AfterSendCommitGate.Reset();
                    unreliableControl.AfterReceivedGate.Reset();
                }
            }
        }
    }
}
