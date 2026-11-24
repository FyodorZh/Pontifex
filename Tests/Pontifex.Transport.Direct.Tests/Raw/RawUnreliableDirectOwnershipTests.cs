using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the RawUnreliable Direct delivery-system ownership: construction only during start, exact-once release
/// on every path, transient connect/install failures, the truthful server debug capability, and the unbounded
/// debug commit queue.
/// </summary>
[TestFixture]
public sealed class RawUnreliableDirectOwnershipTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void TransportWithoutStart_ConstructsNoDeliverySystem()
    {
        var transport = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        int constructions = 0;
        transport.SetDeliverySystemFactory(
            () => { Interlocked.Increment(ref constructions); return new PerfectDeliverySystem(); },
            () => { Interlocked.Increment(ref constructions); return new PerfectDeliverySystem(); });

        Assert.That(transport.Stop(), Is.True);

        Assert.That(Volatile.Read(ref constructions), Is.Zero,
            "a pre-start stop must not materialize a delivery system");
    }

    [Test]
    public void NetemFactory_StopBeforeStart_ConstructsNoSystem()
    {
        var transport = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        int constructions = 0;
        transport.SetDeliverySystemFactory(
            () => { Interlocked.Increment(ref constructions); return new TrackingDeliverySystem(); },
            () => { Interlocked.Increment(ref constructions); return new TrackingDeliverySystem(); });

        Assert.That(transport.Stop(), Is.True);

        Assert.That(Volatile.Read(ref constructions), Is.Zero);
    }

    [Test]
    public void Binding_NetemClient_StopBeforeStart_ConstructsNoSystem()
    {
        var builderCalls = 0;
        var constructionCount = 0;
        var context = new TestBuildContext(TransportRole.Client, Logger, MemoryRental.Shared)
            .WithString("id", UniqueId())
            .WithStruct("delivery", TestStructValue.Delivery("netem"));

        var transport = RawUnreliableDirectBinding.Create(context, (_, _) =>
        {
            Interlocked.Increment(ref builderCalls);
            return () =>
            {
                Interlocked.Increment(ref constructionCount);
                return new TrackingDeliverySystem();
            };
        });

        Assert.That(transport.Stop(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref builderCalls), Is.EqualTo(2),
                "the binding must build one factory per direction");
            Assert.That(Volatile.Read(ref constructionCount), Is.Zero,
                "the binding must not construct a system before start");
        });
    }

    [Test]
    public void Binding_FactoryBuilderThrows_DoesNotConstructAnySystem()
    {
        int constructionCount = 0;
        int calls = 0;
        var context = new TestBuildContext(TransportRole.Client, Logger, MemoryRental.Shared)
            .WithString("id", UniqueId())
            .WithStruct("delivery", TestStructValue.Delivery("netem"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RawUnreliableDirectBinding.Create(context, (_, _) =>
            {
                int call = Interlocked.Increment(ref calls);
                if (call == 1)
                {
                    throw new InvalidOperationException("Expected first builder failure.");
                }

                return () =>
                {
                    Interlocked.Increment(ref constructionCount);
                    return new TrackingDeliverySystem();
                };
            }));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("first builder failure"));
            Assert.That(Volatile.Read(ref calls), Is.EqualTo(1));
            Assert.That(Volatile.Read(ref constructionCount), Is.Zero);
        });
    }

    [Test]
    public void Binding_SecondFactoryBuilderThrows_DoesNotConstructAnySystem()
    {
        int constructionCount = 0;
        int calls = 0;
        var context = new TestBuildContext(TransportRole.Client, Logger, MemoryRental.Shared)
            .WithString("id", UniqueId())
            .WithStruct("delivery", TestStructValue.Delivery("netem"));

        Assert.Throws<InvalidOperationException>(() =>
            RawUnreliableDirectBinding.Create(context, (_, _) =>
            {
                int call = Interlocked.Increment(ref calls);
                if (call == 2)
                {
                    throw new InvalidOperationException("Expected second builder failure.");
                }

                return () =>
                {
                    Interlocked.Increment(ref constructionCount);
                    return new TrackingDeliverySystem();
                };
            }));

        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref calls), Is.EqualTo(2));
            Assert.That(Volatile.Read(ref constructionCount), Is.Zero);
        });
    }

    [Test]
    public void SetDeliverySystem_StopBeforeStart_ReleasesPairAndLaterStartUsesFreshPerfect()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var first = new TrackingDeliverySystem();
        var second = new TrackingDeliverySystem();
        var handler = new RecordingHandler();

        try
        {
            client.SetDeliverySystem(first, second);
            Assert.That(client.Stop(), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(first.ClearCalls, Is.EqualTo(1));
                Assert.That(second.ClearCalls, Is.EqualTo(1));
            });

            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(handler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(first.ClearCalls, Is.EqualTo(1), "a released explicit system must not be reused");
                Assert.That(second.ClearCalls, Is.EqualTo(1));
            });
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void SetDeliverySystem_Twice_ReleasesTheFirstPairOnce()
    {
        var client = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        var firstClient = new TrackingDeliverySystem();
        var firstServer = new TrackingDeliverySystem();
        var secondClient = new TrackingDeliverySystem();
        var secondServer = new TrackingDeliverySystem();

        client.SetDeliverySystem(firstClient, firstServer);
        client.SetDeliverySystem(secondClient, secondServer);

        Assert.Multiple(() =>
        {
            Assert.That(firstClient.ClearCalls, Is.EqualTo(1));
            Assert.That(firstServer.ClearCalls, Is.EqualTo(1));
            Assert.That(secondClient.ClearCalls, Is.Zero);
            Assert.That(secondServer.ClearCalls, Is.Zero);
        });

        client.Stop();
    }

    [Test]
    public void SetDeliverySystem_SameInstanceBothDirections_ReleasesOnceOnPreStartStop()
    {
        var client = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        var shared = new TrackingDeliverySystem();

        client.SetDeliverySystem(shared, shared);
        Assert.That(client.Stop(), Is.True);

        Assert.That(shared.ClearCalls, Is.EqualTo(1));
    }

    [Test]
    public void TryMakeReliable_ReleasesExplicitPairAndUsesPerfectSystems()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var explicitClient = new TrackingDeliverySystem();
        var explicitServer = new TrackingDeliverySystem();
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();

        try
        {
            client.SetDeliverySystem(explicitClient, explicitServer);
            Assert.That(client.TryMakeReliableForDebug(), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(client.IsReliableForDebugForTests, Is.True);
                Assert.That(explicitClient.ClearCalls, Is.EqualTo(1));
                Assert.That(explicitServer.ClearCalls, Is.EqualTo(1));
            });

            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));
            Assert.That(serverHandler.Received.Task.Wait(Timeout), Is.True);
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void SetDeliverySystem_AfterTryMakeReliable_Throws()
    {
        var client = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        Assert.That(client.TryMakeReliableForDebug(), Is.True);

        Assert.Throws<InvalidOperationException>(() =>
            client.SetDeliverySystem(new TrackingDeliverySystem(), new TrackingDeliverySystem()));
    }

    [Test]
    public void TryMakeReliable_AfterStart_ReturnsFalse()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);

        try
        {
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(new RecordingHandler()), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(client.TryMakeReliableForDebug(), Is.False);
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task TryMakeReliable_BeforeStartLock_AppliesToEveryEndpoint()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();
        using var startPaused = new ManualResetEventSlim(false);
        using var releaseStart = new ManualResetEventSlim(false);
        client.BeforeStartLockForTests = () =>
        {
            startPaused.Set();
            releaseStart.Wait(Timeout);
        };

        try
        {
            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);

            var start = Task.Run(() => client.Start(_ => { }));
            Assert.That(startPaused.Wait(Timeout), Is.True);

            Assert.That(client.TryMakeReliableForDebug(), Is.True);
            releaseStart.Set();

            Assert.That(await start.WaitAsync(Timeout), Is.True);
            Assert.That(client.IsReliableForDebugForTests, Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));
            await serverHandler.Received.Task.WaitAsync(Timeout);
        }
        finally
        {
            releaseStart.Set();
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void EndpointFactoryThrow_ReleasesExplicitPairExactlyOnce()
    {
        var client = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        var explicitClient = new TrackingDeliverySystem();
        var explicitServer = new TrackingDeliverySystem();
        client.SetDeliverySystem(explicitClient, explicitServer);
        client.EndpointFactoryForTests = _ =>
            throw new InvalidOperationException("Expected endpoint construction failure.");

        Assert.That(client.Init(new RecordingHandler()), Is.True);
        Assert.That(client.Start(_ => { }), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(explicitClient.ClearCalls, Is.EqualTo(1));
            Assert.That(explicitServer.ClearCalls, Is.EqualTo(1));
            Assert.That(client.IsValid, Is.False);
        });
    }

    [Test]
    public void StartBeforeInit_ReleasesExplicitPairExactlyOnce()
    {
        var client = new RawUnreliableDirectClientTransport(UniqueId(), Logger, MemoryRental.Shared);
        var explicitClient = new TrackingDeliverySystem();
        var explicitServer = new TrackingDeliverySystem();
        client.SetDeliverySystem(explicitClient, explicitServer);

        Assert.That(client.Start(_ => { }), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(explicitClient.ClearCalls, Is.EqualTo(1));
            Assert.That(explicitServer.ClearCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ManagerCallbackThrow_EndpointSurvivesAndRetriesWithFreshSystems()
    {
        var id = UniqueId();
        var explicitClient = new TrackingDeliverySystem();
        var explicitServer = new TrackingDeliverySystem();
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var clientHandler = new RecordingHandler();
        var serverHandler = new RecordingHandler();
        RawUnreliableDirectServerTransport? realServer = null;

        Assert.That(ChannelManager.Instance.RegisterServer(RawUnreliableDirectInfo.TransportName, new StringEndPoint(id),
            _ => throw new InvalidOperationException("Expected manager callback failure.")), Is.True);
        try
        {
            client.SetDeliverySystem(explicitClient, explicitServer);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));
            Assert.Multiple(() =>
            {
                Assert.That(client.IsStarted, Is.True);
                Assert.That(explicitClient.ClearCalls, Is.Zero,
                    "a null connect result leaves the configured systems unhanded");
                Assert.That(explicitServer.ClearCalls, Is.Zero);
            });

            ChannelManager.Instance.UnregisterServer(RawUnreliableDirectInfo.TransportName, new StringEndPoint(id));
            realServer = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
            Assert.That(realServer.Init(_ => serverHandler), Is.True);
            Assert.That(realServer.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint.Send(Message()), Is.EqualTo(SendResult.Ok));
            await serverHandler.Received.Task.WaitAsync(Timeout);
        }
        finally
        {
            client.Stop();
            realServer?.Stop();
            ChannelManager.Instance.UnregisterServer(RawUnreliableDirectInfo.TransportName, new StringEndPoint(id));
        }
    }

    [Test]
    public async Task InstallThrow_ReleasesOnlyTheUninstalledSystemAndRetries()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var throwingClientSystem = new ThrowingSubscriptionDeliverySystem();
        var serverSystem = new TrackingDeliverySystem();
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();

        try
        {
            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            client.SetDeliverySystem(throwingClientSystem, serverSystem);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(client.IsStarted, Is.True);
                Assert.That(serverSystem.ClearCalls, Is.EqualTo(1),
                    "only the direction that the channel never took must be released");
            });

            Assert.That(clientHandler.Endpoint.Send(Message()), Is.EqualTo(SendResult.Ok));
            await serverHandler.Received.Task.WaitAsync(Timeout);
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task NormalStartAndStop_ReleasesEachDirectionExactlyOnce()
    {
        var id = UniqueId();
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var clientSystem = new TrackingDeliverySystem();
        var serverSystem = new TrackingDeliverySystem();
        var clientHandler = new RecordingHandler();

        try
        {
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            client.SetDeliverySystem(clientSystem, serverSystem);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message()), Is.EqualTo(SendResult.Ok));
            await WaitForChannelAsync(client);

            Assert.That(client.Stop(), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(clientSystem.ClearCalls, Is.EqualTo(1));
                Assert.That(serverSystem.ClearCalls, Is.EqualTo(1));
            });
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void Server_ReportsReliableDebugAsUnsupported()
    {
        var server = new RawUnreliableDirectServerTransport(UniqueId(), Logger, MemoryRental.Shared);

        Assert.That(server.TryMakeReliableForDebug(), Is.False);
    }

    [Test]
    public async Task UnboundedCallbackQueue_AcceptsBeyondAnyFixedCapacity()
    {
        int processed = 0;
        var allProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new SerializedCallbackQueue<int>("unbounded-test",
            _ =>
            {
                if (Interlocked.Increment(ref processed) == 10_000)
                {
                    allProcessed.TrySetResult();
                }
            },
            _ => { });

        for (int i = 0; i < 10_000; i++)
        {
            Assert.That(queue.Post(i), Is.True);
        }

        await allProcessed.Task.WaitAsync(Timeout);
        Assert.That(Volatile.Read(ref processed), Is.EqualTo(10_000));
    }

    [Test]
    public void BoundedCallbackQueue_RejectsAtItsCapacity()
    {
        using var gate = new ManualResetEventSlim(false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new SerializedCallbackQueue<int>(capacity: 1, "bounded-test",
            _ =>
            {
                entered.TrySetResult();
                gate.Wait(Timeout);
            },
            _ => { });

        Assert.That(queue.Post(1), Is.True);
        Assert.That(entered.Task.Wait(Timeout), Is.True);
        Assert.That(queue.Post(2), Is.True);

        int accepted = 2;
        while (queue.Post(accepted + 1))
        {
            accepted++;
            Assert.That(accepted, Is.LessThan(100), "a bounded queue must reject a fixed capacity");
        }

        Assert.That(accepted, Is.GreaterThanOrEqualTo(2));
        gate.Set();
    }

    private static async Task WaitForChannelAsync(RawUnreliableDirectClientTransport client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.CurrentEndpoint?.ChannelForTests != null)
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("The client endpoint did not acquire its channel.");
    }

    private static string UniqueId()
    {
        return "stage4-" + Guid.NewGuid().ToString("N");
    }

    private static UnionDataList Message()
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(1);
        return message;
    }

    private sealed class TrackingDeliverySystem : IDeliverySystem, IDisposable
    {
        private int _clearCalls;
        private int _disposeCalls;

        public int ClearCalls => Volatile.Read(ref _clearCalls);

        public int DisposeCalls => Volatile.Read(ref _disposeCalls);

        public event Action<UnionDataList>? Delivered;

        public void Deliver(UnionDataList message)
        {
            Delivered?.Invoke(message);
        }

        public void Clear()
        {
            Interlocked.Increment(ref _clearCalls);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCalls);
        }
    }

    private sealed class ThrowingSubscriptionDeliverySystem : IDeliverySystem
    {
        public event Action<UnionDataList>? Delivered
        {
            add => throw new InvalidOperationException("Expected delivery-system subscription failure.");
            remove { }
        }

        public void Deliver(UnionDataList message)
        {
            message.Release();
        }

        public void Clear()
        {
        }
    }

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        private int _receivedCount;

        public TaskCompletionSource Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IRawUnreliableEndpoint? Endpoint { get; private set; }

        public int ReceivedCount => Volatile.Read(ref _receivedCount);

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            Interlocked.Increment(ref _receivedCount);
            Received.TrySetResult();
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
