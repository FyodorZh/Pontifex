using Pontifex.NoAck.Raw;
using Pontifex.NoAck.Raw.Unreliable;

namespace Pontifex.Tests.NoAck.Raw.Unreliable;

/// <summary>
/// Creates implementation-specific NoAckRawUnreliable topologies for the
/// carrier-independent conformance suite.
/// </summary>
public interface INoAckRawUnreliableConformanceAdapter
{
    INoAckRawUnreliableConformanceFixture CreateFixture(
        NoAckRawUnreliableConformanceFixtureOptions? options = null);
}

/// <summary>
/// Owns one server and every client created for that server's test topology.
/// </summary>
public interface INoAckRawUnreliableConformanceFixture : IDisposable
{
    INoAckRawUnreliableServer Server { get; }

    /// <summary>
    /// Creates an unstarted client configured for this fixture's server route.
    /// Clients may be created before or after the server becomes terminal.
    /// </summary>
    INoAckRawUnreliableClient CreateClient();
}
