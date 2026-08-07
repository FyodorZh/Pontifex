using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Raw.Unreliable.NoAck.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectRawUnreliableNoAckConformanceAdapter : IRawUnreliableNoAckConformanceAdapter
{
    private readonly ILogger _logger;

    public DirectRawUnreliableNoAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableConformanceFixture<IRawUnreliableNoAckServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        return new Fixture(Guid.NewGuid().ToString("N"), _logger, memory);
    }

    private sealed class Fixture : RawUnreliableConformanceFixture<IRawUnreliableNoAckServer>
    {
        private readonly string _serverName;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
            : base(new RawUnreliableNoAckDirectServer(serverName, logger, memory), logger, memory)
        {
            _serverName = serverName;
        }

        protected override bool InitServerCore(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory)
            => Server.Init(source => factory(source, null));

        protected override IRawUnreliableClient CreateClientCore()
            => new RawUnreliableNoAckDirectClient(_serverName, Logger, Memory);
    }
}
