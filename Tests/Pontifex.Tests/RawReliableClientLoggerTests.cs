using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

// Loose fixture for RawReliableClientLogger. The fake transport supplies a recording logger,
// so the assertions do not depend on the static Scriba.Log sink.
[TestFixture]
public sealed class RawReliableClientLoggerTests
{
    private static readonly StopReason Reason = new("test", "reason");

    [Test]
    public void Init_ForwardsToCore()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeRawReliableClient(CreateLog(consumer));
        var logger = new RawReliableClientLogger(core);
        var userHandler = new RecordingClientHandler();

        Assert.That(((IRawReliableClient)logger).Init(userHandler), Is.True);

        Assert.That(core.Handler, Is.SameAs(logger));
    }

    [Test]
    public void HandlerCallbacks_ReachTheHandlerAndOnConnectedReceivesAWrappedEndpoint()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeRawReliableClient(CreateLog(consumer));
        var logger = new RawReliableClientLogger(core);
        var userHandler = new RecordingClientHandler();
        ((IRawReliableClient)logger).Init(userHandler);

        var endPoint = new StubReliableEndpoint();
        var session = (IRawReliableClientHandler)logger;
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

        Assert.That(userHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.ConnectedEndPoint, Is.Not.SameAs(endPoint));
        Assert.That(userHandler.ConnectedEndPoint, Is.InstanceOf<RawAckClientSideEndpointWrapper>());
        Assert.That(userHandler.ReceivedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedReason, Is.SameAs(Reason));
        Assert.That(userHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(userHandler.StoppedReason, Is.SameAs(Reason));
    }

    private static Logger CreateLog(RecordingLogConsumer consumer)
    {
        return new Logger(new ILogConsumer[] { consumer });
    }

    private static UnionDataList AcquireBuffer()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }

    private sealed class FakeRawReliableClient : IRawReliableClient
    {
        private readonly ILogger _log;

        public FakeRawReliableClient(ILogger log)
        {
            _log = log;
        }

        public IRawReliableClientHandler? Handler { get; private set; }

        public TransportType Type => TransportType.RawReliable;

        public string Name => "FakeRawReliableClient";

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

        public bool Init(IRawReliableClientHandler handler)
        {
            Handler = handler;
            return true;
        }
    }

    private sealed class RecordingClientHandler : IRawReliableClientHandler
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
