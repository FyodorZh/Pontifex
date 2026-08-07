using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectRawReliableAckConformanceAdapter : IRawReliableAckConformanceAdapter
{
    private readonly ILogger _logger;

    public DirectRawReliableAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawReliableAckConformanceFixture CreateFixture(
        RawReliableAckConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        var logger = options?.Logger ?? _logger;
        return new Fixture(Guid.NewGuid().ToString("N"), logger, memory);
    }

    private sealed class Fixture : RawReliableAckConformanceFixture
    {
        private readonly string _serverName;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
            : base(new RawReliableAckDirectServer(serverName, logger, memory), logger, memory)
        {
            _serverName = serverName;
        }

        protected override IRawReliableAckClient CreateClientCore()
            => new RawReliableAckDirectClient(_serverName, Logger, Memory);
    }
}
