using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

// Loose fixture for RawReliableServerLogger. The fake transport supplies a recording logger,
// so the log-sink assertion does not depend on the static Scriba.Log sink.
[TestFixture]
public sealed class RawReliableServerLoggerTests
{
    private static readonly StopReason Reason = new("test", "reason");

    [Test]
    public void Init_ForwardsToCoreAndStoresTheUserFactory()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeRawReliableServer(CreateLog(consumer));
        var logger = new RawReliableServerLogger(core);
        var userFactory = new RecordingFactory();

        Assert.That(((IRawReliableServer)logger).Init(userFactory), Is.True);

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
        var core = new FakeRawReliableServer(CreateLog(consumer));
        var logger = new RawReliableServerLogger(core);
        var userFactory = new RecordingFactory();
        ((IRawReliableServer)logger).Init(userFactory);

        IRawReliableServerHandler? session = logger.TryCreate(VoidEndPoint.Instance);
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
        Assert.That(userHandler.ConnectedEndPoint, Is.InstanceOf<RawReliableServerSideEndpointWrapper>());
        Assert.That(userHandler.ReceivedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(userHandler.DisconnectedReason, Is.SameAs(Reason));
        Assert.That(userHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(userHandler.StoppedReason, Is.SameAs(Reason));
    }

    [Test]
    public void SessionOnConnected_IsTransparentAndForwardsTheMessageMaxByteSizeUnchanged()
    {
        const int coreMaxByteSize = 4096;
        var consumer = new RecordingLogConsumer();
        var core = new FakeRawReliableServer(CreateLog(consumer));
        var logger = new RawReliableServerLogger(core);
        var userFactory = new RecordingFactory();
        ((IRawReliableServer)logger).Init(userFactory);

        IRawReliableServerHandler? session = logger.TryCreate(VoidEndPoint.Instance);
        Assert.That(session, Is.Not.Null);

        var endPoint = new RecordingReliableEndpoint(coreMaxByteSize);
        session!.OnConnected(endPoint);

        // The logger adds no handshake or payload of its own and keeps the negotiated limit.
        Assert.That(endPoint.SendCount, Is.Zero);
        Assert.That(userFactory.Handler!.ConnectedCount, Is.EqualTo(1));
        Assert.That(
            userFactory.Handler!.ConnectedEndPoint!.MessageMaxByteSize,
            Is.EqualTo(coreMaxByteSize));
    }

    [Test]
    public void TryCreate_WhenUserFactoryThrows_LogsAndRethrows()
    {
        var consumer = new RecordingLogConsumer();
        var core = new FakeRawReliableServer(CreateLog(consumer));
        var logger = new RawReliableServerLogger(core);
        var failure = new InvalidOperationException("boom");
        ((IRawReliableServer)logger).Init(new ThrowingFactory(failure));

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

    private sealed class FakeRawReliableServer : IRawReliableServer
    {
        private readonly ILogger _log;

        public FakeRawReliableServer(ILogger log)
        {
            _log = log;
        }

        public IRawReliableServerHandlerFactory<IRawReliableServerHandler>? Factory { get; private set; }

        public TransportType Type => TransportType.RawReliable;

        public string Name => "FakeRawReliableServer";

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

        public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
        {
            Factory = factory;
            return true;
        }
    }

    private sealed class RecordingFactory :
        IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        public int CreateCount { get; private set; }

        public RecordingServerHandler? Handler { get; private set; }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            CreateCount++;
            var handler = new RecordingServerHandler();
            Handler = handler;
            return handler;
        }
    }

    private sealed class ThrowingFactory :
        IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly Exception _failure;

        public ThrowingFactory(Exception failure)
        {
            _failure = failure;
        }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            throw _failure;
        }
    }

    private sealed class RecordingServerHandler : IRawReliableServerHandler
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

    private sealed class RecordingReliableEndpoint : IRawReliableEndpoint
    {
        public RecordingReliableEndpoint(int messageMaxByteSize)
        {
            MessageMaxByteSize = messageMaxByteSize;
        }

        public int SendCount { get; private set; }

        public IEndPoint? RemoteEndPoint => null;

        public int MessageMaxByteSize { get; }

        public bool IsConnected => true;

        public SendResult Send(UnionDataList bufferToSend)
        {
            SendCount++;
            return SendResult.Ok;
        }

        public bool Disconnect(StopReason reason) => true;

        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
    }
}
