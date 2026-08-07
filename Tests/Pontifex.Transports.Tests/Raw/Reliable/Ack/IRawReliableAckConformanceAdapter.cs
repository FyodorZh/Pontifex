namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Creates implementation-specific RawReliableAck topologies for the
/// carrier-independent conformance suite.
/// </summary>
public interface IRawReliableAckConformanceAdapter
{
    IRawReliableAckConformanceFixture CreateFixture(
        RawReliableAckConformanceFixtureOptions? options = null);
}
