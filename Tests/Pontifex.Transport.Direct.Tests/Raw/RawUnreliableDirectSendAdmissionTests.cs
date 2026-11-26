using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Tests.Raw.TestSupport;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the RawUnreliable Direct outbound admission: the declared 100-message commit window, the exact-once
/// reservation release on every path, and the send-return barrier that defers peer callbacks.
/// </summary>
[TestFixture]
[Category("Direct.Integration")]
public sealed class RawUnreliableDirectSendAdmissionTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public async Task ClientCommitWindow_RejectsThe101stMessageWithBufferOverflow()
    {
        var id = "stage3-client-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var blocker = new BlockingDeliverySystem();
        var handler = new RecordingHandler();
        blocker.ExpectCompletions(100);

        try
        {
            client.SetDeliverySystem(new PerfectDeliverySystem(), blocker);
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);
            var endpoint = client.CurrentEndpoint!;

            Assert.That(endpoint.Send(Message(1)), Is.EqualTo(SendResult.Ok));
            await blocker.FirstDelivery.Task.WaitAsync(Timeout);

            for (int i = 2; i <= 100; i++)
            {
                Assert.That(endpoint.Send(Message(i)), Is.EqualTo(SendResult.Ok));
            }

            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.EqualTo(100));

            var rejected = Message(101);
            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Send(rejected), Is.EqualTo(SendResult.BufferOverflow));
                Assert.That(rejected.IsAlive, Is.False, "a rejected message must be released");
            });

            blocker.Release();
            await blocker.ExpectedCompletions.Task.WaitAsync(Timeout);
            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.Zero,
                "every accepted reservation must be released after the worker drains");
        }
        finally
        {
            blocker.Release();
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task ClientCommitWindow_CountsEmptyMessages()
    {
        var id = "stage3-empty-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var blocker = new BlockingDeliverySystem();
        var handler = new RecordingHandler();
        blocker.ExpectCompletions(100);

        try
        {
            client.SetDeliverySystem(new PerfectDeliverySystem(), blocker);
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);
            var endpoint = client.CurrentEndpoint!;

            Assert.That(endpoint.Send(EmptyMessage()), Is.EqualTo(SendResult.Ok));
            await blocker.FirstDelivery.Task.WaitAsync(Timeout);

            for (int i = 1; i < 100; i++)
            {
                Assert.That(endpoint.Send(EmptyMessage()), Is.EqualTo(SendResult.Ok));
            }

            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.EqualTo(100));

            var rejected = EmptyMessage();
            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Send(rejected), Is.EqualTo(SendResult.BufferOverflow));
                Assert.That(rejected.IsAlive, Is.False);
            });

            blocker.Release();
            await blocker.ExpectedCompletions.Task.WaitAsync(Timeout);
            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.Zero);
        }
        finally
        {
            blocker.Release();
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task ServerCommitWindow_UsesItsOwnCounterAndRejectsThe101stMessage()
    {
        var id = "stage3-server-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var blocker = new BlockingDeliverySystem();
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();
        blocker.ExpectCompletions(100);

        try
        {
            client.SetDeliverySystem(blocker, new PerfectDeliverySystem());
            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message(0)), Is.EqualTo(SendResult.Ok));
            await serverHandler.Started.Task.WaitAsync(Timeout);
            var endpoint = (RawUnreliableDirectServerEndpoint)serverHandler.Endpoint!;

            Assert.That(endpoint.Send(Message(1)), Is.EqualTo(SendResult.Ok));
            await blocker.FirstDelivery.Task.WaitAsync(Timeout);

            for (int i = 2; i <= 100; i++)
            {
                Assert.That(endpoint.Send(Message(i)), Is.EqualTo(SendResult.Ok));
            }

            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.EqualTo(100));

            var rejected = Message(101);
            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Send(rejected), Is.EqualTo(SendResult.BufferOverflow));
                Assert.That(rejected.IsAlive, Is.False);
            });

            blocker.Release();
            await blocker.ExpectedCompletions.Task.WaitAsync(Timeout);
            Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.Zero);
        }
        finally
        {
            blocker.Release();
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task PostThenThrow_ReleasesTheReservationOnceAndStopsTheEndpoint()
    {
        var id = "stage3-throw-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var handler = new RecordingHandler();
        var transportStopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            Assert.That(server.Init(_ => null), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(handler), Is.True);
            Assert.That(client.Start(reason => transportStopped.TrySetResult(reason)), Is.True);
            var endpoint = client.CurrentEndpoint!;
            endpoint.AfterPostForTests = () =>
                throw new InvalidOperationException("Expected test post-then-throw failure.");

            Assert.That(endpoint.Send(Message(1)), Is.EqualTo(SendResult.Ok));

            await transportStopped.Task.WaitAsync(Timeout);
            Assert.Multiple(() =>
            {
                Assert.That(endpoint.OutstandingCommitMessagesForTests, Is.Zero,
                    "the post-then-throw path must release the reservation exactly once");
                Assert.That(client.IsStarted, Is.False);
            });
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task ClientSendReturnBarrier_DefersTheServerCallbackUntilSendReturns()
    {
        var id = "stage3-barrier-client-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();

        try
        {
            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);
            var endpoint = client.CurrentEndpoint!;

            using var senderGate = new ManualResetEventSlim(false);
            var workerWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            endpoint.BeforeSendReturnsForTests = () => senderGate.Wait(Timeout);
            endpoint.BeforeSendReturnWaitForTests = () => workerWaiting.TrySetResult();

            var send = Task.Run(() => endpoint.Send(Message(1)));
            await workerWaiting.Task.WaitAsync(Timeout);

            Assert.That(serverHandler.ReceivedCount, Is.Zero,
                "the peer callback must not begin while the sender is still inside Send");

            senderGate.Set();
            Assert.That(await send.WaitAsync(Timeout), Is.EqualTo(SendResult.Ok));
            await serverHandler.Received.Task.WaitAsync(Timeout);
            Assert.That(serverHandler.ReceivedCount, Is.EqualTo(1));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public async Task ServerSendReturnBarrier_DefersTheClientCallbackUntilSendReturns()
    {
        var id = "stage3-barrier-server-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(id, Logger, MemoryRental.Shared);
        var client = new RawUnreliableDirectClientTransport(id, Logger, MemoryRental.Shared);
        var serverHandler = new RecordingHandler();
        var clientHandler = new RecordingHandler();

        try
        {
            Assert.That(server.Init(_ => serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Endpoint!.Send(Message(0)), Is.EqualTo(SendResult.Ok));
            await serverHandler.Started.Task.WaitAsync(Timeout);
            var endpoint = (RawUnreliableDirectServerEndpoint)serverHandler.Endpoint!;

            using var senderGate = new ManualResetEventSlim(false);
            var workerWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            endpoint.BeforeSendReturnsForTests = () => senderGate.Wait(Timeout);
            endpoint.BeforeSendReturnWaitForTests = () => workerWaiting.TrySetResult();

            var send = Task.Run(() => endpoint.Send(Message(1)));
            await workerWaiting.Task.WaitAsync(Timeout);

            Assert.That(clientHandler.ReceivedCount, Is.Zero,
                "the peer callback must not begin while the sender is still inside Send");

            senderGate.Set();
            Assert.That(await send.WaitAsync(Timeout), Is.EqualTo(SendResult.Ok));
            await clientHandler.Received.Task.WaitAsync(Timeout);
            Assert.That(clientHandler.ReceivedCount, Is.EqualTo(1));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static UnionDataList Message(int value)
    {
        var message = EmptyMessage();
        message.PutLast(value);
        return message;
    }

    private static UnionDataList EmptyMessage()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        private int _receivedCount;

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Stopped { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IRawUnreliableEndpoint? Endpoint { get; private set; }

        public int ReceivedCount => Volatile.Read(ref _receivedCount);

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Endpoint = endpoint;
            Started.TrySetResult();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            Interlocked.Increment(ref _receivedCount);
            Received.TrySetResult();
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
            Stopped.TrySetResult();
        }
    }
}
