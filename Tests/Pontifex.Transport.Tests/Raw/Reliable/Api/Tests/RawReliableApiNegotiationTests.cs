using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Raw.Reliable.Api.Harness;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Api;

/// <summary>
/// Deterministic tests for the Api negotiation layer (<c>"api"</c>, version <c>1</c>): the flat request, the
/// admission gate, and the rejection lifecycle. Every test runs over hand-written fake reliable transports with
/// synchronous, caller-driven delivery: no real time, sockets, or networks.
/// </summary>
[TestFixture]
[Category("RawReliableApiNegotiation")]
public sealed class RawReliableApiNegotiationTests
{
    private static ILogger Log => new Logger([]);

    [Test]
    public void ServerApi_WithRawApplicationPayloadInsteadOfRequest_RejectsAndDoesNotStartApi()
    {
        using var pair = new ApiNegotiationPair();
        pair.ClientToServer = data =>
        {
            data.Release();
            pair.ServerSession!.DeliverToServer(ApiPayload.Create("raw-application-data"));
        };

        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerApiStartedCount, Is.Zero,
                "the Api must not start before the api negotiation is admitted");
            Assert.That(pair.ClientConnectedCount, Is.Zero);
            Assert.That(pair.ClientDisconnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientDisconnectReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.ServerTransport.IsStarted, Is.True,
                "a rejected session must not stop the server transport");
            Assert.That(pair.ServerSession!.Endpoint.Sent, Has.Count.EqualTo(1));
            Assert.That(ApiPayload.Describe(pair.ServerSession.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:api", "b:2", "s:expected the api negotiation request" }));
        });
    }

    [Test]
    public void ClientApi_WithWrongVersionResponse_RaisesDisconnectedOnceAndNoConnected()
    {
        using var pair = new ApiNegotiationPair();
        pair.ServerToClient = data =>
        {
            data.Release();
            pair.ClientTransport.DeliverToClient(RawReliableNegotiation.BuildResponse(2, "api", null));
        };

        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientConnectedCount, Is.Zero);
            Assert.That(pair.ClientDisconnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientDisconnectReason, Is.TypeOf<ProtocolMismatch>());
        });
    }

    [Test]
    public void ClientApi_WithWrongNameResponse_RaisesDisconnectedOnceAndNoConnected()
    {
        using var pair = new ApiNegotiationPair();
        pair.ServerToClient = data =>
        {
            data.Release();
            pair.ClientTransport.DeliverToClient(RawReliableNegotiation.BuildResponse(1, "other", null));
        };

        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientConnectedCount, Is.Zero);
            Assert.That(pair.ClientDisconnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientDisconnectReason, Is.TypeOf<ProtocolMismatch>());
        });
    }

    [Test]
    public void ClientApi_FirstOutboundPayload_IsTheApiNegotiationRequest()
    {
        using var pair = new ApiNegotiationPair();
        pair.ClientToServer = data => data.Release();

        pair.Start();

        Assert.That(pair.ClientTransport.Endpoint, Is.Not.Null);
        Assert.That(pair.ClientTransport.Endpoint!.Sent, Has.Count.EqualTo(1));
        Assert.That(ApiPayload.Describe(pair.ClientTransport.Endpoint.Sent[0]), Is.EqualTo(
            new[] { "h:1", "s:api", "b:0" }));
    }

    [Test]
    public void ServerApi_WithAppendedNegotiationElements_AdmitsTheSession()
    {
        var clientApi = new TailAppendingClientSideApi(new ApiNegotiationTestApi(), MemoryRental.Shared, Log);
        using var pair = new ApiNegotiationPair(clientApi);

        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(clientApi.AppendedCount, Is.EqualTo(1));
            Assert.That(ApiPayload.Describe(pair.ClientTransport.Endpoint!.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:api", "b:0", "i:42" }));
            Assert.That(pair.ClientConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ServerApiStartedCount, Is.EqualTo(1));
            Assert.That(pair.ClientDisconnectedCount, Is.Zero);
            Assert.That(pair.ClientDisconnectReason, Is.Null);
        });
    }
}
