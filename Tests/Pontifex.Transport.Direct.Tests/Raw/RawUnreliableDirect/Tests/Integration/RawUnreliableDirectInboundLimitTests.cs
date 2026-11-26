using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.RawUnreliableDirect;

/// <summary>
/// Pins the inbound size limit of the in-process unreliable client endpoint: an oversized frame is released
/// and logged without reaching the handler and without stopping the transport.
/// </summary>
[TestFixture]
[Category("Direct.RawUnreliable")]
[Category("Direct.RawUnreliable.Integration")]
public sealed class RawUnreliableDirectInboundLimitTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void OversizedInboundMessage_IsDroppedWithoutStoppingTheClient()
    {
        var serverName = "inbound-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(serverName, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(serverName, Logger, MemoryRental.Shared);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);
            Assert.That(handler.Endpoint, Is.Not.Null);

            var trigger = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            trigger.PutLast(1);
            Assert.That(handler.Endpoint!.Send(trigger), Is.EqualTo(SendResult.Ok));

            WaitFor(() => client.CurrentEndpoint?.ChannelForTests != null,
                "The client endpoint did not acquire its channel.");
            var channel = client.CurrentEndpoint!.ChannelForTests!;

            var oversized = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            oversized.PutLast(new UnionData(
                new MultiRefByteArray(new byte[client.MessageMaxByteSize + 1])));
            Assert.That(channel.SendToClient(oversized), Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReceivedCount, Is.Zero,
                    "An oversized inbound message must not reach the handler.");
                Assert.That(client.IsStarted, Is.True,
                    "An oversized inbound message must not stop the transport.");
            });
        }
        finally
        {
            client.Stop();
            server.Stop();
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

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        private int _receivedCount;

        public IRawUnreliableEndpoint? Endpoint { get; private set; }

        public int ReceivedCount => Volatile.Read(ref _receivedCount);

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            Interlocked.Increment(ref _receivedCount);
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
