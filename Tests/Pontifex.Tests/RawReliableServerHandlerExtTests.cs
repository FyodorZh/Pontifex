using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests;

// Loose fixture for the shared server-handler extensions. The Test(...) method is not covered here:
// it is a Debug-only instrumentation seam, not a contract change, and its invariant checker reports
// through a caller-supplied callback instead of producing observable behaviour.
[TestFixture]
public sealed class RawReliableServerHandlerExtTests
{
    private static readonly StopReason Reason = new("test", "reason");

    #region GetSafe(IRawReliableServerHandler) forwarding

    [Test]
    public void GetSafeBase_OnConnected_ForwardsEndpoint()
    {
        Exception? reported = null;
        var core = new RecordingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);
        var endpoint = new StubReliableEndpoint();

        safe.OnConnected(endpoint);

        Assert.That(core.ConnectedCount, Is.EqualTo(1));
        Assert.That(core.ConnectedEndPoint, Is.SameAs(endpoint));
        Assert.That(core.DisconnectedCount, Is.Zero);
        Assert.That(core.ReceivedCount, Is.Zero);
        Assert.That(core.StoppedCount, Is.Zero);
        Assert.That(reported, Is.Null);
    }

    [Test]
    public void GetSafeBase_OnDisconnected_ForwardsReason()
    {
        Exception? reported = null;
        var core = new RecordingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);

        safe.OnDisconnected(Reason);

        Assert.That(core.DisconnectedCount, Is.EqualTo(1));
        Assert.That(core.DisconnectedReason, Is.SameAs(Reason));
        Assert.That(core.ConnectedCount, Is.Zero);
        Assert.That(core.ReceivedCount, Is.Zero);
        Assert.That(core.StoppedCount, Is.Zero);
        Assert.That(reported, Is.Null);
    }

    [Test]
    public void GetSafeBase_OnReceived_ForwardsBuffer()
    {
        Exception? reported = null;
        var core = new RecordingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);
        UnionDataList buffer = AcquireBuffer();
        try
        {
            safe.OnReceived(buffer);

            Assert.That(core.ReceivedCount, Is.EqualTo(1));
            Assert.That(core.ReceivedBuffer, Is.SameAs(buffer));
        }
        finally
        {
            buffer.Release();
        }

        Assert.That(core.ConnectedCount, Is.Zero);
        Assert.That(core.DisconnectedCount, Is.Zero);
        Assert.That(core.StoppedCount, Is.Zero);
        Assert.That(reported, Is.Null);
    }

    [Test]
    public void GetSafeBase_OnStopped_ForwardsReason()
    {
        Exception? reported = null;
        var core = new RecordingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);

        safe.OnStopped(Reason);

        Assert.That(core.StoppedCount, Is.EqualTo(1));
        Assert.That(core.StoppedReason, Is.SameAs(Reason));
        Assert.That(core.ConnectedCount, Is.Zero);
        Assert.That(core.DisconnectedCount, Is.Zero);
        Assert.That(core.ReceivedCount, Is.Zero);
        Assert.That(reported, Is.Null);
    }

    #endregion

    #region GetSafe(IRawReliableServerHandler) exceptions

    [Test]
    public void GetSafeBase_OnConnected_WhenHandlerThrows_PropagatesAndDoesNotCallOnFail()
    {
        Exception? reported = null;
        var core = new ThrowingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);

        var actual = Assert.Throws<InvalidOperationException>(() => safe.OnConnected(new StubReliableEndpoint()));

        Assert.That(actual, Is.SameAs(core.Error));
        Assert.That(reported, Is.Null);
    }

    [Test]
    public void GetSafeBase_OnDisconnected_WhenHandlerThrows_CallsOnFail()
    {
        Exception? reported = null;
        var core = new ThrowingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);

        Assert.DoesNotThrow(() => safe.OnDisconnected(Reason));

        Assert.That(reported, Is.SameAs(core.Error));
    }

    [Test]
    public void GetSafeBase_OnReceived_WhenHandlerThrows_CallsOnFail()
    {
        Exception? reported = null;
        var core = new ThrowingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);
        UnionDataList buffer = AcquireBuffer();
        try
        {
            Assert.DoesNotThrow(() => safe.OnReceived(buffer));
        }
        finally
        {
            buffer.Release();
        }

        Assert.That(reported, Is.SameAs(core.Error));
    }

    [Test]
    public void GetSafeBase_OnStopped_WhenHandlerThrows_CallsOnFail()
    {
        Exception? reported = null;
        var core = new ThrowingServerHandler();
        IRawReliableServerHandler safe = core.GetSafe(e => reported = e);

        Assert.DoesNotThrow(() => safe.OnStopped(Reason));

        Assert.That(reported, Is.SameAs(core.Error));
    }

    #endregion

    private static UnionDataList AcquireBuffer()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }

    private sealed class RecordingServerHandler : IRawReliableServerHandler
    {
        public int ConnectedCount { get; private set; }

        public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

        public int DisconnectedCount { get; private set; }

        public StopReason? DisconnectedReason { get; private set; }

        public int ReceivedCount { get; private set; }

        public UnionDataList? ReceivedBuffer { get; private set; }

        public int StoppedCount { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            ConnectedCount++;
            ConnectedEndPoint = endPoint;
        }

        public void OnDisconnected(StopReason reason)
        {
            DisconnectedCount++;
            DisconnectedReason = reason;
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            ReceivedCount++;
            ReceivedBuffer = receivedBuffer;
        }

        public void OnStopped(StopReason reason)
        {
            StoppedCount++;
            StoppedReason = reason;
        }
    }

    private sealed class ThrowingServerHandler : IRawReliableServerHandler
    {
        public InvalidOperationException Error { get; } = new("handler failed");

        public void OnConnected(IRawReliableEndpoint endPoint) => throw Error;

        public void OnDisconnected(StopReason reason) => throw Error;

        public void OnReceived(UnionDataList receivedBuffer) => throw Error;

        public void OnStopped(StopReason reason) => throw Error;
    }

    private sealed class StubReliableEndpoint : IRawReliableEndpoint
    {
        public IEndPoint? RemoteEndPoint => null;

        public int MessageMaxByteSize => 0;

        public bool IsConnected => false;

        public SendResult Send(UnionDataList bufferToSend) => SendResult.Error;

        public bool Disconnect(StopReason reason) => false;

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
    }
}
