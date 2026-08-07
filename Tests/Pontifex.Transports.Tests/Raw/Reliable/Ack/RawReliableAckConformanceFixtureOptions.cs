using Actuarius.Memory;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Construction options for one RawReliableAck conformance fixture.
/// </summary>
public sealed class RawReliableAckConformanceFixtureOptions
{
    /// <summary>
    /// Memory rental used for the fixture server and every fixture client.
    /// A null value lets the adapter select its normal test default.
    /// </summary>
    public IMemoryRental? MemoryRental { get; init; }

    /// <summary>
    /// Logger used for the fixture server and every fixture client.
    /// A null value lets the adapter select its normal test default.
    /// Supplying a logger with a custom observable <c>ILogSink</c> enables
    /// tests to verify that errors are logged according to the specification.
    /// </summary>
    public ILogger? Logger { get; init; }
}
