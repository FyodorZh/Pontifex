using System;
using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Udp;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable;

public sealed class UdpRawUnreliableConformanceAdapter
    : IRawUnreliableConformanceAdapter<IRawUnreliableServer>
{
    private readonly ILogger _logger;

    public UdpRawUnreliableConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableConformanceFixture<IRawUnreliableServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        var port = GetFreePort();
        return new Fixture(port, _logger, memory);
    }

    private static int GetFreePort()
    {
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class Fixture : RawUnreliableConformanceFixture<IRawUnreliableServer>
    {
        private readonly int _port;

        public Fixture(int port, ILogger logger, IMemoryRental memory)
            : base(new RawUnreliableUdpServer(IPAddress.Loopback, port, logger, memory), logger, memory)
        {
            _port = port;
        }

        protected override bool InitServerCore(Func<IEndPoint, IRawUnreliableHandler?> factory)
            => Server.Init(factory);

        protected override IRawUnreliableClient CreateClientCore()
            => new RawUnreliableUdpClient(IPAddress.Loopback, _port, Logger, Memory);
    }
}
