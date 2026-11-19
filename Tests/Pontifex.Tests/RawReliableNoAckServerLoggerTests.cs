using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Raw.Reliable.NoAck;
using Pontifex.Raw.Reliable.NoAck.Logger;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

// Loose fixture for RawReliableNoAckServerLogger. The fake transport supplies a recording logger,
// so the log-sink assertion does not depend on the static Scriba.Log sink.
[TestFixture]
public sealed class RawReliableNoAckServerLoggerTests
{
    private static readonly StopReason Reason = new("test", "reason");

    [Test]
    public void Init_ForwardsToCoreAndStoresTheUserFactory()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeNoAckServer(CreateLog(consumer));
        var logger = new RawReliableNoAckServerLogger(core);
        var userFactory = new RecordingFactory();

        Assert.That(((IRawReliableNoAckServer)logger).Init(userFactory), Is.True);

        // The core receives the logger as its factory and the user factory is retained for
        // TryCreate.
        Assert.That(core.Factory, Is.SameAs(logger));
        Assert.That(logger.TryCreate(VoidEndPoint.Instance), Is.Not.Null);
        Assert.That(userFactory.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public void TryCreate_ForwardsAllCallbacksAndWrapsTheEndpoint()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeNoAckServer(CreateLog(consumer));
        var logger = new RawReliableNoAckServerLogger(core);
        var userFactory = new RecordingFactory();
        ((IRawReliableNoAckServer)logger).Init(userFactory);

        IRawReliableNoAckServerHandler? session = logger.TryCreate(VoidEndPoint.Instance);
        Assert.That(session, Is.Not.Null);
        Assert.That(session, Is.Not.SameAs(userFactory.Handler));

        var endPoint = new StubReliableEndpoint();
        session!.OnConnected(endPoint);
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

        RecordingServerHandler userHandler = userFactory.Handler!;
        Assert.That(userHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.ConnectedEndPoint, Is.Not.SameAs(endPoint));
        Assert.That(userHandler.ConnectedEndPoint, Is.InstanceOf<RawAckServerSideEndpointWrapper>());
        Assert.That(userHandler.ReceivedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedReason, Is.SameAs(Reason));
        Assert.That(userHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(userHandler.StoppedReason, Is.SameAs(Reason));
    }

    [Test]
    public void TryCreate_WhenUserFactoryThrows_LogsAndRethrows()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeNoAckServer(CreateLog(consumer));
        var logger = new RawReliableNoAckServerLogger(core);
        var failure = new InvalidOperationException("boom");
        ((IRawReliableNoAckServer)logger).Init(new ThrowingFactory(failure));

        var thrown = Assert.Throws<InvalidOperationException>(
            () => logger.TryCreate(VoidEndPoint.Instance));

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(consumer.Messages, Has.Some.Contains("UserFactory.TryCreate failed"));
    }

    private static Logger CreateLog(RecordingLogConsumer consumer)
    {
        return new Logger(new ILogConsumer[] { consumer });
    }

    private static UnionDataList AcquireBuffer()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }

    private sealed class FakeNoAckServer : IRawReliableNoAckServer
    {
        private readonly ILogger _log;

        public FakeNoAckServer(ILogger log)
        {
            _log = log;
        }

        public IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>? Factory { get; private set; }

        public TransportType Type => TransportType.RawReliableNoAck;

        public string Name => "FakeNoAckServer";

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

        public bool Init(IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory)
        {
            Factory = factory;
            return true;
        }
    }

    private sealed class RecordingFactory :
        IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>
    {
        public int CreateCount { get; private set; }

        public RecordingServerHandler? Handler { get; private set; }

        public IRawReliableNoAckServerHandler? TryCreate(IEndPoint remote)
        {
            CreateCount++;
            var handler = new RecordingServerHandler();
            Handler = handler;
            return handler;
        }
    }

    private sealed class ThrowingFactory :
        IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>
    {
        private readonly Exception _failure;

        public ThrowingFactory(Exception failure)
        {
            _failure = failure;
        }

        public IRawReliableNoAckServerHandler? TryCreate(IEndPoint remote)
        {
            throw _failure;
        }
    }

    private sealed class RecordingServerHandler : IRawReliableNoAckServerHandler
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
