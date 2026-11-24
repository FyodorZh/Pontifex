using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the server-side RawUnreliable Direct contract: the supplied stop reason reaches an active handler, an
/// oversized inbound message is discarded before routing, and a factory-created handler is notified when the
/// carrier declines the endpoint.
/// </summary>
[TestFixture]
public sealed class RawUnreliableDirectServerContractTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void ServerStop_ActiveHandler_ReceivesTheSuppliedReasonInstance()
    {
        var handler = new RecordingHandler();
        StopReason? transportReason = null;
        var server = StartServer(_ => handler, out var channel,
            reason => transportReason = reason);

        try
        {
            Assert.That(channel.SendToServer(Message()), Is.EqualTo(SendResult.Ok));
            Assert.That(handler.ReceivedCount, Is.EqualTo(1));

            var reason = new UserIntention("test", "stop");

            Assert.That(server.Stop(reason), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handler.StoppedCount, Is.EqualTo(1));
                Assert.That(handler.StoppedReason, Is.SameAs(reason),
                    "the active handler must observe the exact supplied reason instance");
                Assert.That(transportReason, Is.SameAs(reason));
                Assert.That(server.IsStarted, Is.False);
            });
        }
        finally
        {
            channel.Dispose();
            server.Stop();
        }
    }

    [Test]
    public void OversizedInbound_ForNewRoute_DoesNotInvokeFactoryAndKeepsTransportAlive()
    {
        var handler = new RecordingHandler();
        var factoryCalls = 0;
        var server = StartServer(_ =>
        {
            factoryCalls++;
            return handler;
        }, out var channel);

        try
        {
            var oversized = OversizedMessage(server.MessageMaxByteSize);

            Assert.That(channel.SendToServer(oversized), Is.EqualTo(SendResult.Ok));
            Assert.Multiple(() =>
            {
                Assert.That(oversized.IsAlive, Is.False, "the oversized message must be released");
                Assert.That(factoryCalls, Is.Zero, "an oversized message must not invoke the factory");
                Assert.That(handler.StartedCount, Is.Zero);
                Assert.That(server.IsStarted, Is.True);
                Assert.That(server.IsValid, Is.True);
            });
        }
        finally
        {
            channel.Dispose();
            server.Stop();
        }
    }

    [Test]
    public void OversizedInbound_ForExistingRoute_DoesNotReachHandlerAndKeepsSessionAlive()
    {
        var handler = new RecordingHandler();
        var factoryCalls = 0;
        var server = StartServer(_ =>
        {
            factoryCalls++;
            return handler;
        }, out var channel);

        try
        {
            Assert.That(channel.SendToServer(Message()), Is.EqualTo(SendResult.Ok));
            Assert.That(handler.ReceivedCount, Is.EqualTo(1));
            var endpoint = handler.Endpoint!;

            var oversized = OversizedMessage(server.MessageMaxByteSize);

            Assert.That(channel.SendToServer(oversized), Is.EqualTo(SendResult.Ok));
            Assert.Multiple(() =>
            {
                Assert.That(oversized.IsAlive, Is.False, "the oversized message must be released");
                Assert.That(factoryCalls, Is.EqualTo(1), "an existing route must not re-invoke the factory");
                Assert.That(handler.ReceivedCount, Is.EqualTo(1), "the handler must not see oversized data");
                Assert.That(endpoint.IsValid, Is.True);
                Assert.That(server.IsStarted, Is.True);
            });
        }
        finally
        {
            channel.Dispose();
            server.Stop();
        }
    }

    [Test]
    public void DeclinedEndpoint_NotifiesFactoryCreatedHandlerOnceOutsideAdmissionLock()
    {
        var handler = new RecordingHandler();
        var factoryCalls = 0;
        var server = StartServer(_ =>
        {
            factoryCalls++;
            return handler;
        }, out var channel);

        try
        {
            server.EndpointFactoryForTests = (_, _, _) => null;
            handler.StopTransportAfterNotification = true;

            channel.SendToServer(Message());

            Assert.Multiple(() =>
            {
                Assert.That(factoryCalls, Is.EqualTo(1));
                Assert.That(handler.StoppedCount, Is.EqualTo(1),
                    "a factory-created handler whose endpoint was declined must observe one terminal notification");
                Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
                Assert.That(handler.AdmissionLockHeldDuringStopped, Is.False,
                    "application code must run outside the admission lock");
                Assert.That(handler.CalledTransportStop, Is.True);
                Assert.That(server.IsStarted, Is.False);
            });
        }
        finally
        {
            channel.Dispose();
            server.Stop();
        }
    }

    [Test]
    public void ThrowingEndpointFactory_NotifiesFactoryCreatedHandlerOnceOutsideAdmissionLock()
    {
        var handler = new RecordingHandler();
        var factoryCalls = 0;
        var server = StartServer(_ =>
        {
            factoryCalls++;
            return handler;
        }, out var channel);

        try
        {
            server.EndpointFactoryForTests = (_, _, _) =>
                throw new InvalidOperationException("Expected test endpoint construction failure.");

            channel.SendToServer(Message());

            Assert.Multiple(() =>
            {
                Assert.That(factoryCalls, Is.EqualTo(1));
                Assert.That(handler.StoppedCount, Is.EqualTo(1));
                Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
                Assert.That(handler.AdmissionLockHeldDuringStopped, Is.False);
                Assert.That(server.IsStarted, Is.True);
                Assert.That(server.IsValid, Is.True);
            });

            server.EndpointFactoryForTests = null;

            channel.SendToServer(Message());

            Assert.Multiple(() =>
            {
                Assert.That(factoryCalls, Is.EqualTo(2));
                Assert.That(handler.ReceivedCount, Is.EqualTo(1),
                    "a later message must be able to create a fresh route");
            });
        }
        finally
        {
            channel.Dispose();
            server.Stop();
        }
    }

    [Test]
    public void ClientCommit_DisposedChannel_DiscardsWithoutHittingCommitGate()
    {
        var id = "stage2-commit-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var handler = new ClientRecordingHandler();

        try
        {
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var endpoint = client.CurrentEndpoint;
            Assert.That(endpoint, Is.Not.Null);

            var discardedChannel = new Channel(new GuidEndPoint(Guid.NewGuid()),
                new StringEndPoint(id));
            discardedChannel.Dispose();
            endpoint!.SetChannelForTests(discardedChannel);

            var controls = new List<IControl>();
            endpoint.GetControls(controls);
            var control = controls.OfType<IRawUnreliableEndpointConformanceControl>().Single();
            var gate = control.BeforeSendCommitGate;
            gate.Arm(requiredHits: 2);

            try
            {
                var buffer = Message();
                endpoint.CommitForTests(buffer);

                Assert.Multiple(() =>
                {
                    Assert.That(buffer.IsAlive, Is.False,
                        "a message committed to a disposed channel must be released");
                    Assert.That(gate.HitCount, Is.EqualTo(1),
                        "the commit gate must not be hit for a discarded message");
                });
            }
            finally
            {
                gate.Reset();
            }
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static RawUnreliableDirectServerTransport StartServer(
        Func<IEndPoint, IRawUnreliableHandler?> factory, out Channel channel,
        Action<StopReason>? onStopped = null)
    {
        var id = "stage2-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        Assert.That(server.Init(source =>
        {
            var handler = factory(source);
            if (handler is RecordingHandler recording)
            {
                recording.AdmissionLockProbe = () => server.AdmissionLockHeldForTests;
                recording.Transport = server;
            }

            return handler;
        }), Is.True);
        Assert.That(server.Start(onStopped ?? (_ => { })), Is.True);

        var connected = ChannelManager.Instance.Connect(new StringEndPoint(id), new GuidEndPoint(Guid.NewGuid()));
        Assert.That(connected, Is.Not.Null, "the server did not accept the test channel.");
        channel = connected!;
        return server;
    }

    private static UnionDataList Message()
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(1);
        return message;
    }

    private static UnionDataList OversizedMessage(int messageMaxByteSize)
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(new UnionData(new MultiRefByteArray(new byte[messageMaxByteSize + 1])));
        Assert.That(message.GetDataSize(), Is.GreaterThan(messageMaxByteSize));
        return message;
    }

    private sealed class ClientRecordingHandler : IRawUnreliableHandler
    {
        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
        }
    }

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        private int _startedCount;
        private int _receivedCount;
        private int _stoppedCount;

        public RawUnreliableDirectServerTransport? Transport { get; set; }

        public Func<bool>? AdmissionLockProbe { get; set; }

        public bool StopTransportAfterNotification { get; set; }

        public IRawUnreliableEndpoint? Endpoint { get; private set; }

        public int StartedCount => Volatile.Read(ref _startedCount);

        public int ReceivedCount => Volatile.Read(ref _receivedCount);

        public int StoppedCount => Volatile.Read(ref _stoppedCount);

        public StopReason? StoppedReason { get; private set; }

        public bool AdmissionLockHeldDuringStopped { get; private set; }

        public bool CalledTransportStop { get; private set; }

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Interlocked.Increment(ref _startedCount);
            Endpoint = endpoint;
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            Interlocked.Increment(ref _receivedCount);
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _stoppedCount);
            StoppedReason = reason;

            if (AdmissionLockProbe != null)
            {
                AdmissionLockHeldDuringStopped = AdmissionLockProbe();
            }

            if (StopTransportAfterNotification)
            {
                CalledTransportStop = true;
                Transport!.Stop();
            }
        }
    }
}
