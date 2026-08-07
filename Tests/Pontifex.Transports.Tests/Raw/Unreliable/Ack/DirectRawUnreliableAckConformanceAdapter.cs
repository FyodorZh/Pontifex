using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Raw.Unreliable.Ack.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable.Ack;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectRawUnreliableAckConformanceAdapter : IRawUnreliableAckConformanceAdapter
{
    private readonly ILogger _logger;

    public DirectRawUnreliableAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableConformanceFixture<IRawUnreliableAckServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        return new Fixture(Guid.NewGuid().ToString("N"), _logger, memory);
    }

    private sealed class Fixture : RawUnreliableConformanceFixture<IRawUnreliableAckServer>
    {
        private readonly string _serverName;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
            : base(new RawUnreliableAckDirectServer(serverName, logger, memory), logger, memory)
        {
            _serverName = serverName;
        }

        protected override bool InitServerCore(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory)
            => Server.Init((source, message) => factory(source, message));

        protected override IRawUnreliableClient CreateClientCore()
            => new RawUnreliableAckDirectClient(_serverName, Logger, Memory);
    }
}
