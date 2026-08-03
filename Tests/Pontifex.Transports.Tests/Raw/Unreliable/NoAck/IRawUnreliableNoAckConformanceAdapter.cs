using Pontifex.Raw.Unreliable.NoAck;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Creates implementation-specific RawUnreliableNoAck topologies for the
/// carrier-independent conformance suite.
/// </summary>
public interface IRawUnreliableNoAckConformanceAdapter
{
    IRawUnreliableNoAckConformanceFixture CreateFixture(
        RawUnreliableNoAckConformanceFixtureOptions? options = null);
}

/// <summary>
/// Owns one server and every client created for that server's test topology.
/// </summary>
public interface IRawUnreliableNoAckConformanceFixture : IDisposable
{
    IRawUnreliableNoAckServer Server { get; }

    /// <summary>
    /// Creates an unstarted client configured for this fixture's server route.
    /// Clients may be created before or after the server becomes terminal.
    /// </summary>
    IRawUnreliableNoAckClient CreateClient();
}
