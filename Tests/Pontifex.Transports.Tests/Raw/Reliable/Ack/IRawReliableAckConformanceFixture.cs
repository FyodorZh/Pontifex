using System;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// One linked server-client topology for the carrier-independent
/// RawReliableAck conformance suite. Owns the server transport, creates
/// clients, and tracks endpoints for gate cleanup on disposal.
/// </summary>
public interface IRawReliableAckConformanceFixture : IDisposable, IRawReliableAckEndpointTracker
{
    IRawReliableAckServer Server { get; }

    /// <summary>
    /// Creates an unstarted client configured for this fixture's server route.
    /// </summary>
    IRawReliableAckClient CreateClient();

    /// <summary>
    /// Initializes the server with an acknowledger. The acknowledger owns
    /// the admission logic for new client connections.
    /// </summary>
    bool InitServer(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger);

    /// <summary>
    /// Creates a simple acknowledger from a delegate. The delegate receives
    /// the client's ACK data, inspects it, releases it, and returns a fresh
    /// handler plus an owned ACK response (to accept) or null (to reject). The
    /// transport owns the returned response. If the delegate throws, the
    /// buffer is released and null is returned.
    /// </summary>
    IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> CreateSimpleAcknowledger(
        Func<UnionDataList, (IRawReliableAckServerHandler handler, UnionDataList ackResponse)?> tryAck);

    /// <summary>
    /// Acquires an empty ACK response from the fixture's memory for tests that
    /// do not care about the response content. The transport owns the returned
    /// response; the acknowledger must not release or retain it after returning.
    /// </summary>
    UnionDataList CreateEmptyAckResponse();
}
