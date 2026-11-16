using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Unreliable.RawUnreliableServerEndpoint"/>, exercised
/// through the extended public + protected surface exposed by <see cref="TestRawUnreliableServerEndpoint"/>.
/// They cover the pending-trigger hand-off and the transport-wide send budget this layer adds on top of
/// <see cref="Pontifex.Raw.Unreliable.RawUnreliableEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawUnreliableServerEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawUnreliableServerEndpoint Create(
        RecordingRawUnreliableHandler handler,
        UnionDataList trigger,
        SendBudget sendBudget,
        int max = 1024,
        long capacity = 1024 * 1024,
        int inboxMessages = 16,
        long inboxBytes = 1024 * 1024)
    {
        return new TestRawUnreliableServerEndpoint(
            handler,
            trigger,
            sendBudget,
            messageMaxByteSize: max,
            bufferCapacityBytes: capacity,
            inboxCapacityMessages: inboxMessages,
            inboxCapacityBytes: inboxBytes);
    }

    private static byte Payload(UnionDataList buffer)
    {
        return buffer.Elements[0].Alias.ByteValue;
    }

    #region Constructor

    [Test]
    public void Constructor_WithNullTrigger_Throws()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var budget = new SendBudget(1024, 16);

        Assert.Throws<ArgumentNullException>(() => new TestRawUnreliableServerEndpoint(handler, null!, budget));
    }

    [Test]
    public void Constructor_WithNullSendBudget_Throws()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 1);

        Assert.Throws<ArgumentNullException>(() => new TestRawUnreliableServerEndpoint(handler, trigger, null!));

        // The constructor rejects before taking ownership of the trigger.
        trigger.Release();
    }

    #endregion

    #region Pending trigger

    [Test]
    public void Start_WithBufferedMessages_DeliversTriggerFirst()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var budget = new SendBudget(1024, 16);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        using var ep = Create(handler, trigger, budget);
        UnionDataList firstBuffered = TestBuffers.WithByte(Memory, 1);
        UnionDataList secondBuffered = TestBuffers.WithByte(Memory, 2);

        ep.Deliver(firstBuffered);
        ep.Deliver(secondBuffered);
        ep.StartAndComplete();

        Assert.That(handler.ReceivedCount, Is.EqualTo(3));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)100));
        Assert.That(Payload(handler.Received[1]), Is.EqualTo((byte)1));
        Assert.That(Payload(handler.Received[2]), Is.EqualTo((byte)2));
    }

    [Test]
    public void Start_WhenInboxCapsAreZero_StillBuffersAndDeliversTrigger()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var budget = new SendBudget(1024, 16);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        using var ep = Create(handler, trigger, budget, inboxMessages: 0, inboxBytes: 0);

        ep.StartAndComplete();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(Payload(handler.Received[0]), Is.EqualTo((byte)100));
    }

    [Test]
    public void Stop_BeforeStart_ReleasesTriggerWithoutDelivery()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        var budget = new SendBudget(1024, 16);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        using var ep = Create(handler, trigger, budget);

        Assert.That(ep.StopOneShot(), Is.True);
        ep.DrainAll();

        Assert.That(trigger.IsAlive, Is.False);
        Assert.That(handler.StartedCount, Is.Zero);
        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(ep.StoppedReasons, Has.Count.EqualTo(1));
    }

    #endregion

    #region Shared send budget

    [Test]
    public void Send_WhenSharedBudgetAccepts_ReturnsOk()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        var budget = new SendBudget(1024, 1);
        using var ep = Create(handler, trigger, budget);
        ep.StartAndComplete();
        UnionDataList message = TestBuffers.WithByte(Memory, 7);

        Assert.That(ep.Send(message), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void Send_WhenSharedMessageBudgetRejects_ReturnsBufferOverflowAndReleases()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        var budget = new SendBudget(1024, 1);
        using var ep = Create(handler, trigger, budget);
        ep.StartAndComplete();
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);

        Assert.That(ep.Send(first), Is.EqualTo(SendResult.Ok));
        Assert.That(ep.Send(second), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(second.IsAlive, Is.False);

        ep.DrainAll();

        Assert.That(ep.CapturedSends, Has.Count.EqualTo(1));
        Assert.That(Payload(ep.CapturedSends[0]), Is.EqualTo((byte)1));
    }

    [Test]
    public void Send_WhenSharedByteBudgetRejects_ReturnsBufferOverflow()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        UnionDataList probe = TestBuffers.WithByte(Memory, 1);
        int size = probe.GetDataSize();
        probe.Release();
        var budget = new SendBudget(size * 2L, 10);
        using var ep = Create(handler, trigger, budget);
        ep.StartAndComplete();
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        UnionDataList third = TestBuffers.WithByte(Memory, 3);

        Assert.That(ep.Send(first), Is.EqualTo(SendResult.Ok));
        Assert.That(ep.Send(second), Is.EqualTo(SendResult.Ok));
        Assert.That(ep.Send(third), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(third.IsAlive, Is.False);
    }

    [Test]
    public void Send_AfterDrain_ReleasesSharedBudget()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        var budget = new SendBudget(1024, 1);
        using var ep = Create(handler, trigger, budget);
        ep.StartAndComplete();
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);

        Assert.That(ep.Send(first), Is.EqualTo(SendResult.Ok));
        ep.DrainAll();
        Assert.That(ep.Send(second), Is.EqualTo(SendResult.Ok));

        ep.DrainAll();

        Assert.That(ep.CapturedSends, Has.Count.EqualTo(2));
    }

    [Test]
    public void Send_WhenLocalBudgetOverflows_RollsSharedBudgetBack()
    {
        using var handler = new RecordingRawUnreliableHandler(Memory);
        UnionDataList trigger = TestBuffers.WithByte(Memory, 100);
        UnionDataList probe = TestBuffers.WithByte(Memory, 1);
        int size = probe.GetDataSize();
        probe.Release();
        var budget = new SendBudget(size, 1);
        using var ep = Create(handler, trigger, budget, capacity: 1);
        ep.StartAndComplete();
        UnionDataList message = TestBuffers.WithByte(Memory, 1);

        Assert.That(ep.Send(message), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(message.IsAlive, Is.False);

        // CanAccept would be false if the reservation had survived the local rollback.
        Assert.That(budget.CanAccept(size), Is.True);
    }

    #endregion
}
