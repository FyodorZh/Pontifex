using System;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableEndpointClientTransport{TClientHandler}"/>, exercised
/// through the endpoint factory and attachment surface exposed by
/// <see cref="TestRawReliableEndpointClientTransport"/>. Covers the reliable client facade delta and the endpoint
/// creation and teardown hand-off. The inherited init, connect state machine, and failed-start non-notification
/// are covered by <see cref="RawReliableClientTransportTests"/> and are not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableEndpointClientTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [Test]
    public void EndpointClientTransport_ExposesReliableClientContract()
    {
        var transport = new TestRawReliableEndpointClientTransport();

        Assert.That(transport, Is.InstanceOf<IRawReliableClient>());
        Assert.That(transport, Is.InstanceOf<IRawReliableClientTransport<IRawReliableClientHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void EndpointClientTransport_HasRawReliableTransportType()
    {
        var transport = new TestRawReliableEndpointClientTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliable));
    }

    [Test]
    public void Start_CreatesAndStartsEndpoint()
    {
        var transport = new TestRawReliableEndpointClientTransport();
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.AttachedEndpoint, Is.SameAs(transport.LastCreatedEndpoint));

        using var endpoint = transport.LastCreatedEndpoint!;
        endpoint.DrainAll();
        Assert.That(transport.IsConnected, Is.False);

        endpoint.CompleteConnect();
        endpoint.CompleteHandshake();

        Assert.That(transport.IsConnected, Is.True);
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_DestroysAttachedEndpoint()
    {
        var transport = new TestRawReliableEndpointClientTransport();
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        using var endpoint = transport.LastCreatedEndpoint!;
        endpoint.CompleteConnect();
        endpoint.CompleteHandshake();
        Assert.That(transport.IsConnected, Is.True);

        var reason = new UserIntention("test");
        Assert.That(transport.Stop(reason), Is.True);

        endpoint.DrainAll();
        endpoint.CompleteDisconnect();

        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.AttachedEndpoint, Is.Null);
        Assert.That(endpoint.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(endpoint.LastDisconnectReason, Is.SameAs(reason));
        Assert.That(endpoint.IsRunning, Is.False);
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenCreateEndpointThrows_FailsWithoutHandlerNotification()
    {
        var transport = new TestRawReliableEndpointClientTransport();
        transport.CreateEndpointException = new InvalidOperationException("endpoint creation failure");
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        int userStoppedCalls = 0;

        Assert.That(transport.Start(_ => userStoppedCalls++), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(transport.IsValid, Is.False);
            Assert.That(transport.IsStarted, Is.False);
            Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
            Assert.That(transport.AttachedEndpoint, Is.Null);
            Assert.That(transport.LastFakeEndpoint, Is.Null);
            Assert.That(handler.StoppedCount, Is.Zero);
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(userStoppedCalls, Is.Zero);
        });
    }

    [Test]
    public void Start_WhenEndpointStartReturnsFalse_StopsAttachedEndpointAndFailsWithoutHandlerNotification()
    {
        var transport = new TestRawReliableEndpointClientTransport();
        transport.EndpointStartReturnsFalse = true;
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        int userStoppedCalls = 0;

        Assert.That(transport.Start(_ => userStoppedCalls++), Is.False);

        FakeRawReliableClientEndpoint endpoint = transport.LastFakeEndpoint!;
        Assert.Multiple(() =>
        {
            Assert.That(transport.IsValid, Is.False);
            Assert.That(transport.IsStarted, Is.False);
            Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
            Assert.That(transport.EndpointWasAttachedDuringStart, Is.True,
                "the endpoint must be attached before its start is requested");
            Assert.That(transport.AttachedEndpoint, Is.Null);
            Assert.That(endpoint.StartCalls, Is.EqualTo(1));
            Assert.That(endpoint.StopCalls, Is.EqualTo(1));
            Assert.That(endpoint.LastStopReason, Is.Not.Null);
            Assert.That(handler.StoppedCount, Is.Zero);
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(userStoppedCalls, Is.Zero);
        });
    }

    [Test]
    public void Start_WhenEndpointStartThrows_StopsAttachedEndpointAndFailsWithoutHandlerNotification()
    {
        var transport = new TestRawReliableEndpointClientTransport();
        transport.EndpointStartException = new InvalidOperationException("endpoint start failure");
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        int userStoppedCalls = 0;

        Assert.That(transport.Start(_ => userStoppedCalls++), Is.False);

        FakeRawReliableClientEndpoint endpoint = transport.LastFakeEndpoint!;
        Assert.Multiple(() =>
        {
            Assert.That(transport.IsValid, Is.False);
            Assert.That(transport.IsStarted, Is.False);
            Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
            Assert.That(transport.EndpointWasAttachedDuringStart, Is.True,
                "the endpoint must be attached before its start is requested");
            Assert.That(transport.AttachedEndpoint, Is.Null);
            Assert.That(endpoint.StartCalls, Is.EqualTo(1));
            Assert.That(endpoint.StopCalls, Is.EqualTo(1));
            Assert.That(endpoint.LastStopReason, Is.Not.Null);
            Assert.That(handler.StoppedCount, Is.Zero);
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(userStoppedCalls, Is.Zero);
        });
    }
}
