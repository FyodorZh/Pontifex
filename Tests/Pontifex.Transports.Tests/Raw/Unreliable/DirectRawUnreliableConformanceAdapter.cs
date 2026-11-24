using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectRawUnreliableConformanceAdapter
    : IRawUnreliableConformanceAdapter<IRawUnreliableServer>
{
    private readonly ILogger _logger;

    public DirectRawUnreliableConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The Direct server has no delivery systems and cannot enforce
    /// reliable delivery for server replies; reliable debug mode is client-only for this
    /// carrier.</description></item>
    /// </list>
    /// </remarks>
    public bool ServerSupportsReliableDebug => false;

    public IRawUnreliableConformanceFixture<IRawUnreliableServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        return new Fixture(Guid.NewGuid().ToString("N"), _logger, memory);
    }

    private sealed class Fixture : RawUnreliableConformanceFixture<IRawUnreliableServer>
    {
        private readonly string _serverName;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
            : base(new RawUnreliableDirectServerTransport(serverName, logger, memory), logger, memory)
        {
            _serverName = serverName;
        }

        protected override bool InitServerCore(Func<IEndPoint, IRawUnreliableHandler?> factory)
            => Server.Init(factory);

        protected override IRawUnreliableClient CreateClientCore()
            => new RawUnreliableDirectClientTransport(_serverName, Logger, Memory);
    }
}
