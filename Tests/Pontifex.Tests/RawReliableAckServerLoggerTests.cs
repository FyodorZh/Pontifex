using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

// Loose fixture for RawReliableAckServerLogger after it was rewritten to delegate the handler
// callbacks to the shared RawReliableServerHandlerLogger. The fake transport supplies a recording
// logger, so the log-sink assertions do not depend on the static Scriba.Log sink.
[TestFixture]
public sealed class RawReliableAckServerLoggerTests
{
    private static readonly StopReason Reason = new("test", "reason");

    [Test]
    public void TryAck_DelegatesAllHandlerCallbacksToTheUserHandler()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeAckServer(CreateLog(consumer));
        var firstAcknowledger = new RecordingAcknowledger();
        var logger = new RawReliableAckServerLogger(core);
        var server = (IRawReliableAckServer)logger;

        Assert.That(server.Init(firstAcknowledger), Is.True);
        Assert.That(core.Acknowledger, Is.SameAs(logger));

        var result = logger.TryAck(VoidEndPoint.Instance, AcquireBuffer());
        Assert.That(result, Is.Not.Null);
        var (session, ackResponse) = result!.Value;
        try
        {
            Assert.That(session, Is.SameAs(logger));

            var endPoint = new StubReliableEndpoint();
            session.OnConnected(endPoint);
            UnionDataList inbound = AcquireBuffer();
            try
            {
                session.OnReceived(inbound);
            }
            finally
            {
                inbound.Release();
            }

            session.OnDisconnected(Reason);
            session.OnStopped(Reason);

            RecordingServerHandler userHandler = firstAcknowledger.Handler!;
            Assert.That(userHandler.ConnectedCount, Is.EqualTo(1));
            Assert.That(userHandler.ConnectedEndPoint, Is.Not.SameAs(endPoint));
            Assert.That(userHandler.ConnectedEndPoint, Is.InstanceOf<RawAckServerSideEndpointWrapper>());
            Assert.That(userHandler.ReceivedCount, Is.EqualTo(1));
            Assert.That(userHandler.DisconnectedCount, Is.EqualTo(1));
            Assert.That(userHandler.DisconnectedReason, Is.SameAs(Reason));
            Assert.That(userHandler.StoppedCount, Is.EqualTo(1));
            Assert.That(userHandler.StoppedReason, Is.SameAs(Reason));

            // A second Init/TryAck rewires the wrapper's user handler for the next session.
            var secondAcknowledger = new RecordingAcknowledger();
            Assert.That(server.Init(secondAcknowledger), Is.True);
            var rewired = logger.TryAck(VoidEndPoint.Instance, AcquireBuffer());
            Assert.That(rewired, Is.Not.Null);
            var (rewiredSession, rewiredAckResponse) = rewired!.Value;
            try
            {
                rewiredSession.OnStopped(Reason);

                Assert.That(secondAcknowledger.Handler!.StoppedCount, Is.EqualTo(1));
                Assert.That(firstAcknowledger.Handler!.StoppedCount, Is.EqualTo(1));
            }
            finally
            {
                rewiredAckResponse.Release();
            }
        }
        finally
        {
            ackResponse.Release();
        }
    }

    [Test]
    public void HandlerCallbacks_LogToTheTransportLogger()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeAckServer(CreateLog(consumer));
        var logger = new RawReliableAckServerLogger(core);
        ((IRawReliableAckServer)logger).Init(new RecordingAcknowledger());

        var result = logger.TryAck(VoidEndPoint.Instance, AcquireBuffer());
        Assert.That(result, Is.Not.Null);
        var (session, ackResponse) = result!.Value;
        try
        {
            session.OnStopped(Reason);
        }
        finally
        {
            ackResponse.Release();
        }

        // A helper that kept the static Scriba.Log sink would publish nothing here.
        Assert.That(consumer.Messages, Has.Some.Contains("UserHandler.OnStopped"));
    }

    private static Logger CreateLog(RecordingLogConsumer consumer)
    {
        return new Logger(new ILogConsumer[] { consumer });
    }

    private static UnionDataList AcquireBuffer()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }

    private sealed class FakeAckServer : IRawReliableAckServer
    {
        private readonly ILogger _log;

        public FakeAckServer(ILogger log)
        {
            _log = log;
        }

        public IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? Acknowledger { get; private set; }

        public TransportType Type => TransportType.RawReliableAck;

        public string Name => "FakeAckServer";

        public bool IsValid => true;

        public bool IsStarted => false;

        public bool Start(Action<StopReason> onStopped) => false;

        public bool Stop(StopReason? reason = null) => false;

        public ILogger Log => _log;

        public IMemoryRental Memory => MemoryRental.Shared;

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        public int MessageMaxByteSize => 1024;

        public bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            Acknowledger = acknowledger;
            return true;
        }
    }

    private sealed class RecordingAcknowledger : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        public RecordingServerHandler? Handler { get; private set; }

        public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryAck(
            IEndPoint remote, UnionDataList ackData)
        {
            // The acknowledger owns the received reference and releases it exactly once.
            ackData.Release();
            var handler = new RecordingServerHandler();
            Handler = handler;
            IRawReliableAckServerHandler ackHandler = handler;
            return (ackHandler, AcquireBuffer());
        }
    }

    private sealed class RecordingServerHandler : IRawReliableAckServerHandler
    {
        public int ConnectedCount { get; private set; }

        public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

        public int DisconnectedCount { get; private set; }

        public StopReason? DisconnectedReason { get; private set; }

        public int ReceivedCount { get; private set; }

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
