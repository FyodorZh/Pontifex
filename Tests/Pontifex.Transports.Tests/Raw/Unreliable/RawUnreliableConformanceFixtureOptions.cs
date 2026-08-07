using Actuarius.Memory;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Shared construction options for every endpoint in one RawUnreliable
/// conformance fixture, used by both the Ack and NoAck contract variants.
/// </summary>
public sealed class RawUnreliableConformanceFixtureOptions
{
    /// <summary>
    /// Memory rental used for the fixture server and every fixture client.
    /// A null value lets the adapter select its normal test default.
    /// </summary>
    public IMemoryRental? MemoryRental { get; init; }
}
