using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Conformance topology adapter for the TCP transport.
/// </summary>
public sealed class TcpRawReliableAckConformanceAdapter : IRawReliableAckConformanceAdapter
{
    private readonly ILogger _logger;

    public TcpRawReliableAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawReliableAckConformanceFixture CreateFixture(
        RawReliableAckConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        var logger = options?.Logger ?? _logger;
        var port = GetRandomPort();
        return new Fixture(IPAddress.Loopback, port, logger, memory);
    }

    private static int GetRandomPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class Fixture : RawReliableAckConformanceFixture
    {
        private readonly IPAddress _address;
        private readonly int _port;

        public Fixture(IPAddress address, int port, ILogger logger, IMemoryRental memory)
            : base(new RawReliableAckTcpServer(address, port, 100, TimeSpan.FromSeconds(60), null, logger, memory), logger, memory)
        {
            _address = address;
            _port = port;
        }

        protected override IRawReliableAckClient CreateClientCore()
            => new RawReliableAckTcpClient(_address, _port, TimeSpan.FromSeconds(60), null, Logger, Memory);
    }
}
