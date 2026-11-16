using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawUnreliableClientTransport"/>, exercised through the
/// extended public and protected surface of <see cref="TestRawUnreliableClientTransport"/>. Covers the
/// initialization gate, endpoint creation and ownership during the start, the endpoint-stop/start race,
/// the no-endpoint fallback notification, and the carrier stop ordering.
/// </summary>
[Category("Transports")]
public sealed class RawUnreliableClientTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static TestRawUnreliableClientTransport Create(ILogger? logger = null)
    {
        return new TestRawUnreliableClientTransport(
            "TestRawUnreliableClientTransport",
            logger ?? new Logger([]));
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    #region Init

    [Test]
    public void Init_WhenConstructed_BindsHandlerAndLeavesTransportUnstarted()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);

        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Handler, Is.SameAs(handler));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
    }

    [Test]
    public void Init_CalledTwice_ReturnsFalseAndKeepsFirstHandler()
    {
        using var transport = Create();
        using var first = new RecordingRawUnreliableHandler(Memory);
        using var second = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(first), Is.True);

        Assert.That(transport.Init(second), Is.False);

        Assert.That(transport.Handler, Is.SameAs(first));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
    }

    [Test]
    public void Init_WithNullHandler_ThrowsWithoutChangingTheTransport()
    {
        using var transport = Create();

        Assert.Throws<ArgumentNullException>(() => transport.Init(null!));

        Assert.That(transport.Handler, Is.Null);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.Start(_ => { }), Is.False);
    }

    [Test]
    public void Init_AfterStartAttempt_ReturnsFalseWithoutBindingHandler()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.Handler, Is.Null);
        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
    }

    [Test]
    public void Init_AfterStart_ReturnsFalse()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(handler), Is.False);

        Assert.That(transport.Handler, Is.SameAs(handler));
    }

    #endregion

    #region Start

    [Test]
    public void Start_WithoutInit_FailsWithoutStartingCarrierOrCreatingEndpoint()
    {
        using var transport = Create();
        bool callbackCalled = false;

        Assert.That(transport.Start(_ => callbackCalled = true), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(callbackCalled, Is.False);
    }

    [Test]
    public void Start_WhenInitialized_CreatesAttachesAndStartsOneEndpoint()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.LastCreateEndpointHandler, Is.SameAs(handler));

        RawUnreliableEndpoint endpoint = transport.CreatedEndpoints[0];
        Assert.That(transport.ExposedAttachedEndpoint, Is.SameAs(endpoint));
        // The endpoint started: its start action is queued on its manual queue.
        Assert.That(endpoint.IsRunning, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);

        ((TestRawUnreliableEndpoint)endpoint).DrainAll();

        Assert.That(endpoint.IsRunning, Is.True);
        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.StartedEndpoint, Is.SameAs(endpoint));
        transport.StopAndDrainAll();
    }

    [Test]
    public void Start_CalledTwice_CreatesEndpointOnlyOnce()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        transport.StopAndDrainAll();
    }

    [Test]
    public void Start_WhenCarrierFails_StopsCarrierAndNotifiesHandlerWithoutEndpoint()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        transport.StartCarrierBehavior = static () => false;
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r => callbackReason = r), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
    }

    [Test]
    public void Start_WhenCarrierThrows_LogsAndReturnsFalse()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(new Logger([logConsumer]));
        using var handler = new RecordingRawUnreliableHandler(Memory);
        transport.StartCarrierException = new InvalidOperationException("carrier start failed");
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(logConsumer.Messages, Has.Some.Contains("carrier start failed"));
    }

    [Test]
    public void Start_WhenCreateEndpointThrows_LogsAndReturnsFalse()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(new Logger([logConsumer]));
        using var handler = new RecordingRawUnreliableHandler(Memory);
        transport.CreateEndpointException = new InvalidOperationException("endpoint factory failed");
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(logConsumer.Messages, Has.Some.Contains("endpoint factory failed"));
    }

    [Test]
    public void Start_WhenNoEndpointWasCreated_NotifiesHandlerStoppedDirectly()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var exception = new InvalidOperationException("endpoint factory failed");
        transport.CreateEndpointException = exception;
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r => callbackReason = r), Is.False);

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(((ExceptionFail)handler.StoppedReason!).Exception, Is.SameAs(exception));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(handler.StoppedReason));
        Assert.That(callbackReason, Is.SameAs(handler.StoppedReason));
    }

    [Test]
    public void Start_WhenEndpointStopsDuringStart_ReplaysStopAfterStart()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var reason = Reason();
        var endpoint = new StopDuringStartEndpoint(handler, reason);
        transport.EndpointFactory = _ => endpoint;
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r => callbackReason = r), Is.True);

        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(endpoint.StoppedReasons, Is.EqualTo(new[] { reason }));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
    }

    [Test]
    public void Start_WhenTransportStopsDuringEndpointCreation_StopsUnstartedEndpointAndReturnsFalse()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var reason = Reason();
        TestRawUnreliableEndpoint? endpoint = null;
        transport.EndpointFactory = h =>
        {
            transport.Stop(reason);
            endpoint = new TestRawUnreliableEndpoint(h, transportName: transport.Name);
            return endpoint;
        };
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;

        Assert.That(transport.Start(r => callbackReason = r), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        // The no-endpoint fallback ran before the endpoint existed.
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.StartedCount, Is.Zero);

        endpoint!.DrainAll();

        Assert.That(endpoint.IsRunning, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(endpoint.StoppedReasons, Has.Count.EqualTo(1));
        Assert.That(endpoint.StoppedReasons[0], Is.TypeOf<TextFail>());
        endpoint.Dispose();
    }

    #endregion

    #region Endpoint stop and carrier ordering

    [Test]
    public void EndpointStopped_AfterStart_StopsTransportWithSameReason()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        var endpoint = (TestRawUnreliableEndpoint)transport.CreatedEndpoints[0];
        endpoint.DrainAll();
        var reason = Reason();

        Assert.That(endpoint.StopOneShot(reason), Is.True);
        endpoint.DrainAll();

        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Stop_WhenStarted_StopsEndpointBeforeCarrier()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        StopReason? callbackReason = null;
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        var endpoint = (TestRawUnreliableEndpoint)transport.CreatedEndpoints[0];
        endpoint.DrainAll();
        var reason = Reason();
        bool? secondStopResult = null;
        transport.StopCarrierProbe = () => secondStopResult = endpoint.StopOneShot();

        Assert.That(transport.Stop(reason), Is.True);

        // A false from the one-shot entry inside StopCarrier proves the transport had already latched the
        // endpoint stop before it stopped the carrier.
        Assert.That(secondStopResult, Is.False);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.ExposedAttachedEndpoint, Is.Null);

        endpoint.DrainAll();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Stop_WhenStarted_DoesNotNotifyHandlerWhenAnEndpointOwnsTheNotification()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        var endpoint = (TestRawUnreliableEndpoint)transport.CreatedEndpoints[0];
        endpoint.DrainAll();

        Assert.That(transport.Stop(Reason()), Is.True);

        // The endpoint has not run its stop transition yet, so the handler notification is still pending
        // with the endpoint and the transport must not add its own.
        Assert.That(handler.StoppedCount, Is.Zero);
        transport.StopAndDrainAll();
    }

    #endregion
}
