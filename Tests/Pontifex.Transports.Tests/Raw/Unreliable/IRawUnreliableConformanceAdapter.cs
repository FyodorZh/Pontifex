namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Creates an implementation-specific RawUnreliable topology for the shared
/// carrier-independent conformance suite.
/// </summary>
public interface IRawUnreliableConformanceAdapter<TServer>
    where TServer : Pontifex.Raw.Unreliable.IRawUnreliableTransport
{
    /// <summary>
    /// Gets a value indicating whether the adapter's server can enforce reliable debug delivery.
    /// </summary>
    bool ServerSupportsReliableDebug { get; }

    IRawUnreliableConformanceFixture<TServer> CreateFixture(
        RawUnreliableConformanceFixtureOptions? options = null);
}
