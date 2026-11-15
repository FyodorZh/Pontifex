using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawUnreliableServerTransport{TFactory}"/>, exercised through
/// the extended public and protected surface of <see cref="TestRawUnreliableServerTransport"/>. Covers the
/// admission gate, route creation and trigger hand-off, factory failure handling, route removal and
/// recreation, the stop cascade, the stop-during-creation path, and the shared send-budget wiring.
/// </summary>
[Category("Transports")]
public sealed class RawUnreliableServerTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static TestRawUnreliableServerTransport Create(
        int messageMaxByteSize = 1024,
        long? sendBudgetBytes = null,
        int? sendBudgetMessages = null,
        ILogger? logger = null)
    {
        return new TestRawUnreliableServerTransport(
            "TestRawUnreliableServerTransport",
            messageMaxByteSize,
            sendBudgetBytes,
            sendBudgetMessages,
            logger ?? new Logger([]));
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static byte Payload(UnionDataList buffer)
    {
        return buffer.Elements[0].Alias.ByteValue;
    }

    private static void StartWithFactory(TestRawUnreliableServerTransport transport,
        Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> factory, out StopReason? callbackReason)
    {
        StopReason? reason = null;
        Assert.That(transport.ExposedTryInitializeServer(factory), Is.True);
        Assert.That(transport.Start(r => reason = r), Is.True);
        callbackReason = reason;
    }

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    #region Send-budget limits

    [Test]
    public void SendBudgetLimits_ByDefault_DeriveFromMessageMaxByteSize()
    {
        using var transport = Create(messageMaxByteSize: 490);
        using var other = Create(messageMaxByteSize: 1024);

        Assert.That(transport.ExposedSendBudgetMessages, Is.EqualTo(100));
        Assert.That(transport.ExposedSendBudgetBytes, Is.EqualTo(490L * 100));
        Assert.That(other.ExposedSendBudgetBytes, Is.EqualTo(1024L * 100));
    }

    [Test]
    public void Start_AfterInitialization_CreatesSharedBudgetFromConfiguredCaps()
    {
        using var transport = Create(messageMaxByteSize: 100, sendBudgetBytes: 250, sendBudgetMessages: 3);
        Assert.That(transport.ExposedSharedSendBudget, Is.Null);
        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => null), Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        SendBudget budget = transport.ExposedSharedSendBudget!;
        Assert.That(budget, Is.Not.Null);
        Assert.That(budget.CanAccept(90), Is.True);
        budget.Apply(90, 1);
        Assert.That(budget.CanAccept(90), Is.True);
        budget.Apply(90, 1);
        Assert.That(budget.CanAccept(90), Is.False);
    }

    [Test]
    public void Start_CreatesSharedBudgetHonouringMessageCap()
    {
        using var transport = Create(sendBudgetBytes: 100_000, sendBudgetMessages: 2);
        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => null), Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        SendBudget budget = transport.ExposedSharedSendBudget!;
        budget.Apply(1, 1);
        budget.Apply(1, 1);
        Assert.That(budget.CanAccept(1), Is.False);
    }

    [Test]
    public void SharedSendBudget_IsPassedToEveryEndpointWithTheConfiguredLocalCap()
    {
        using var transport = Create(sendBudgetBytes: 4096, sendBudgetMessages: 4);
        using var handler = new RecordingRawUnreliableHandler(Memory);
        StartWithFactory(transport, (source, trigger) => handler, out _);

        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        transport.ExposedOnCarrierInbound(new StringEndPoint("first"), first);
        transport.ExposedOnCarrierInbound(new StringEndPoint("second"), second);

        Assert.That(transport.CreatedSendBudgets, Has.Count.EqualTo(2));
        Assert.That(transport.CreatedSendBudgets[0], Is.SameAs(transport.ExposedSharedSendBudget));
        Assert.That(transport.CreatedSendBudgets[1], Is.SameAs(transport.ExposedSharedSendBudget));
        Assert.That(transport.CreatedLocalBudgetCapacities, Is.EqualTo(new[] { 4096L, 4096L }));
        Assert.That(transport.ExposedSendBudgetBytes, Is.EqualTo(4096));
        Assert.That(transport.ExposedSendBudgetMessages, Is.EqualTo(4));
        transport.StopAndDrainAll();
    }

    #endregion

    #region Initialization and start

    [Test]
    public void Start_BeforeInitialization_FailsWithoutStartingCarrierOrCreatingBudget()
    {
        using var transport = Create();

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(transport.ExposedSharedSendBudget, Is.Null);
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void TryInitializeServer_CalledTwice_ReturnsFalseAndKeepsFirstFactory()
    {
        using var transport = Create();
        using var first = new RecordingRawUnreliableHandler(Memory);
        using var second = new RecordingRawUnreliableHandler(Memory);

        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => first), Is.True);
        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => second), Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);
        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.CreatedHandlers[0], Is.SameAs(first));
        Assert.That(transport.CreatedHandlers[0], Is.Not.SameAs(second));
        transport.StopAndDrainAll();
    }

    [Test]
    public void TryInitializeServer_AfterStartAttempt_ReturnsFalse()
    {
        using var transport = Create();
        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => null), Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
    }

    [Test]
    public void Start_WhenCarrierReturnsFalse_FailsWithoutStoppingCarrier()
    {
        using var transport = Create();
        transport.StartCarrierBehavior = static () => false;
        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => null), Is.True);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.ExposedSharedSendBudget, Is.Null);
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void Start_WhenCarrierThrows_LogsAndFailsWithoutStoppingCarrier()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(logger: new Logger([logConsumer]));
        transport.StartCarrierException = new InvalidOperationException("server carrier failed");
        Assert.That(transport.ExposedTryInitializeServer((source, trigger) => null), Is.True);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.ExposedSharedSendBudget, Is.Null);
        Assert.That(logConsumer.Messages, Has.Some.Contains("server carrier failed"));
    }

    #endregion

    #region Route creation

    [Test]
    public void OnCarrierInbound_WhenRouteIsNew_CreatesEndpointWithTriggerAndStartsIt()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        StartWithFactory(transport, (source, trigger) => handler, out _);
        var source = new StringEndPoint("source");
        UnionDataList trigger = TestBuffers.WithByte(Memory, 7);

        transport.ExposedOnCarrierInbound(source, trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.FactorySources[0], Is.SameAs(source));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.CreatedSources[0], Is.SameAs(source));
        Assert.That(transport.CreatedTriggers[0], Is.SameAs(trigger));
        Assert.That(transport.ExposedRouteCount, Is.EqualTo(1));
        Assert.That(trigger.IsAlive, Is.True);

        var endpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        Assert.That(endpoint.IsRunning, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);

        endpoint.DrainAll();

        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.StartedEndpoint, Is.SameAs(endpoint));
        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(handler.ReceivedBeforeStartedCount, Is.Zero);
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)7));
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(endpoint.IsRunning, Is.True);
        transport.StopAndDrainAll();
    }

    [Test]
    public void OnCarrierInbound_WhenRouteExists_DeliversToItWithoutAnotherFactoryCall()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        StartWithFactory(transport, (source, trigger) => handler, out _);
        var source = new StringEndPoint("source");
        transport.ExposedOnCarrierInbound(source, TestBuffers.WithByte(Memory, 1));
        var endpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        endpoint.DrainAll();

        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        transport.ExposedOnCarrierInbound(source, second);

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedRouteCount, Is.EqualTo(1));
        Assert.That(handler.ReceivedCount, Is.EqualTo(1));

        endpoint.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(2));
        Assert.That(Payload(handler.Received[1]), Is.EqualTo((byte)2));
        Assert.That(second.IsAlive, Is.False);
        transport.StopAndDrainAll();
    }

    [Test]
    public void OnCarrierInbound_WhenFactoryReturnsNull_ReleasesTriggerAndPublishesNoRoute()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(logger: new Logger([logConsumer]));
        StartWithFactory(transport, (source, trigger) => null, out _);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Is.Empty);
    }

    [Test]
    public void OnCarrierInbound_WhenNoFactoryIsBound_ReleasesTriggerAndPublishesNoRoute()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(logger: new Logger([logConsumer]));
        Assert.That(transport.ExposedTryInitializeServer(null!), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Is.Empty);
    }

    [Test]
    public void OnCarrierInbound_WhenFactoryThrows_LogsReleasesTriggerAndPublishesNoRoute()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(logger: new Logger([logConsumer]));
        transport.FactoryBehavior = (source, trigger) => throw new InvalidOperationException("factory failed");
        StartWithFactory(transport, (source, trigger) => null, out _);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Has.Some.Contains("factory failed"));
    }

    [Test]
    public void OnCarrierInbound_WhenCreateEndpointThrows_LogsReleasesTriggerAndPublishesNoRoute()
    {
        var logConsumer = new RecordingLogConsumer();
        using var transport = Create(logger: new Logger([logConsumer]));
        using var handler = new RecordingRawUnreliableHandler(Memory);
        transport.CreateEndpointException = new InvalidOperationException("endpoint creation failed");
        StartWithFactory(transport, (source, trigger) => handler, out _);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Has.Some.Contains("endpoint creation failed"));
    }

    [Test]
    public void OnCarrierInbound_WithNullArguments_Throws()
    {
        using var transport = Create();
        StartWithFactory(transport, (source, trigger) => null, out _);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        try
        {
            Assert.Throws<ArgumentNullException>(() => transport.ExposedOnCarrierInbound(null!, trigger));
            Assert.Throws<ArgumentNullException>(() =>
                transport.ExposedOnCarrierInbound(new StringEndPoint("source"), null!));
        }
        finally
        {
            trigger.Release();
        }

        Assert.That(transport.ExposedRouteCount, Is.Zero);
    }

    [Test]
    public void OnCarrierInbound_AfterStop_ReleasesTriggerWithoutCreatingRoute()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        StartWithFactory(transport, (source, trigger) => handler, out _);
        Assert.That(transport.Stop(Reason()), Is.True);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.Zero);
        Assert.That(transport.CreateEndpointCalls, Is.Zero);
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(transport.ExposedIsStopping, Is.True);
    }

    #endregion

    #region Concurrency

    [Test]
    [Category("Transports.Concurrency")]
    public void OnCarrierInbound_ConcurrentTriggersForOneSource_InvokeFactoryOnce()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var startGate = new ManualResetEventSlim(false);
        using var ready = new CountdownEvent(2);
        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);
        var source = new StringEndPoint("source");
        int factoryCalls = 0;
        Exception? threadFailure = null;

        transport.FactoryBehavior = (s, t) =>
        {
            Interlocked.Increment(ref factoryCalls);
            factoryEntered.Set();

            if (!releaseFactory.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The factory gate was not released.");
            }

            return handler;
        };
        StartWithFactory(transport, (s, t) => handler, out _);

        UnionDataList firstTrigger = TestBuffers.WithByte(Memory, 1);
        UnionDataList secondTrigger = TestBuffers.WithByte(Memory, 2);

        Thread Contender(UnionDataList trigger) => new(() =>
        {
            ready.Signal();
            startGate.Wait();

            try
            {
                transport.ExposedOnCarrierInbound(source, trigger);
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        });

        var first = Contender(firstTrigger);
        var second = Contender(secondTrigger);
        first.IsBackground = true;
        second.IsBackground = true;

        try
        {
            first.Start();
            second.Start();

            Assert.That(ready.Wait(TimeSpan.FromSeconds(10)), Is.True,
                "The contenders did not become ready.");
            Assert.That(transport.ExposedRouteCount, Is.Zero);

            startGate.Set();

            Assert.That(factoryEntered.Wait(TimeSpan.FromSeconds(10)), Is.True,
                "Neither contender entered the factory.");
        }
        finally
        {
            startGate.Set();
            releaseFactory.Set();
        }

        Assert.That(first.Join(TimeSpan.FromSeconds(10)), Is.True, "The first contender did not finish.");
        Assert.That(second.Join(TimeSpan.FromSeconds(10)), Is.True, "The second contender did not finish.");

        Assert.That(threadFailure, Is.Null);
        Assert.That(factoryCalls, Is.EqualTo(1));
        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedRouteCount, Is.EqualTo(1));

        var endpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        endpoint.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(2));
        Assert.That(handler.ReceivedBeforeStartedCount, Is.Zero);
        transport.StopAndDrainAll();
    }

    #endregion

    #region Route removal and recreation

    [Test]
    public void EndpointStopped_RemovesRouteAndAllowsRecreation()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        StartWithFactory(transport, (source, trigger) => handler, out _);
        var source = new StringEndPoint("source");
        transport.ExposedOnCarrierInbound(source, TestBuffers.WithByte(Memory, 1));
        var firstEndpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        firstEndpoint.DrainAll();
        var reason = Reason();

        Assert.That(firstEndpoint.StopOneShot(reason), Is.True);
        firstEndpoint.DrainAll();

        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.SameAs(reason));

        transport.ExposedOnCarrierInbound(source, TestBuffers.WithByte(Memory, 2));

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(2));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(2));
        Assert.That(transport.ExposedRouteCount, Is.EqualTo(1));
        Assert.That(transport.CreatedEndpoints[1], Is.Not.SameAs(firstEndpoint));
        transport.StopAndDrainAll();
    }

    [Test]
    public void EndpointStopped_RemovesOnlyItsOwnRoute()
    {
        using var transport = Create();
        using var firstHandler = new RecordingRawUnreliableHandler(Memory);
        using var secondHandler = new RecordingRawUnreliableHandler(Memory);
        var firstSource = new StringEndPoint("first");
        var secondSource = new StringEndPoint("second");
        StartWithFactory(transport,
            (source, trigger) => ReferenceEquals(source, firstSource) ? firstHandler : secondHandler, out _);
        transport.ExposedOnCarrierInbound(firstSource, TestBuffers.WithByte(Memory, 1));
        transport.ExposedOnCarrierInbound(secondSource, TestBuffers.WithByte(Memory, 2));
        var firstEndpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        var secondEndpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[1];
        firstEndpoint.DrainAll();
        secondEndpoint.DrainAll();

        firstEndpoint.StopOneShot(Reason());
        firstEndpoint.DrainAll();

        Assert.That(transport.ExposedRouteCount, Is.EqualTo(1));

        transport.ExposedOnCarrierInbound(secondSource, TestBuffers.WithByte(Memory, 3));
        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(2));
        secondEndpoint.DrainAll();
        Assert.That(secondHandler.ReceivedCount, Is.EqualTo(2));

        transport.ExposedOnCarrierInbound(firstSource, TestBuffers.WithByte(Memory, 4));
        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(3));
        Assert.That(transport.ExposedRouteCount, Is.EqualTo(2));
        transport.StopAndDrainAll();
    }

    #endregion

    #region Stop

    [Test]
    public void Stop_WhenRoutesExist_StopsEveryLiveEndpointWithTransportReason()
    {
        using var transport = Create();
        using var firstHandler = new RecordingRawUnreliableHandler(Memory);
        using var secondHandler = new RecordingRawUnreliableHandler(Memory);
        var firstSource = new StringEndPoint("first");
        var secondSource = new StringEndPoint("second");
        StopReason? callbackReason = null;
        Assert.That(transport.ExposedTryInitializeServer(
            (source, trigger) => ReferenceEquals(source, firstSource) ? firstHandler : secondHandler), Is.True);
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        transport.ExposedOnCarrierInbound(firstSource, TestBuffers.WithByte(Memory, 1));
        transport.ExposedOnCarrierInbound(secondSource, TestBuffers.WithByte(Memory, 2));
        var firstEndpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        var secondEndpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[1];
        firstEndpoint.DrainAll();
        secondEndpoint.DrainAll();
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(transport.IsStoppingDuringStopCarrier, Is.True);
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(callbackReason, Is.SameAs(reason));

        firstEndpoint.DrainAll();
        secondEndpoint.DrainAll();

        Assert.That(firstHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(firstHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(secondHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(secondHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(firstEndpoint.StoppedReasons, Is.EqualTo(new[] { reason }));
        Assert.That(secondEndpoint.StoppedReasons, Is.EqualTo(new[] { reason }));
    }

    [Test]
    public void Stop_WhileEndpointIsBeingCreated_RemovesRouteAndStopsUnstartedEndpoint()
    {
        using var transport = Create();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var reason = Reason();
        transport.FactoryBehavior = (source, trigger) =>
        {
            transport.Stop(reason);
            return handler;
        };
        StartWithFactory(transport, (source, trigger) => handler, out _);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 3);

        transport.ExposedOnCarrierInbound(new StringEndPoint("source"), trigger);

        Assert.That(transport.InvokeFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedRouteCount, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));

        var endpoint = (TestRawUnreliableServerEndpoint)transport.CreatedEndpoints[0];
        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.True);

        endpoint.DrainAll();

        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(endpoint.StoppedReasons, Has.Count.EqualTo(1));
    }

    #endregion
}
