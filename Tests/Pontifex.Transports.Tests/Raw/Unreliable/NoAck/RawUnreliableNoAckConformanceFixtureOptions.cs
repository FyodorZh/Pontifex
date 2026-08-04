using Actuarius.Memory;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Shared construction options for every endpoint in one conformance fixture.
/// </summary>
public sealed class RawUnreliableNoAckConformanceFixtureOptions
{
    /// <summary>
    /// Memory rental used for the fixture server and every fixture client.
    /// A null value lets the adapter select its normal test default.
    /// </summary>
    public IMemoryRental? MemoryRental { get; init; }
}
