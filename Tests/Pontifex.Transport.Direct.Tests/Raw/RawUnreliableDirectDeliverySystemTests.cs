using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the unreliable Direct client's delivery-system ownership: configured systems are released even when
/// the endpoint never installed them on a channel.
/// </summary>
[TestFixture]
public sealed class RawUnreliableDirectDeliverySystemTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void ClientStop_ReleasesConfiguredSystemsThatWereNeverInstalled()
    {
        var client = new RawUnreliableDirectClientTransport(
            "lifetime-" + Guid.NewGuid().ToString("N"), Logger, MemoryRental.Shared);
        var clientDelivery = new TrackingDeliverySystem();
        var serverDelivery = new TrackingDeliverySystem();

        client.SetDeliverySystem(clientDelivery, serverDelivery);
        Assert.That(client.Init(new NoopHandler()), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        var endpoint = client.CurrentEndpoint;
        Assert.That(endpoint, Is.Not.Null);
        using var endpointStopped = new ManualResetEventSlim(false);
        endpoint!.Stopped += _ => endpointStopped.Set();

        Assert.That(client.Stop(), Is.True);
        Assert.That(endpointStopped.Wait(TimeSpan.FromSeconds(5)), Is.True,
            "The endpoint did not reach its terminal state.");

        Assert.Multiple(() =>
        {
            Assert.That(clientDelivery.DisposeCount, Is.EqualTo(1),
                "The transport did not release a configured system that was never installed.");
            Assert.That(serverDelivery.DisposeCount, Is.EqualTo(1),
                "The transport did not release a configured system that was never installed.");
        });
    }

    [Test]
    public void ClientSend_ToAServerThatRejectsTheChannel_ReleasesTheConfiguredSystems()
    {
        string serverName = "rejects-" + Guid.NewGuid().ToString("N");
        var serverEndPoint = new StringEndPoint(serverName);
        Assert.That(ChannelManager.Instance.RegisterServer(serverEndPoint, channel => channel.Dispose()),
            Is.True);

        try
        {
            var client = new RawUnreliableDirectClientTransport(
                serverName, Logger, MemoryRental.Shared);
            var clientDelivery = new TrackingDeliverySystem();
            var serverDelivery = new TrackingDeliverySystem();

            client.SetDeliverySystem(clientDelivery, serverDelivery);
            Assert.That(client.Init(new NoopHandler()), Is.True);

            using var transportStopped = new ManualResetEventSlim(false);
            Assert.That(client.Start(_ => transportStopped.Set()), Is.True);

            var endpoint = client.CurrentEndpoint;
            Assert.That(endpoint, Is.Not.Null);

            var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            message.PutLast(1);
            Assert.That(endpoint!.Send(message), Is.EqualTo(SendResult.Ok));

            Assert.That(transportStopped.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "The client transport did not stop after the rejected channel.");

            Assert.Multiple(() =>
            {
                Assert.That(clientDelivery.DisposeCount, Is.EqualTo(1),
                    "A system installed on a rejected channel was leaked.");
                Assert.That(serverDelivery.DisposeCount, Is.EqualTo(1),
                    "A system installed on a rejected channel was leaked.");
            });
        }
        finally
        {
            ChannelManager.Instance.UnregisterServer(serverEndPoint);
        }
    }

    [Test]
    public void SetDeliverySystem_AfterStart_IsRejected()
    {
        var client = new RawUnreliableDirectClientTransport(
            "lifetime-" + Guid.NewGuid().ToString("N"), Logger, MemoryRental.Shared);
        Assert.That(client.Init(new NoopHandler()), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        try
        {
            Assert.Throws<InvalidOperationException>(() => client.SetDeliverySystem(
                new PerfectDeliverySystem(), new PerfectDeliverySystem()));
        }
        finally
        {
            client.Stop();
        }
    }

    private sealed class TrackingDeliverySystem : IDeliverySystem, IDisposable
    {
        private int _clearCount;
        private int _disposeCount;

        public int ClearCount => Volatile.Read(ref _clearCount);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public event Action<UnionDataList>? Delivered;

        public void Deliver(UnionDataList message)
        {
            var delivered = Delivered;
            if (delivered != null)
            {
                delivered(message);
            }
            else
            {
                message.Release();
            }
        }

        public void Clear()
        {
            Interlocked.Increment(ref _clearCount);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
        }
    }

    private sealed class NoopHandler : IRawUnreliableHandler
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
}
