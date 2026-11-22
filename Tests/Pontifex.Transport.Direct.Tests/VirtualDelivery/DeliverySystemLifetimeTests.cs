using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Tests.VirtualDelivery;

/// <summary>
/// Pins delivery-system ownership: a replaced or discarded system is cleared and, when disposable, disposed.
/// </summary>
[TestFixture]
public sealed class DeliverySystemLifetimeTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void ReplacingAChannelDeliverySystem_ReleasesTheOldSystem()
    {
        using var channel = new Channel(new StringEndPoint("server"), new StringEndPoint("client"));
        var first = new TrackingDeliverySystem();

        channel.SetClientDeliverySystem(first);
        channel.SetClientDeliverySystem(new PerfectDeliverySystem());

        Assert.Multiple(() =>
        {
            Assert.That(first.ClearCount, Is.EqualTo(1));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DisposingAChannel_ReleasesBothDirectionSystems()
    {
        var channel = new Channel(new StringEndPoint("server"), new StringEndPoint("client"));
        var client = new TrackingDeliverySystem();
        var server = new TrackingDeliverySystem();

        channel.SetClientDeliverySystem(client);
        channel.SetServerDeliverySystem(server);
        channel.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(client.ClearCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
            Assert.That(server.ClearCount, Is.EqualTo(1));
            Assert.That(server.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClientStop_ReleasesConfiguredSystemsThatWereNeverInstalled()
    {
        var client = new RawUnreliableDirectClient(
            "lifetime-" + Guid.NewGuid().ToString("N"), Logger, MemoryRental.Shared);
        var clientDelivery = new TrackingDeliverySystem();
        var serverDelivery = new TrackingDeliverySystem();

        client.SetDeliverySystem(clientDelivery, serverDelivery);
        Assert.That(client.Init(new NoopHandler()), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        Assert.That(client.Stop(), Is.True);

        WaitFor(() => clientDelivery.DisposeCount == 1 && serverDelivery.DisposeCount == 1,
            "The transport did not release delivery systems that were never installed on a channel.");
    }

    [Test]
    public void SetDeliverySystem_AfterStart_IsRejected()
    {
        var client = new RawUnreliableDirectClient(
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

    private static void WaitFor(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(message);
            }

            Thread.Sleep(1);
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
