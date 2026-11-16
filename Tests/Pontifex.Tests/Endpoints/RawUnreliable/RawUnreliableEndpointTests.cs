using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Unreliable.RawUnreliableEndpoint"/>, exercised through
/// the extended public + protected surface exposed by <see cref="TestRawUnreliableEndpoint"/>. They cover the
/// pre-start inbox, the flush-driven start notification, the one-shot stop entry, and the inbound/outbound
/// admission deltas this layer introduces over <see cref="Pontifex.Raw.RawEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawUnreliableEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawUnreliableEndpoint Create(
        RecordingRawUnreliableHandler handler,
        int max = 1024,
        long capacity = 1024 * 1024,
        int inboxMessages = 16,
        long inboxBytes = 1024 * 1024,
        ILogger? log = null)
    {
        return new TestRawUnreliableEndpoint(
            handler,
            messageMaxByteSize: max,
            bufferCapacityBytes: capacity,
            inboxCapacityMessages: inboxMessages,
            inboxCapacityBytes: inboxBytes,
            log: log);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static byte Payload(UnionDataList buffer)
    {
        return buffer.Elements[0].Alias.ByteValue;
    }

    private static IRawUnreliableEndpointConformanceControl Control(TestRawUnreliableEndpoint ep)
    {
        var controls = new List<IControl>();
        ep.GetControls(controls);
        return controls.OfType<IRawUnreliableEndpointConformanceControl>().Single();
    }

    #region Constructor

    [Test]
    public void Constructor_WithNegativeInboxMessageCapacity_Throws()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);

        Assert.Throws<ArgumentOutOfRangeException>(() => Create(handler, inboxMessages: -1));
    }

    [Test]
    public void Constructor_WithNegativeInboxByteCapacity_Throws()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);

        Assert.Throws<ArgumentOutOfRangeException>(() => Create(handler, inboxBytes: -1));
    }

    #endregion

    #region Inbox

    [Test]
    public void Deliver_BeforeStart_BuffersWithoutDelivery()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList message = TestBuffers.WithByte(Memory, 1);

        ep.Deliver(message);

        Assert.That(ep.IsRunning, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(message.IsAlive, Is.True);
    }

    [Test]
    public void Deliver_BeforeStart_DeliversBufferedMessagesInFifoOrderAfterStart()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        UnionDataList third = TestBuffers.WithByte(Memory, 3);
        int pendingActionsAtStart = -1;
        handler.OnStartedProbe = () => pendingActionsAtStart = ep.PendingActions;

        ep.Deliver(first);
        ep.Deliver(second);
        ep.Deliver(third);
        ep.StartAndComplete();

        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.ReceivedCount, Is.EqualTo(3));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)1));
        Assert.That(Payload(handler.Received[1]), Is.EqualTo((byte)2));
        Assert.That(Payload(handler.Received[2]), Is.EqualTo((byte)3));
        Assert.That(handler.ReceivedAfterStartedCount, Is.EqualTo(3));
        Assert.That(handler.ReceivedBeforeStartedCount, Is.Zero);

        // The flush raises the start notification before it schedules any buffered delivery. No delivery
        // action can be pending inside the notification; if the drain ran first, all three would be queued.
        Assert.That(pendingActionsAtStart, Is.Zero);
    }

    [Test]
    public void Deliver_AfterFlush_DeliversWithoutBuffering()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        UnionDataList message = TestBuffers.WithByte(Memory, 7);

        ep.Deliver(message);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)7));
        Assert.That(message.IsAlive, Is.False);
    }

    [Test]
    public void Deliver_WhenOnReceivedThrows_KeepsEndpointRunningAndDeliversLaterMessages()
    {
        using var handler = new ThrowingOnReceivedHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        ep.Deliver(TestBuffers.WithByte(Memory, 1));
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(handler.AttemptedCount, Is.EqualTo(1));
            Assert.That(ep.IsRunning, Is.True);
        });

        ep.Deliver(TestBuffers.WithByte(Memory, 2));
        ep.DrainAll();

        // An unreliable handler exception abandons only that delivery. The endpoint and the transport stay
        // running, and later deliveries are still eligible.
        Assert.Multiple(() =>
        {
            Assert.That(handler.AttemptedCount, Is.EqualTo(2));
            Assert.That(ep.IsRunning, Is.True);
        });
    }

    [Test]
    public void Deliver_BetweenFinishStartAndFlush_StaysBehindBufferedMessages()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList buffered = TestBuffers.WithByte(Memory, 1);
        UnionDataList late = TestBuffers.WithByte(Memory, 2);

        ep.Deliver(buffered);
        ep.Start();
        ep.DrainOne();
        ep.DrainOne();

        Assert.That(ep.IsRunning, Is.True);
        Assert.That(handler.StartedCount, Is.Zero);

        ep.Deliver(late);
        ep.DrainAll();

        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.ReceivedCount, Is.EqualTo(2));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)1));
        Assert.That(Payload(handler.Received[1]), Is.EqualTo((byte)2));
    }

    [Test]
    public void Deliver_WhenMessageCapReached_DropsSilentlyAndReleases()
    {
        var logConsumer = new RecordingLogConsumer();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler, inboxMessages: 2, log: new Logger([logConsumer]));
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        UnionDataList third = TestBuffers.WithByte(Memory, 3);
        UnionDataList fourth = TestBuffers.WithByte(Memory, 4);

        ep.Deliver(first);
        ep.Deliver(second);
        ep.Deliver(third);
        ep.Deliver(fourth);

        Assert.That(third.IsAlive, Is.False);
        Assert.That(fourth.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Is.Empty);

        ep.StartAndComplete();

        Assert.That(handler.ReceivedCount, Is.EqualTo(2));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)1));
        Assert.That(Payload(handler.Received[1]), Is.EqualTo((byte)2));
    }

    [Test]
    public void Deliver_WhenByteCapReached_DropsSilentlyAndReleases()
    {
        var logConsumer = new RecordingLogConsumer();
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        int size = first.GetDataSize();
        using var ep = Create(handler, inboxBytes: size, log: new Logger([logConsumer]));
        UnionDataList second = TestBuffers.WithByte(Memory, 2);

        ep.Deliver(first);
        ep.Deliver(second);

        Assert.That(first.IsAlive, Is.True);
        Assert.That(second.IsAlive, Is.False);
        Assert.That(logConsumer.Messages, Is.Empty);

        ep.StartAndComplete();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)1));
    }

    [Test]
    public void Deliver_AfterStop_ReleasesWithoutDelivery()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        ep.StopOneShot(Reason());
        ep.DrainAll();
        UnionDataList message = TestBuffers.WithByte(Memory, 1);

        ep.Deliver(message);

        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(message.IsAlive, Is.False);
    }

    #endregion

    #region Start

    [Test]
    public void Start_RaisesOnStartedExactlyOnce()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);

        ep.StartAndComplete();
        ep.StopOneShot(Reason());
        ep.DrainAll();

        Assert.That(handler.StartedCount, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenOnStartedThrows_FailsHardWithoutHandlerStopAndReleasesInbox()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory) { ThrowOnStarted = true };
        using var ep = Create(handler);
        UnionDataList buffered = TestBuffers.WithByte(Memory, 1);
        ep.Deliver(buffered);

        ep.StartAndComplete();

        Assert.That(handler.StartedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(buffered.IsAlive, Is.False);
        Assert.That(ep.IsRunning, Is.False);
        Assert.That(ep.StoppedReasons, Has.Count.EqualTo(1));
        Assert.That(ep.StoppedReasons[0], Is.TypeOf<ExceptionFail>());
    }

    #endregion

    #region Stop

    [Test]
    public void Stop_BeforeStart_ReleasesBufferedMessagesWithoutDelivery()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        ep.Deliver(first);
        ep.Deliver(second);

        Assert.That(ep.StopOneShot(Reason()), Is.True);
        ep.DrainAll();

        Assert.That(first.IsAlive, Is.False);
        Assert.That(second.IsAlive, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(ep.StoppedReasons, Has.Count.EqualTo(1));
    }

    [Test]
    public void Stop_AfterStart_NotifiesHandlerOnceAndRaisesStoppedOnce()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        var reason = Reason();

        ep.StopOneShot(reason);
        ep.DrainAll();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(ep.StoppedReasons, Has.Count.EqualTo(1));
        Assert.That(ep.StoppedReasons[0], Is.SameAs(reason));
        Assert.That(ep.IsValid, Is.False);
    }

    [Test]
    public void Stop_WhileStarting_SuppressesOnStartedAndOnStopped()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        var reason = Reason();
        var gate = Control(ep).BeforeHandlerStoppedGate;
        gate.Arm(requiredHits: 100);

        try
        {
            ep.Start();
            ep.DrainOne();

            Assert.That(ep.StopOneShot(reason), Is.True);
            ep.DrainAll();

            Assert.That(handler.StartedCount, Is.Zero);
            Assert.That(handler.StoppedCount, Is.Zero);
            Assert.That(ep.StoppedReasons, Has.Count.EqualTo(1));
            Assert.That(ep.StoppedReasons[0], Is.SameAs(reason));
            Assert.That(ep.StoppedReasons[0], Is.Not.TypeOf<ExceptionFail>());
            Assert.That(ep.IsRunning, Is.False);

            // BeginStop is hit once per stop transition and the gate is only hit there, so a single hit
            // proves the promoted stop ran exactly one BeginStop (a second one would fail hard on FinishStop).
            Assert.That(gate.HitCount, Is.EqualTo(98));
        }
        finally
        {
            gate.Reset();
        }
    }

    [Test]
    public void Stop_FirstCallReturnsTrue_SubsequentCallsReturnFalse()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        Assert.That(ep.StopOneShot(Reason()), Is.True);
        Assert.That(ep.IsValid, Is.False);
        Assert.That(ep.StopOneShot(Reason()), Is.False);

        ep.DrainAll();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_NullReason_MapsToUnknownFromTransportName()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        Assert.That(ep.StopOneShot(), Is.True);
        ep.DrainAll();

        Assert.That(handler.StoppedReason, Is.TypeOf<Unknown>());
        Assert.That(handler.StoppedReason!.Source, Is.EqualTo("TestTransport"));
        Assert.That(ep.StoppedReasons[0], Is.TypeOf<Unknown>());
    }

    [Test]
    public void IsValid_TracksIsRunning()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.IsValid, Is.False);
        Assert.That(ep.IsValid, Is.EqualTo(ep.IsRunning));

        ep.StartAndComplete();

        Assert.That(ep.IsValid, Is.True);
        Assert.That(ep.IsValid, Is.EqualTo(ep.IsRunning));

        ep.StopOneShot(Reason());

        Assert.That(ep.IsValid, Is.False);
        Assert.That(ep.IsValid, Is.EqualTo(ep.IsRunning));
    }

    #endregion

    #region Send

    [Test]
    public void Send_Null_ReturnsInvalidMessage()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        Assert.That(ep.Send(null!), Is.EqualTo(SendResult.InvalidMessage));
    }

    [Test]
    public void Send_WhenNotStarted_MapsNotConnectedToError()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.Error));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_AboveMessageMaxByteSize_ReturnsMessageTooBig()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler, max: 8);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithPayload(Memory, 32);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(8));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_ExceedingLocalByteBudget_ReturnsBufferOverflow()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler, capacity: 2);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(2));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(buffer.IsAlive, Is.False);
    }

    #endregion

    #region Conformance gates

    [Test]
    public void Start_WithBufferedMessage_HitsAfterReceivedGateExactlyOnce()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList buffered = TestBuffers.WithByte(Memory, 1);
        ep.Deliver(buffered);
        var gate = Control(ep).AfterReceivedGate;
        gate.Arm(requiredHits: 100);

        try
        {
            ep.StartAndComplete();

            Assert.That(handler.ReceivedCount, Is.EqualTo(1));

            // Arm(100) leaves 99 free hits, and the quota is far above the delivery count, so the gate never
            // blocks. One buffered delivery consumes exactly one hit; a bypass would leave 99 and a double hit
            // would leave 97.
            Assert.That(gate.HitCount, Is.EqualTo(98));
        }
        finally
        {
            gate.Reset();
        }
    }

    [Test]
    public void Start_WithTwoBufferedMessages_HitsAfterReceivedGateOncePerMessage()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        ep.Deliver(first);
        ep.Deliver(second);
        var gate = Control(ep).AfterReceivedGate;
        gate.Arm(requiredHits: 100);

        try
        {
            ep.StartAndComplete();

            Assert.That(handler.ReceivedCount, Is.EqualTo(2));
            Assert.That(gate.HitCount, Is.EqualTo(97));
        }
        finally
        {
            gate.Reset();
        }
    }

    #endregion

    #region Controls

    [Test]
    public void GetControls_ExposesEndpointConformanceControl()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        var controls = new List<IControl>();

        ep.GetControls(controls);

        Assert.That(controls, Has.Count.EqualTo(1));
        Assert.That(controls[0], Is.InstanceOf<IRawUnreliableEndpointConformanceControl>());
        Assert.That(controls[0], Is.TypeOf<RawUnreliableEndpointConformanceControl>());
    }

    [Test]
    public void GetControls_HonoursPredicate()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        using var ep = Create(handler);
        var rejected = new List<IControl>();
        var accepted = new List<IControl>();

        ep.GetControls(rejected, _ => false);
        ep.GetControls(accepted, control => control is IRawUnreliableEndpointConformanceControl);

        Assert.That(rejected, Is.Empty);
        Assert.That(accepted, Has.Count.EqualTo(1));
    }

    #endregion

    private sealed class ThrowingOnReceivedHandler : RecordingRawUnreliableHandler
    {
        public ThrowingOnReceivedHandler(IMemoryRental memory) : base(memory)
        {
        }

        public int AttemptedCount { get; private set; }

        public override void OnReceived(UnionDataList receivedBuffer)
        {
            AttemptedCount++;
            try
            {
                throw new InvalidOperationException("Expected conformance test exception.");
            }
            finally
            {
                receivedBuffer.Release();
            }
        }
    }
}
