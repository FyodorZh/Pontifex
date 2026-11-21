using System;
using System.Collections.Generic;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Minimal tracking surface a test handler needs from its fixture: register
/// every endpoint handed to OnStarted so fixture disposal can reset its gates.
/// </summary>
public interface IRawUnreliableEndpointTracker
{
    void TrackEndpoint(IRawUnreliableEndpoint endpoint);
}

/// <summary>
/// One linked server-client topology for the carrier-independent
/// RawUnreliable conformance suite. The generic parameter is the server
/// transport type.
/// </summary>
public interface IRawUnreliableConformanceFixture<TServer> : IDisposable, IRawUnreliableEndpointTracker
    where TServer : IRawUnreliableTransport
{
    TServer Server { get; }

    /// <summary>Creates an unstarted client configured for this fixture's server route.</summary>
    IRawUnreliableClient CreateClient();

    /// <summary>
    /// Binds the server handler factory. The adapter maps the uniform delegate
    /// onto its server <c>Init</c>.
    /// </summary>
    bool InitServer(Func<IEndPoint, IRawUnreliableHandler?> factory);

    /// <summary>All endpoints registered so far (safe for concurrent reads).</summary>
    IReadOnlyList<IRawUnreliableEndpoint> TrackedEndpoints { get; }
}
