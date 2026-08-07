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
/// RawUnreliable conformance suite, shared by the Ack and NoAck variants.
/// The generic parameter is the variant server transport type.
/// </summary>
public interface IRawUnreliableConformanceFixture<TServer> : IDisposable, IRawUnreliableEndpointTracker
    where TServer : IRawUnreliableTransport
{
    TServer Server { get; }

    /// <summary>Creates an unstarted client configured for this fixture's server route.</summary>
    IRawUnreliableClient CreateClient();

    /// <summary>
    /// Binds the variant server handler factory. The adapter maps the uniform
    /// delegate onto its variant <c>Init</c>: NoAck ignores the message, Ack
    /// forwards it. For NoAck the message argument is always null.
    /// </summary>
    bool InitServer(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory);

    /// <summary>All endpoints registered so far (safe for concurrent reads).</summary>
    IReadOnlyList<IRawUnreliableEndpoint> TrackedEndpoints { get; }
}
