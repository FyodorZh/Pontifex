using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

// Loose fixture for the shared internal server-handler logging wrapper. The wrapper is internal,
// which is fine because Pontifex exposes its internals to this project. The transport logger is
// injected directly, so the sink assertions never depend on the static Scriba.Log sink.
[TestFixture]
public sealed class RawReliableServerHandlerLoggerTests
{
    private static readonly StopReason Reason = new("test", "reason");

    [Test]
    public void OnConnected_ForwardsToUserHandlerWithWrappedEndpoint()
    {
        var consumer = new RecordingLogConsumer();
        var userHandler = new RecordingServerHandler();
        RawReliableServerHandlerLogger helper = CreateHelper(consumer, userHandler);
        var endpoint = new StubReliableEndpoint();

        helper.OnConnected(endpoint);

        Assert.That(userHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.ConnectedEndPoint, Is.Not.SameAs(endpoint));
        Assert.That(userHandler.ConnectedEndPoint, Is.InstanceOf<RawReliableServerSideEndpointWrapper>());

        // The wrapper reports failure instead of throwing when it has no core endpoint.
        var nullCoreHandler = new RecordingServerHandler();
        RawReliableServerHandlerLogger nullCoreHelper = CreateHelper(consumer, nullCoreHandler);

        nullCoreHelper.OnConnected(null!);

        IRawReliableEndpoint wrapper = nullCoreHandler.ConnectedEndPoint!;
        UnionDataList buffer = AcquireBuffer();
        try
        {
            Assert.That(wrapper.Send(buffer), Is.EqualTo(SendResult.Error));
        }
        finally
        {
            buffer.Release();
        }

        Assert.That(wrapper.Disconnect(Reason), Is.False);
    }

    [Test]
    public void OnDisconnected_ForwardsReason()
    {
        var consumer = new RecordingLogConsumer();
        var userHandler = new RecordingServerHandler();
        RawReliableServerHandlerLogger helper = CreateHelper(consumer, userHandler);

        helper.OnDisconnected(Reason);

        Assert.That(userHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedReason, Is.SameAs(Reason));
        Assert.That(userHandler.ConnectedCount, Is.Zero);
        Assert.That(userHandler.ReceivedCount, Is.Zero);
        Assert.That(userHandler.StoppedCount, Is.Zero);
    }

    [Test]
    public void OnReceived_ForwardsBuffer()
    {
        var consumer = new RecordingLogConsumer();
        var userHandler = new RecordingServerHandler();
        RawReliableServerHandlerLogger helper = CreateHelper(consumer, userHandler);
        UnionDataList buffer = AcquireBuffer();
        try
        {
            helper.OnReceived(buffer);

            Assert.That(userHandler.ReceivedCount, Is.EqualTo(1));
            Assert.That(userHandler.ReceivedBuffer, Is.SameAs(buffer));
        }
        finally
        {
            buffer.Release();
        }

        Assert.That(userHandler.ConnectedCount, Is.Zero);
        Assert.That(userHandler.DisconnectedCount, Is.Zero);
        Assert.That(userHandler.StoppedCount, Is.Zero);
    }

    [Test]
    public void OnStopped_ForwardsReason()
    {
        var consumer = new RecordingLogConsumer();
        var userHandler = new RecordingServerHandler();
        RawReliableServerHandlerLogger helper = CreateHelper(consumer, userHandler);

        helper.OnStopped(Reason);

        Assert.That(userHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(userHandler.StoppedReason, Is.SameAs(Reason));
        Assert.That(userHandler.ConnectedCount, Is.Zero);
        Assert.That(userHandler.DisconnectedCount, Is.Zero);
        Assert.That(userHandler.ReceivedCount, Is.Zero);
    }

    [Test]
    public void UserHandler_Redirection_RoutesSubsequentCallbacksToNewHandlerAndLogsToTransportSink()
    {
        var consumer = new RecordingLogConsumer();
        var first = new RecordingServerHandler();
        var second = new RecordingServerHandler();
        RawReliableServerHandlerLogger helper = CreateHelper(consumer, first);

        helper.OnDisconnected(Reason);
        helper.UserHandler = second;
        helper.OnStopped(Reason);

        Assert.That(first.DisconnectedCount, Is.EqualTo(1));
        Assert.That(first.StoppedCount, Is.Zero);
        Assert.That(second.DisconnectedCount, Is.Zero);
        Assert.That(second.StoppedCount, Is.EqualTo(1));

        // A helper that kept the static Scriba.Log sink would publish nothing here.
        Assert.That(consumer.Messages, Has.Some.Contains("UserHandler.OnStopped"));
    }

    private static RawReliableServerHandlerLogger CreateHelper(
        RecordingLogConsumer consumer, IRawReliableServerHandler? userHandler)
    {
        return new RawReliableServerHandlerLogger(CreateLog(consumer), userHandler);
    }

    private static Logger CreateLog(RecordingLogConsumer consumer)
    {
        return new Logger(new ILogConsumer[] { consumer });
    }

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
