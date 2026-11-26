using Actuarius.Memory;
using Pontifex.Api;
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

    [Test]
    public void ClientApi_WithManualClock_TimesOutAtTheNegotiationDeadline()
    {
        var clock = new ManualTimerFactory();
        var clientApi = new ClientSideApi(new ApiNegotiationTestApi(), MemoryRental.Shared, Log, clock);
        var serverInstance = new ServerSideApiInstance<ApiNegotiationTestApi>(
            new ApiNegotiationTestApi(), MemoryRental.Shared, Log, clock);
        using var pair = new ApiNegotiationPair(clientApi, serverInstance);
        pair.ServerToClient = data => data.Release(); // never answer the request

        pair.Start();

        Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(10) }),
            "only the client deadline remains armed after the server accepted");
        Assert.That(pair.ClientConnectedCount, Is.Zero);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientConnectedCount, Is.Zero);
            Assert.That(pair.ClientDisconnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientDisconnectReason, Is.TypeOf<TimeOut>());
            Assert.That(clock.PendingCount, Is.Zero, "the deadline is not left armed after it fires");
        });
    }

    [Test]
    public void ServerApi_WithManualClock_RejectsAtTheDeadlineAndLingersTwoSeconds()
    {
        var clock = new ManualTimerFactory();
        var clientApi = new ClientSideApi(new ApiNegotiationTestApi(), MemoryRental.Shared, Log, clock);
        var serverInstance = new ServerSideApiInstance<ApiNegotiationTestApi>(
            new ApiNegotiationTestApi(), MemoryRental.Shared, Log, clock);
        using var pair = new ApiNegotiationPair(clientApi, serverInstance);
        pair.ClientToServer = data => data.Release(); // the server never sees a request

        pair.Start();

        Assert.That(clock.PendingDelays, Is.EqualTo(new[]
        {
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)
        }), "both sides arm their deadline; neither has completed");
        Assert.That(pair.ServerApiStartedCount, Is.Zero);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerApiStartedCount, Is.Zero);
            Assert.That(pair.ServerSession!.Endpoint.Sent, Has.Count.EqualTo(1));
            Assert.That(ApiPayload.Describe(pair.ServerSession.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:api", "b:2", "s:negotiation timeout" }));
            Assert.That(pair.ServerSession.Endpoint.IsConnected, Is.True,
                "the rejection linger keeps the session open");
            Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerSession.Endpoint.IsConnected, Is.False,
                "the linger closes the session after two seconds");
            Assert.That(clock.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void ServerApiFactory_CreatesAFreshInstancePerAdmittedSession()
    {
        int created = 0;
        using var pair = new ApiNegotiationPair(instanceFactory: () =>
        {
            created++;
            return new ServerSideApiInstance<ApiNegotiationTestApi>(
                new ApiNegotiationTestApi(), MemoryRental.Shared, Log);
        });

        pair.Start();

        Assert.That(created, Is.EqualTo(1));
        Assert.That(pair.ServerApiStartedCount, Is.EqualTo(1));

        // The second accepted response is not routed to the already-admitted client.
        pair.ServerToClient = data => data.Release();
        var second = pair.ConnectServerSession();

        Assert.That(created, Is.EqualTo(2));
        Assert.That(second.Handler, Is.Not.SameAs(pair.ServerSessions[0].Handler));

        var request = pair.ClientTransport.Endpoint!.Sent[0]
            .Clone(MemoryRental.Shared.CollectablePool);
        second.DeliverToServer(request);

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerApiStartedCount, Is.EqualTo(2),
                "the second session starts its own Api instance");
            Assert.That(pair.ServerInstances, Has.Count.EqualTo(2));
            Assert.That(pair.ServerInstances[1], Is.Not.SameAs(pair.ServerInstances[0]));
        });
    }
}
