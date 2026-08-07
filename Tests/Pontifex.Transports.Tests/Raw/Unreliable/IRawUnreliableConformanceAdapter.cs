namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Creates an implementation-specific RawUnreliable topology for the shared
/// carrier-independent conformance suite.
/// </summary>
public interface IRawUnreliableConformanceAdapter<TServer>
    where TServer : Pontifex.Raw.Unreliable.IRawUnreliableTransport
{
    IRawUnreliableConformanceFixture<TServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null);
}
