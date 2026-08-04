using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

public abstract class RawUnreliableNoAckConformanceTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(2);

    protected abstract IRawUnreliableNoAckConformanceAdapter CreateAdapter();

    [Test]
    public void Start_NullCallback_ThrowsWithoutChangingLifecycle()
    {
        using var fixture = CreateAdapter().CreateFixture();

        Assert.Throws<ArgumentNullException>(() => fixture.Server.Start(null!));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });
    }

    [Test]
    public void Stop_BeforeStart_IsNoOpAndDoesNotConsumeStart()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.That(fixture.Server.Stop(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        });
    }

    [Test]
    public void Start_IsOneTimeAndNormalStopRemainsValid()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.False);
        Assert.That(fixture.Server.Stop(new UserIntention("test", "normal stop")), Is.True);
        Assert.That(stopped.Task.Wait(DeliveryTimeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.Start(_ => { }), Is.False);
        });
    }

    [Test]
    public async Task Start_ConcurrentCalls_HasExactlyOneWinner()
    {
        using var fixture = CreateAdapter().CreateFixture();

        var starts = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => fixture.Server.Start(_ => { })));
        var results = await Task.WhenAll(starts);

        Assert.That(results.Count(result => result), Is.EqualTo(1));
        Assert.That(fixture.Server.Stop(), Is.True);
    }

    [Test]
    public void FailNextStart_InvalidatesWithoutStoppedNotification()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IConformanceControl>(fixture.Server);
        var stopped = false;

        control.FailNextStart();

        Assert.That(fixture.Server.Start(_ => stopped = true), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.False);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(stopped, Is.False);
            Assert.That(fixture.Server.Stop(), Is.False);
        });
    }

    [Test]
    public async Task InjectUnrecoverableFailure_InvalidatesAndNotifies()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var control = GetControl<IConformanceControl>(fixture.Server);
        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        control.InjectUnrecoverableFailure();

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.False);
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.Stop(), Is.False);
        });
    }

    [Test]
    public void ClientStart_DoesNotRequireRunningServer()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.Start(_ => { }), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });
    }

    [Test]
    public void TrySend_WhenUnavailable_ReturnsError()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Error));
        Assert.That(fixture.Server.TrySend(new ForeignEndPoint(), CreateMessage(fixture.Server, 2)),
            Is.EqualTo(SendResult.Error));
    }

    [Test]
    public void TrySend_AfterNormalStop_ReturnsError()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        Start(fixture.Server, client);

        Assert.That(client.Stop(), Is.True);
        Assert.That(fixture.Server.Stop(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Error));
            Assert.That(fixture.Server.TrySend(new ForeignEndPoint(), CreateMessage(fixture.Server, 2)),
                Is.EqualTo(SendResult.Error));
        });
    }

    [Test]
    public void TrySend_WhenRunning_ReportsInvalidMessageAndInvalidAddress()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        Start(fixture.Server, client);

        Assert.Multiple(() =>
        {
            Assert.That(client.TrySend(null!), Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(fixture.Server.TrySend(new ForeignEndPoint(), CreateMessage(fixture.Server, 3)),
                Is.EqualTo(SendResult.InvalidAddress));
            Assert.That(client.IsValid, Is.True);
            Assert.That(fixture.Server.IsValid, Is.True);
        });
    }

    [Test]
    public void TrySend_MessageTooBigTakesPrecedenceOverServerAddress()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        Start(fixture.Server, client);

        Assert.Multiple(() =>
        {
            Assert.That(client.TrySend(CreateOversizedMessage(client)), Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(fixture.Server.TrySend(new ForeignEndPoint(), CreateOversizedMessage(fixture.Server)),
                Is.EqualTo(SendResult.MessageTooBig));
        });
    }

    [Test]
    public async Task ReliableLink_AcceptsAndDeliversMessageAtExactSizeLimit()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                received.TrySetResult(message.GetDataSize());
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        var message = CreateExactLimitMessage(client, client.MessageMaxByteSize);
        Assert.That(message.GetDataSize(), Is.EqualTo(client.MessageMaxByteSize));
        Assert.That(client.TrySend(message), Is.EqualTo(SendResult.Ok));

        Assert.That(await received.Task.WaitAsync(DeliveryTimeout), Is.EqualTo(client.MessageMaxByteSize));
    }

    [Test]
    public async Task ReliableLink_DeliversEmptyMessageAndPreservesComplexContentBothDirections()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var serverReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var emptyReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = CreateComplexMessage(client);
        var response = CreateComplexMessage(fixture.Server);
        var expectedResponse = response.Clone(fixture.Server.Memory.CollectablePool);

        fixture.Server.OnReceived += (endpoint, message) =>
        {
            if (message.Elements.Count == 0)
            {
                emptyReceived.TrySetResult(true);
                message.Release();
                return;
            }

            try
            {
                serverReceived.TrySetResult(message.EqualByContent(expected));
                Assert.That(fixture.Server.TrySend(endpoint, response), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
                expected.Release();
            }
        };
        client.OnReceived += message =>
        {
            try
            {
                clientReceived.TrySetResult(message.EqualByContent(expectedResponse));
            }
            finally
            {
                message.Release();
                expectedResponse.Release();
            }
        };

        Start(fixture.Server, client);

        Assert.That(client.TrySend(CreateEmptyMessage(client)), Is.EqualTo(SendResult.Ok));
        Assert.That(client.TrySend(expected.Clone(client.Memory.CollectablePool)), Is.EqualTo(SendResult.Ok));

        Assert.That(await emptyReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(await serverReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(await clientReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
    }

    [Test]
    public async Task ReliableLink_DeliversExactlyOnceInFifoOrder()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        const int messageCount = 8;
        var received = new ConcurrentQueue<int>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                received.Enqueue(value);
                if (received.Count == messageCount)
                    allReceived.TrySetResult();
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        for (var i = 0; i < messageCount; i++)
            Assert.That(client.TrySend(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));

        await allReceived.Task.WaitAsync(DeliveryTimeout);
        Assert.That(received, Is.EqualTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task ReliableLink_DeliversExactlyOnceInFifoOrderForServerReplies()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        const int messageCount = 8;
        var received = new ConcurrentQueue<int>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (endpoint, message) =>
        {
            try
            {
                for (var i = 0; i < messageCount; i++)
                    Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, i)), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
            }
        };
        client.OnReceived += message =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                received.Enqueue(value);
                if (received.Count == messageCount)
                    allReceived.TrySetResult();
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        await allReceived.Task.WaitAsync(DeliveryTimeout);
        Assert.That(received, Is.EqualTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task ServerReplyRoute_RemainsUsableAfterReceiveCallbackReturns()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var route = new TaskCompletionSource<IEndPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replyReceived = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (endpoint, message) =>
        {
            try
            {
                route.TrySetResult(endpoint);
            }
            finally
            {
                message.Release();
            }
        };
        client.OnReceived += message =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                replyReceived.TrySetResult(value);
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        var endpoint = await route.Task.WaitAsync(DeliveryTimeout);

        Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, 2)), Is.EqualTo(SendResult.Ok));
        Assert.That(await replyReceived.Task.WaitAsync(DeliveryTimeout), Is.EqualTo(2));
    }

    [Test]
    public async Task ReceiveHandlerException_DoesNotStopTransportOrPreventLaterDelivery()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var secondDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                if (value == 1)
                    throw new InvalidOperationException("Expected conformance test exception.");
                secondDelivery.TrySetResult();
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(client.TrySend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));

        await secondDelivery.Task.WaitAsync(DeliveryTimeout);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
        });
    }

    [Test]
    public async Task ClientReceiveHandlerException_DoesNotStopTransportOrPreventLaterDelivery()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var secondDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (endpoint, message) =>
        {
            try
            {
                Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, 1)), Is.EqualTo(SendResult.Ok));
                Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, 2)), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
            }
        };
        client.OnReceived += message =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                if (value == 1)
                    throw new InvalidOperationException("Expected conformance test exception.");
                secondDelivery.TrySetResult();
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));

        await secondDelivery.Task.WaitAsync(DeliveryTimeout);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });
    }

    [Test]
    public async Task ReceiveCallbacks_AreNonReentrantWhenHandlerSends()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var secondDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackDepth = 0;
        var maximumDepth = 0;
        fixture.Server.OnReceived += (_, message) =>
        {
            var depth = Interlocked.Increment(ref callbackDepth);
            SetMaximum(ref maximumDepth, depth);
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                if (value == 1)
                    Assert.That(client.TrySend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
                else
                    secondDelivery.TrySetResult();
            }
            finally
            {
                message.Release();
                Interlocked.Decrement(ref callbackDepth);
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        await secondDelivery.Task.WaitAsync(DeliveryTimeout);
        Assert.That(maximumDepth, Is.EqualTo(1));
    }

    [Test]
    public async Task ClientReceiveCallbacks_AreGloballySerialized()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        using var release = new ManualResetEventSlim();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeCallbacks = 0;
        var concurrentCallbacks = 0;
        var completedCallbacks = 0;
        fixture.Server.OnReceived += (endpoint, message) =>
        {
            try
            {
                Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, 1)), Is.EqualTo(SendResult.Ok));
                Assert.That(fixture.Server.TrySend(endpoint, CreateMessage(fixture.Server, 2)), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
            }
        };
        client.OnReceived += message =>
        {
            try
            {
                if (Interlocked.Increment(ref activeCallbacks) > 1)
                    Interlocked.Exchange(ref concurrentCallbacks, 1);
                firstEntered.TrySetResult();
                release.Wait(DeliveryTimeout);
            }
            finally
            {
                Interlocked.Decrement(ref activeCallbacks);
                message.Release();
                if (Interlocked.Increment(ref completedCallbacks) == 2)
                    allCompleted.TrySetResult();
            }
        };

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));
        await firstEntered.Task.WaitAsync(DeliveryTimeout);
        await Task.Delay(100);
        release.Set();
        await allCompleted.Task.WaitAsync(DeliveryTimeout);

        Assert.That(concurrentCallbacks, Is.Zero);
    }

    [Test]
    public async Task ConcurrentTrySend_DeliversEveryAcceptedMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        const int messageCount = 32;
        var received = new ConcurrentDictionary<int, byte>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                received.TryAdd(value, 0);
                if (received.Count == messageCount)
                    allReceived.TrySetResult();
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        var sends = Enumerable.Range(0, messageCount)
            .Select(value => Task.Run(() => client.TrySend(CreateMessage(client, value))));
        var results = await Task.WhenAll(sends);

        Assert.That(results, Is.All.EqualTo(SendResult.Ok));
        await allReceived.Task.WaitAsync(DeliveryTimeout);
        Assert.That(received.Keys, Is.EquivalentTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task StatusReads_AreSafeWhileStopping()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        Start(fixture.Server, client);

        using var cancellation = new CancellationTokenSource();
        var readers = Enumerable.Range(0, 4).Select(readerIndex => Task.Run(() =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                var serverValid = fixture.Server.IsValid;
                var serverStarted = fixture.Server.IsStarted;
                var clientValid = client.IsValid;
                var clientStarted = client.IsStarted;
            }
        }));

        Assert.That(await Task.Run(() => fixture.Server.Stop()).WaitAsync(DeliveryTimeout), Is.True);
        cancellation.Cancel();
        await Task.WhenAll(readers).WaitAsync(DeliveryTimeout);
    }

    [Test]
    public async Task ClientTrySend_IsSafeWhenRacingWithStop()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        Start(fixture.Server, client);

        var sends = Enumerable.Range(0, 32)
            .Select(value => Task.Run(() => client.TrySend(CreateMessage(client, value))));
        var stop = Task.Run(() => client.Stop());
        var results = await Task.WhenAll(sends);

        Assert.That(await stop.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(results.All(result =>
            result is SendResult.Ok or SendResult.Error or SendResult.BufferOverflow), Is.True);
    }

    [Test]
    public async Task Stop_CanBeCalledFromReceiveHandler()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                Assert.That(fixture.Server.Stop(), Is.True);
            }
            finally
            {
                message.Release();
            }
        };

        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        StartClient(client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        await stopped.Task.WaitAsync(DeliveryTimeout);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsStarted, Is.False);
            Assert.That(fixture.Server.IsValid, Is.True);
        });
    }

    [Test]
    public async Task AcceptedSend_DoesNotBeginReceiveCallbackBeforeTrySendReturns()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var callbackObservedReturnedSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientControl = GetControl<IRawUnreliableNoAckConformanceControl>(client);
        var beforeCommitReached = clientControl.BeforeSendCommitGate.Arm();
        var sendReturned = 0;
        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                callbackObservedReturnedSend.TrySetResult(Volatile.Read(ref sendReturned) == 1);
            }
            finally
            {
                message.Release();
            }
        };

        Start(fixture.Server, client);
        Task.Run(() => { Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok)); });
        Volatile.Write(ref sendReturned, 1);
        await beforeCommitReached.WaitAsync(DeliveryTimeout);
        clientControl.BeforeSendCommitGate.Reset();

        Assert.That(await callbackObservedReturnedSend.Task.WaitAsync(DeliveryTimeout), Is.True);
    }

    [Test]
    public async Task ServerCallbacks_AreGloballySerializedAcrossClients()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var firstClient = fixture.CreateClient();
        var secondClient = fixture.CreateClient();
        EnableReliable(firstClient);
        EnableReliable(secondClient);

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var activeCallbacks = 0;
        var concurrentCallbacks = 0;
        var completedCallbacks = 0;

        fixture.Server.OnReceived += (_, message) =>
        {
            try
            {
                if (Interlocked.Increment(ref activeCallbacks) > 1)
                    Interlocked.Exchange(ref concurrentCallbacks, 1);
                firstEntered.TrySetResult();
                release.Wait(DeliveryTimeout);
            }
            finally
            {
                Interlocked.Decrement(ref activeCallbacks);
                message.Release();
                if (Interlocked.Increment(ref completedCallbacks) == 2)
                    allCompleted.TrySetResult();
            }
        };

        Start(fixture.Server, firstClient);
        StartClient(secondClient);
        Assert.That(firstClient.TrySend(CreateMessage(firstClient, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(secondClient.TrySend(CreateMessage(secondClient, 2)), Is.EqualTo(SendResult.Ok));

        await firstEntered.Task.WaitAsync(DeliveryTimeout);
        await Task.Delay(200);
        release.Set();
        await allCompleted.Task.WaitAsync(DeliveryTimeout);

        Assert.That(concurrentCallbacks, Is.Zero);
    }

    [Test]
    public async Task Stop_PreventsBlockedReceiveCallbackFromBeginning()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        EnableReliable(client);

        var callbackBegan = false;
        fixture.Server.OnReceived += (_, message) =>
        {
            callbackBegan = true;
            message.Release();
        };
        var control = GetControl<IRawUnreliableNoAckConformanceControl>(fixture.Server);
        var reached = control.AfterReceivedGate.Arm();

        Start(fixture.Server, client);
        Assert.That(client.TrySend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await reached.WaitAsync(DeliveryTimeout);

        Assert.That(await Task.Run(() => fixture.Server.Stop()).WaitAsync(DeliveryTimeout), Is.True);
        control.AfterReceivedGate.Reset();
        await Task.Delay(100);

        Assert.That(callbackBegan, Is.False);
    }

    private static void EnableReliable(IRawUnreliableNoAckClient client)
    {
        var control = GetControl<IRawUnreliableNoAckClientConformanceControl>(client);
        if (!control.TryMakeReliable())
            Assert.Ignore("The implementation does not provide reliable debug mode.");
    }

    private static void Start(IRawUnreliableNoAckServer server, IRawUnreliableNoAckClient client)
    {
        Assert.That(server.Start(_ => { }), Is.True);
        StartClient(client);
    }

    private static void StartClient(IRawUnreliableNoAckClient client)
    {
        Assert.That(client.Start(_ => { }), Is.True);
    }

    private static UnionDataList CreateEmptyMessage(ITransport transport)
    {
        return transport.Memory.CollectablePool.Acquire<UnionDataList>();
    }

    private static UnionDataList CreateMessage(ITransport transport, int value)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(value);
        return message;
    }

    private static UnionDataList CreateComplexMessage(ITransport transport)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(42);
        message.PutLast(true);
        message.PutFirst("RawUnreliableNoAck");
        return message;
    }

    private static UnionDataList CreateOversizedMessage(ITransport transport)
    {
        var message = CreateEmptyMessage(transport);
        var bytes = transport.Memory.ByteArraysPool.Acquire(transport is IRawUnreliableNoAckClient client
            ? client.MessageMaxByteSize
            : ((IRawUnreliableNoAckServer)transport).MessageMaxByteSize);
        message.PutLast(new UnionData(bytes));
        return message;
    }

    private static UnionDataList CreateExactLimitMessage(ITransport transport, int limit)
    {
        var empty = CreateEmptyMessage(transport);
        if (empty.GetDataSize() == limit)
            return empty;
        empty.Release();

        for (var overhead = 1; overhead <= 8; overhead++)
        {
            var byteCount = limit - overhead;
            if (byteCount <= 0)
                continue;

            var message = CreateEmptyMessage(transport);
            var bytes = transport.Memory.ByteArraysPool.Acquire(byteCount);
            message.PutLast(new UnionData(bytes));
            if (message.GetDataSize() == limit)
                return message;
            message.Release();
        }

        throw new AssertionException($"Unable to construct a UnionDataList of exactly {limit} bytes.");
    }

    private static TControl GetControl<TControl>(ITransport transport)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    private static void SetMaximum(ref int target, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (current >= candidate || Interlocked.CompareExchange(ref target, candidate, current) == current)
                return;
        }
    }

    private sealed class ForeignEndPoint : IEndPoint
    {
        public bool Equals(IEndPoint? other) => ReferenceEquals(this, other);

        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}

[TestFixture]
public sealed class DirectRawUnreliableNoAckConformanceTests : RawUnreliableNoAckConformanceTests
{
    protected override IRawUnreliableNoAckConformanceAdapter CreateAdapter()
    {
        return new DirectRawUnreliableNoAckConformanceAdapter();
    }
}
