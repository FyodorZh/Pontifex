using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Raw.Unreliable.Ack.Udp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable.Ack;

public sealed class UdpRawUnreliableAckConformanceAdapter : IRawUnreliableAckConformanceAdapter
{
    private readonly ILogger _logger;

    public UdpRawUnreliableAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableConformanceFixture<IRawUnreliableAckServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null)
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

    private sealed class Fixture : RawUnreliableConformanceFixture<IRawUnreliableAckServer>
    {
        private readonly int _port;

        public Fixture(int port, ILogger logger, IMemoryRental memory)
            : base(new RawUnreliableAckUdpServer(IPAddress.Loopback, port, logger, memory), logger, memory)
        {
            _port = port;
        }

        protected override bool InitServerCore(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory)
            => Server.Init((source, message) => factory(source, message));

        protected override IRawUnreliableClient CreateClientCore()
            => new RawUnreliableAckUdpClient(IPAddress.Loopback, _port, Logger, Memory);
    }
}
