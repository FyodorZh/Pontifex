using System.Collections.Concurrent;
using Pontifex.Raw;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Shared, implementation-agnostic conformance suite for the RawUnreliable
/// Ack and NoAck contract variants. Each concrete adapter derives the test
/// fixture and supplies a linked server-client topology. Reliable-mode tests
/// call <see cref="IRawUnreliableTransportConformanceControl.TryMakeReliable"/>
/// before startup and are skipped when the capability is unavailable.
/// </summary>
public abstract class RawUnreliableConformanceTests<TServer>
    where TServer : IRawUnreliableTransport
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(2);

    protected abstract IRawUnreliableConformanceAdapter<TServer> CreateAdapter();

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

        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
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

        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
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

        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
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

        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
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

        Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });
    }

    [Test]
    public void TrySend_AfterNormalStop_ReturnsError()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(client.Stop(), Is.True);
        Assert.That(fixture.Server.Stop(), Is.True);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Error));
    }

    [Test]
    public void TrySend_WhenRunning_ReportsInvalidMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.UnreliableSend(null!), Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(client.IsValid, Is.True);
        });
    }

    [Test]
    public void TrySend_OversizedMessage_ReturnsMessageTooBig()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.UnreliableSend(CreateOversizedMessage(client)), Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(client.IsValid, Is.True);
        });
    }

    [Test]
    public async Task ReliableLink_AcceptsAndDeliversMessageAtExactSizeLimit()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new SizeRecordingHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var message = CreateExactLimitMessage(client, client.MessageMaxByteSize);
        Assert.That(message.GetDataSize(), Is.EqualTo(client.MessageMaxByteSize));

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(message), Is.EqualTo(SendResult.Ok));

        await serverHandler.Received.Task.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.LastSize, Is.EqualTo(client.MessageMaxByteSize));
    }

    [Test]
    public async Task ReliableLink_DeliversEmptyMessageAndPreservesComplexContentBothDirections()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var server = fixture.Server;
        if (!EnableReliable(client, server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");

        var emptyReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = CreateComplexMessage(client);
        var response = CreateComplexMessage(server);
        var expectedResponse = response.Clone(server.Memory.CollectablePool);

        var serverHandler = new EchoServerHandler(fixture, expected, response, serverReceived, emptyReceived);
        var clientHandler = new AssertContentClientHandler(fixture, expectedResponse, clientReceived);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateEmptyMessage(client)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.UnreliableSend(expected.Clone(client.Memory.CollectablePool)), Is.EqualTo(SendResult.Ok));

        Assert.That(await emptyReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(await serverReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(await clientReceived.Task.WaitAsync(DeliveryTimeout), Is.True);
    }

    [Test]
    public async Task ReliableLink_DeliversExactlyOnceInFifoOrder()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        const int messageCount = 8;
        var endpoint = WaitForEndpoint(clientHandler);
        for (var i = 0; i < messageCount; i++)
            Assert.That(endpoint.UnreliableSend(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.ReceivedCount == messageCount);
        Assert.That(serverHandler.ReceivedValues, Is.EqualTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task ReliableLink_DeliversExactlyOnceInFifoOrderForServerReplies()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new ReplyAllServerHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => clientHandler.ReceivedCount == 8);
        Assert.That(clientHandler.ReceivedValues, Is.EqualTo(Enumerable.Range(0, 8)));
    }

    [Test]
    public async Task ServerReplyRoute_RemainsUsableAfterReceiveCallbackReturns()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.That(serverHandler.Endpoint!.UnreliableSend(CreateMessage(fixture.Server, 2)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => clientHandler.ReceivedCount == 1);
        Assert.That(clientHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public async Task ReceiveHandlerException_DoesNotStopTransportOrPreventLaterDelivery()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new ThrowingHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.DeliveredCount == 1);
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
        var clientHandler = new ThrowingHandler(fixture);
        var serverHandler = new ServerSendsValuesHandler(fixture, 1, 2);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => clientHandler.DeliveredCount == 1);
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
        var clientHandler = new EchoOnceClientHandler(fixture, client);
        var serverHandler = new ServerReentrancyHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        await serverHandler.SecondDelivery.Task.WaitAsync(DeliveryTimeout);
        Assert.That(serverHandler.MaximumDepth, Is.EqualTo(1));
    }

    [Test]
    public async Task ClientReceiveCallbacks_AreGloballySerialized()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new BlockingClientHandler(fixture);
        var serverHandler = new ServerSendsValuesHandler(fixture, 1, 2);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 0)), Is.EqualTo(SendResult.Ok));

        await clientHandler.FirstEntered.Task.WaitAsync(DeliveryTimeout);
        await Task.Delay(100);
        clientHandler.Release.Set();
        await clientHandler.AllCompleted.Task.WaitAsync(DeliveryTimeout);

        Assert.That(clientHandler.ConcurrentCallbacks, Is.Zero);
    }

    [Test]
    public async Task ConcurrentTrySend_DeliversEveryAcceptedMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        const int messageCount = 32;
        var endpoint = WaitForEndpoint(clientHandler);

        var sends = Enumerable.Range(0, messageCount)
            .Select(value => Task.Run(() => endpoint.UnreliableSend(CreateMessage(client, value))));
        var results = await Task.WhenAll(sends);

        Assert.That(results, Is.All.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.ReceivedCount == messageCount);
        Assert.That(serverHandler.ReceivedValues, Is.EquivalentTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task StatusReads_AreSafeWhileStopping()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
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
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        var sends = Enumerable.Range(0, 32)
            .Select(value => Task.Run(() => endpoint.UnreliableSend(CreateMessage(client, value))));
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
        var serverHandler = new StopFromReceiveServerHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);

        var stopped = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.That(fixture.Server.Start(reason => stopped.TrySetResult(reason)), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

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
        var serverHandler = new SendReturnedObservingHandler(fixture, new ReturnedFlag());
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        var endpointControl = GetEndpointControl<IRawUnreliableEndpointConformanceControl>(endpoint);
        var beforeCommitReached = endpointControl.BeforeSendCommitGate.Arm();

        _ = Task.Run(() => endpoint.UnreliableSend(CreateMessage(client, 1)));
        await beforeCommitReached.WaitAsync(DeliveryTimeout);
        serverHandler.Flag.Value = 1;
        endpointControl.BeforeSendCommitGate.Reset();

        Assert.That(await serverHandler.Observed.Task.WaitAsync(DeliveryTimeout), Is.True);
    }

    [Test]
    public async Task ServerCallbacks_AreGloballySerializedAcrossClients()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var firstClient = fixture.CreateClient();
        var secondClient = fixture.CreateClient();
        if (!EnableReliable(firstClient, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        if (!EnableReliable(secondClient, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");

        var probe = new ServerSerializationProbe();
        var created = new ConcurrentQueue<BlockingServerHandler>();
        Assert.That(fixture.InitServer((_, _) =>
        {
            var handler = new BlockingServerHandler(fixture, probe);
            created.Enqueue(handler);
            return handler;
        }), Is.True);

        var firstClientHandler = new RecordingTestHandler(fixture);
        var secondClientHandler = new RecordingTestHandler(fixture);
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Start(fixture.Server, firstClient);
        Assert.That(secondClient.Start(_ => { }), Is.True);

        var firstEndpoint = WaitForEndpoint(firstClientHandler);
        var secondEndpoint = WaitForEndpoint(secondClientHandler);
        Assert.That(firstEndpoint.UnreliableSend(CreateMessage(firstClient, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(secondEndpoint.UnreliableSend(CreateMessage(secondClient, 2)), Is.EqualTo(SendResult.Ok));

        await probe.FirstEntered.Task.WaitAsync(DeliveryTimeout);
        await Task.Delay(200);
        probe.Release.Set();
        await probe.AllCompleted.Task.WaitAsync(DeliveryTimeout);

        Assert.Multiple(() =>
        {
            Assert.That(probe.ConcurrentCallbacks, Is.Zero);
            Assert.That(created.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Stop_PreventsBlockedReceiveCallbackFromBeginning()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new BlockingPreReceiveServerHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.Reached != null);
        await serverHandler.Reached!.WaitAsync(DeliveryTimeout);

        Assert.That(await Task.Run(() => fixture.Server.Stop()).WaitAsync(DeliveryTimeout), Is.True);
        serverHandler.Gate!.Reset();
        await Task.Delay(100);

        Assert.That(serverHandler.CallbackBegan, Is.False);
    }

    [Test]
    public void Init_NullArgument_ThrowsWithoutChangingLifecycle()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.Throws<ArgumentNullException>(() => client.Init(null!));
        Assert.Throws<ArgumentNullException>(() => fixture.InitServer(null!));
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });

        Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.True);
        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
    }

    [Test]
    public void Init_IsOneTime_SecondCallReturnsFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.True);
        Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.False);
        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.True);
        Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.False);
    }

    [Test]
    public void Init_AfterStart_ReturnsFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);

        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.True);
        Start(fixture.Server, client);

        Assert.Multiple(() =>
        {
            Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.False);
            Assert.That(fixture.InitServer((_, _) => (IRawUnreliableHandler?)null), Is.False);
        });
    }

    [Test]
    public void Start_BeforeInit_ReturnsFalseAndInvalidates()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();

        Assert.That(client.Start(_ => { }), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.Init(new RecordingTestHandler(fixture)), Is.False);
            Assert.That(client.Start(_ => { }), Is.False);
        });
    }

    [Test]
    public void Client_OnStartedAfterStart_EndpointIsValidAndUsable()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.IsValid, Is.True);
            Assert.That(endpoint.RemoteEndPoint, Is.Not.Null);
            Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        });
    }

    [Test]
    public async Task Client_OnReceivedObservesOnStartedCompleted()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new ServerSendsValuesHandler(fixture, 5);
        var clientHandler = new OnStartedOrderClientHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => clientHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.SawStartedCompleted, Is.True);
            Assert.That(clientHandler.ReceivedValues, Is.EqualTo(new[] { 5 }));
        });
    }

    [Test]
    public void Client_OnStartedThrows_StopsClientTransport_WithoutHandlerOnStopped()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var handler = new ThrowingOnStartedHandler(fixture);
        var onStopped = 0;
        Assert.That(client.Init(handler), Is.True);
        Assert.That(client.Start(_ => Interlocked.Increment(ref onStopped)), Is.True);

        WaitUntil(() => Volatile.Read(ref onStopped) == 1);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsStarted, Is.False);
            Assert.That(handler.StoppedReason, Is.Null);
            Assert.That(client.IsValid, Is.True);
        });
    }

    [Test]
    public void Server_NewSource_OnStartedThenOnReceived_WithFactorySourceEqualsRemoteEndPoint()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var handler = new SourceCheckingHandler(fixture);
        IEndPoint? factorySource = null;
        Assert.That(fixture.InitServer((source, _) =>
        {
            factorySource = source;
            handler.Source = source;
            return handler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => handler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(factorySource, Is.Not.Null);
            Assert.That(handler.OnStartedRemoteEqualsSource, Is.True);
            Assert.That(handler.SawOnStartedCompleted, Is.True);
            Assert.That(handler.ReceivedValues, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public void Server_FactoryNull_DeclinesMessage_AndRetriesLater()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) =>
            Interlocked.Increment(ref factoryCalls) == 1 ? (IRawUnreliableHandler?)null : serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => Volatile.Read(ref factoryCalls) == 1);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(2));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public void Server_FactoryThrows_DropsMessage_AndRetriesLater()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) =>
        {
            if (Interlocked.Increment(ref factoryCalls) == 1)
                throw new InvalidOperationException("Expected conformance test factory exception.");
            return serverHandler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => Volatile.Read(ref factoryCalls) == 1);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(2));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public void Server_HandlerOnStartedThrows_DropsTrigger_AndRecreatesRouteLater()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var throwingHandler = new ThrowingOnStartedHandler(fixture);
        var goodHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) =>
        {
            return Interlocked.Increment(ref factoryCalls) == 1
                ? (IRawUnreliableHandler)throwingHandler
                : goodHandler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => Volatile.Read(ref factoryCalls) == 1);
        Assert.That(throwingHandler.StoppedReason, Is.Null);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => goodHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(2));
            Assert.That(goodHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
            Assert.That(throwingHandler.StoppedReason, Is.Null);
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
        });
    }

    [Test]
    public void Server_EndpointStop_RecreatesRouteForLaterMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var handlers = new ConcurrentQueue<EndpointStopOnReceiveHandler>();
        Assert.That(fixture.InitServer((_, _) =>
        {
            var handler = new EndpointStopOnReceiveHandler(fixture);
            handlers.Enqueue(handler);
            return handler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        var firstHandler = WaitForHandler(handlers, 1);
        WaitUntil(() => firstHandler.ReceivedCount == 1 && firstHandler.OnStoppedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
            Assert.That(firstHandler.ReceivedValues, Is.EqualTo(new[] { 1 }));
        });

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        var secondHandler = WaitForHandler(handlers, 2);
        WaitUntil(() => secondHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(secondHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
            Assert.That(firstHandler.OnStoppedCount, Is.EqualTo(1));
            Assert.That(firstHandler.StoppedReason, Is.Not.Null);
        });
    }

    [Test]
    public async Task Server_SameRouteMessagesDuringCreation_QueueBehindTrigger()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) => { Interlocked.Increment(ref factoryCalls); return serverHandler; }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");

        var serverControl = GetControl<IRawUnreliableTransportConformanceControl>(fixture.Server);
        var gateReached = serverControl.BeforeHandlerStartedGate.Arm();
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await gateReached.WaitAsync(DeliveryTimeout);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 3)), Is.EqualTo(SendResult.Ok));

        await Task.Delay(200);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(1));
            Assert.That(serverHandler.ReceivedCount, Is.Zero);
        });

        serverControl.BeforeHandlerStartedGate.Reset();
        WaitUntil(() => serverHandler.ReceivedCount == 3);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(1));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 1, 2, 3 }));
        });
    }

    [Test]
    public void Endpoint_Stop_ReturnsTrueOnce_ThenFalse()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.Stop(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(endpoint.Stop(), Is.False);
            Assert.That(endpoint.IsValid, Is.False);
            Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Error));
        });
    }

    [Test]
    public void Endpoint_StopNull_SuppliesUnknownReasonToOnStopped()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.Stop(), Is.True);
        WaitUntil(() => clientHandler.StoppedReason != null);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.StoppedReason, Is.InstanceOf<Unknown>());
            Assert.That(clientHandler.StoppedReason!.Type, Does.Contain("Unknown"));
            Assert.That(clientHandler.StoppedReason!.ToString(), Does.Contain("Unknown"));
        });
    }

    [Test]
    public void Client_EndpointStop_StopsClientTransport()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        var onStopped = 0;
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(fixture.Server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => Interlocked.Increment(ref onStopped)), Is.True);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.Stop(new UserIntention("test", "stop")), Is.True);

        WaitUntil(() => !client.IsStarted);
        WaitUntil(() => Volatile.Read(ref onStopped) == 1);
        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(clientHandler.StoppedReason, Is.Not.Null);
        });
    }

    [Test]
    public async Task Server_EndpointStop_KeepsServerRunning()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new EndpointStopOnReceiveHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.OnStoppedCount == 1);
        await Task.Delay(200);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.True);
        });
    }

    [Test]
    public void OnStopped_InvokedExactlyOncePerEndpoint()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new OnStoppedCountingHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        WaitForEndpoint(clientHandler);
        Assert.That(client.Stop(), Is.True);
        WaitUntil(() => clientHandler.OnStoppedCount == 1);
        Assert.That(clientHandler.OnStoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void TransportStop_NotifiesEveryEndpointHandler()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var firstClient = fixture.CreateClient();
        var secondClient = fixture.CreateClient();
        var firstClientHandler = new RecordingTestHandler(fixture);
        var secondClientHandler = new RecordingTestHandler(fixture);
        var serverHandlers = new ConcurrentQueue<ServerRouteRecordingHandler>();
        Assert.That(fixture.InitServer((_, _) =>
        {
            var handler = new ServerRouteRecordingHandler(fixture);
            serverHandlers.Enqueue(handler);
            return handler;
        }), Is.True);
        if (!EnableReliable(firstClient, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        if (!EnableReliable(secondClient, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(firstClient.Init(firstClientHandler), Is.True);
        Assert.That(secondClient.Init(secondClientHandler), Is.True);
        Start(fixture.Server, firstClient);
        Assert.That(secondClient.Start(_ => { }), Is.True);

        var firstEndpoint = WaitForEndpoint(firstClientHandler);
        var secondEndpoint = WaitForEndpoint(secondClientHandler);
        Assert.That(firstEndpoint.UnreliableSend(CreateMessage(firstClient, 1)), Is.EqualTo(SendResult.Ok));
        Assert.That(secondEndpoint.UnreliableSend(CreateMessage(secondClient, 2)), Is.EqualTo(SendResult.Ok));

        var firstServerHandler = WaitForHandler(serverHandlers, 1);
        var secondServerHandler = WaitForHandler(serverHandlers, 2);
        WaitUntil(() => firstServerHandler.ReceivedCount == 1 && secondServerHandler.ReceivedCount == 1);

        Assert.That(fixture.Server.Stop(), Is.True);
        WaitUntil(() => firstServerHandler.OnStoppedCount == 1 && secondServerHandler.OnStoppedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(firstServerHandler.OnStoppedCount, Is.EqualTo(1));
            Assert.That(secondServerHandler.OnStoppedCount, Is.EqualTo(1));
            Assert.That(fixture.Server.IsValid, Is.True);
            Assert.That(fixture.Server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task BeforeHandlerFactoryGate_IsHitPerFactoryInvocation()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) => { Interlocked.Increment(ref factoryCalls); return serverHandler; }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");

        var serverControl = GetControl<IRawUnreliableTransportConformanceControl>(fixture.Server);
        var gateReached = serverControl.BeforeHandlerFactoryGate.Arm();
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await gateReached.WaitAsync(DeliveryTimeout);

        Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(0));
        serverControl.BeforeHandlerFactoryGate.Reset();

        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(1));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public async Task BeforeHandlerStartedGate_IsHitBeforeOnStarted()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new OnStartedFlagHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, _) => { Interlocked.Increment(ref factoryCalls); return serverHandler; }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");

        var serverControl = GetControl<IRawUnreliableTransportConformanceControl>(fixture.Server);
        var gateReached = serverControl.BeforeHandlerStartedGate.Arm();
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        await gateReached.WaitAsync(DeliveryTimeout);

        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(1));
            Assert.That(serverHandler.OnStartedRan, Is.False);
        });
        serverControl.BeforeHandlerStartedGate.Reset();

        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.OnStartedRan, Is.True);
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public async Task BeforeEndpointStopStateTransitionGate_BlocksEndpointInvalidation()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        var endpointControl = GetEndpointControl<IRawUnreliableEndpointConformanceControl>(endpoint);
        var gateReached = endpointControl.BeforeEndpointStopStateTransitionGate.Arm();

        var stopTask = Task.Run(() => endpoint.Stop());
        await gateReached.WaitAsync(DeliveryTimeout);
        Assert.That(endpoint.IsValid, Is.True);

        endpointControl.BeforeEndpointStopStateTransitionGate.Reset();
        Assert.That(await stopTask.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(endpoint.IsValid, Is.False);
    }

    [Test]
    public async Task BeforeHandlerStoppedGate_BlocksOnStopped()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new OnStoppedCountingHandler(fixture);
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        var endpointControl = GetEndpointControl<IRawUnreliableEndpointConformanceControl>(endpoint);
        var gateReached = endpointControl.BeforeHandlerStoppedGate.Arm();

        var stopTask = Task.Run(() => client.Stop());
        await gateReached.WaitAsync(DeliveryTimeout);
        Assert.That(clientHandler.StoppedReason, Is.Null);

        endpointControl.BeforeHandlerStoppedGate.Reset();
        WaitUntil(() => clientHandler.OnStoppedCount == 1);
        Assert.That(await stopTask.WaitAsync(DeliveryTimeout), Is.True);
        Assert.That(clientHandler.StoppedReason, Is.Not.Null);
    }

    [Test]
    public void TryMakeReliable_TransportWide_AppliesToAllRoutes()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var serverHandler = new RecordingTestHandler(fixture);
        var clientHandler = new RecordingTestHandler(fixture);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Assert.That(fixture.InitServer((_, _) => serverHandler), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Start(fixture.Server, client);

        const int messageCount = 8;
        var endpoint = WaitForEndpoint(clientHandler);
        for (var i = 0; i < messageCount; i++)
            Assert.That(endpoint.UnreliableSend(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.ReceivedCount == messageCount);
        Assert.That(serverHandler.ReceivedValues, Is.EqualTo(Enumerable.Range(0, messageCount)));
    }

    protected static bool EnableReliable(IRawUnreliableClient client, IRawUnreliableTransport server)
    {
        var c = GetControl<IRawUnreliableTransportConformanceControl>(client);
        var s = GetControl<IRawUnreliableTransportConformanceControl>(server);
        if (!c.TryMakeReliable()) return false;
        if (!s.TryMakeReliable()) return false;
        return true;
    }

    protected static void Start(IRawUnreliableTransport server, IRawUnreliableClient client)
    {
        Assert.That(server.Start(_ => { }), Is.True);
        StartClient(client);
    }

    protected static void StartClient(IRawUnreliableClient client)
    {
        Assert.That(client.Start(_ => { }), Is.True);
    }

    protected static IRawUnreliableEndpoint WaitForEndpoint(RawUnreliableTestHandler handler)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (handler.Endpoint == null)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("OnStarted was not invoked within the delivery timeout.");
            Thread.Sleep(10);
        }
        return handler.Endpoint;
    }

    protected static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Condition was not satisfied within the delivery timeout.");
            Thread.Sleep(10);
        }
    }

    protected static T WaitForHandler<T>(ConcurrentQueue<T> queue, int minimumCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(DeliveryTimeout.TotalSeconds);
        while (queue.Count < minimumCount)
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Expected handler was not created within the delivery timeout.");
            Thread.Sleep(10);
        }
        return queue.ToArray()[minimumCount - 1];
    }

    protected static UnionDataList CreateEmptyMessage(IRawTransport transport)
    {
        return transport.Memory.CollectablePool.Acquire<UnionDataList>();
    }

    protected static UnionDataList CreateMessage(IRawTransport transport, int value)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(value);
        return message;
    }

    protected static UnionDataList CreateComplexMessage(IRawTransport transport)
    {
        var message = CreateEmptyMessage(transport);
        message.PutLast(42);
        message.PutLast(true);
        message.PutFirst("RawUnreliable");
        return message;
    }

    protected static UnionDataList CreateOversizedMessage(IRawTransport transport)
    {
        var message = CreateEmptyMessage(transport);
        var bytes = transport.Memory.ByteArraysPool.Acquire(transport.MessageMaxByteSize);
        message.PutLast(new UnionData(bytes));
        return message;
    }

    protected static UnionDataList CreateExactLimitMessage(IRawTransport transport, int limit)
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

    protected static TControl GetControl<TControl>(ITransport transport)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        transport.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    protected static TControl GetEndpointControl<TControl>(IRawUnreliableEndpoint endpoint)
        where TControl : class, IControl
    {
        var controls = new List<IControl>();
        endpoint.GetControls(controls, control => control is TControl);
        return controls.OfType<TControl>().Single();
    }

    protected static void SetMaximum(ref int target, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (current >= candidate || Interlocked.CompareExchange(ref target, candidate, current) == current)
                return;
        }
    }

    protected sealed class RecordingTestHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private readonly TaskCompletionSource _receivedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }

        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public RecordingTestHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
            _receivedSignal.TrySetResult();
        }
    }

    private sealed class SizeRecordingHandler : RawUnreliableTestHandler
    {
        private readonly object _lock = new();

        public int? LastSize { get; private set; }

        public TaskCompletionSource Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SizeRecordingHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try { lock (_lock) { LastSize = message.GetDataSize(); } }
            finally { message.Release(); }
            Received.TrySetResult();
        }
    }

    private sealed class EchoServerHandler : RawUnreliableTestHandler
    {
        private readonly UnionDataList _expected;
        private readonly UnionDataList _response;
        private readonly TaskCompletionSource<bool> _serverReceived;
        private readonly TaskCompletionSource<bool> _emptyReceived;

        public EchoServerHandler(IRawUnreliableConformanceFixture<TServer> fixture,
            UnionDataList expected, UnionDataList response,
            TaskCompletionSource<bool> serverReceived, TaskCompletionSource<bool> emptyReceived)
            : base(fixture)
        {
            _expected = expected;
            _response = response;
            _serverReceived = serverReceived;
            _emptyReceived = emptyReceived;
        }

        public override void OnReceived(UnionDataList message)
        {
            if (message.Elements.Count == 0)
            {
                _emptyReceived.TrySetResult(true);
                message.Release();
                return;
            }

            try
            {
                _serverReceived.TrySetResult(message.EqualByContent(_expected));
                Assert.That(Endpoint!.UnreliableSend(_response), Is.EqualTo(SendResult.Ok));
            }
            finally
            {
                message.Release();
                _expected.Release();
            }
        }
    }

    private sealed class AssertContentClientHandler : RawUnreliableTestHandler
    {
        private readonly UnionDataList _expectedResponse;
        private readonly TaskCompletionSource<bool> _clientReceived;

        public AssertContentClientHandler(IRawUnreliableConformanceFixture<TServer> fixture,
            UnionDataList expectedResponse, TaskCompletionSource<bool> clientReceived)
            : base(fixture)
        {
            _expectedResponse = expectedResponse;
            _clientReceived = clientReceived;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                _clientReceived.TrySetResult(message.EqualByContent(_expectedResponse));
            }
            finally
            {
                message.Release();
                _expectedResponse.Release();
            }
        }
    }

    private sealed class ReplyAllServerHandler : RawUnreliableTestHandler
    {
        private const int ReplyCount = 8;
        private readonly IRawUnreliableConformanceFixture<TServer> _fixture;

        public ReplyAllServerHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                for (var i = 0; i < ReplyCount; i++)
                    Assert.That(Endpoint!.UnreliableSend(CreateMessage(_fixture.Server, i)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    private sealed class ServerSendsValuesHandler : RawUnreliableTestHandler
    {
        private readonly IRawUnreliableConformanceFixture<TServer> _fixture;
        private readonly int[] _values;

        public ServerSendsValuesHandler(IRawUnreliableConformanceFixture<TServer> fixture, params int[] values)
            : base(fixture)
        {
            _fixture = fixture;
            _values = values;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                foreach (var value in _values)
                    Assert.That(Endpoint!.UnreliableSend(CreateMessage(_fixture.Server, value)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    private sealed class ThrowingHandler : RawUnreliableTestHandler
    {
        private readonly object _lock = new();
        private int _deliveredCount;

        public int DeliveredCount { get { lock (_lock) return _deliveredCount; } }

        public ThrowingHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                if (value == 1)
                    throw new InvalidOperationException("Expected conformance test exception.");
                lock (_lock) { _deliveredCount++; }
            }
            finally { message.Release(); }
        }
    }

    private sealed class ServerReentrancyHandler : RawUnreliableTestHandler
    {
        private readonly IRawUnreliableConformanceFixture<TServer> _fixture;
        private int _depth;
        private int _maxDepth;

        public TaskCompletionSource SecondDelivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaximumDepth { get { return Volatile.Read(ref _maxDepth); } }

        public ServerReentrancyHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        public override void OnReceived(UnionDataList message)
        {
            var depth = Interlocked.Increment(ref _depth);
            SetMaximum(ref _maxDepth, depth);
            try
            {
                Assert.That(message.TryPopFirst(out int value), Is.True);
                if (value == 1)
                    Assert.That(Endpoint!.UnreliableSend(CreateMessage(_fixture.Server, 2)), Is.EqualTo(SendResult.Ok));
                else
                    SecondDelivery.TrySetResult();
            }
            finally
            {
                message.Release();
                Interlocked.Decrement(ref _depth);
            }
        }
    }

    private sealed class EchoOnceClientHandler : RawUnreliableTestHandler
    {
        private readonly IRawUnreliableClient _client;
        private int _echoed;

        public EchoOnceClientHandler(IRawUnreliableConformanceFixture<TServer> fixture, IRawUnreliableClient client)
            : base(fixture)
        {
            _client = client;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value) && Interlocked.Exchange(ref _echoed, 1) == 0)
                    Assert.That(Endpoint!.UnreliableSend(CreateMessage(_client, value)), Is.EqualTo(SendResult.Ok));
            }
            finally { message.Release(); }
        }
    }

    private sealed class BlockingClientHandler : RawUnreliableTestHandler
    {
        private int _activeCallbacks;
        private int _concurrentCallbacks;
        private int _completedCallbacks;

        public ManualResetEventSlim Release { get; } = new();

        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConcurrentCallbacks { get { return Volatile.Read(ref _concurrentCallbacks); } }

        public BlockingClientHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (Interlocked.Increment(ref _activeCallbacks) > 1)
                    Interlocked.Exchange(ref _concurrentCallbacks, 1);
                FirstEntered.TrySetResult();
                Release.Wait(DeliveryTimeout);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCallbacks);
                message.Release();
                if (Interlocked.Increment(ref _completedCallbacks) == 2)
                    AllCompleted.TrySetResult();
            }
        }
    }

    private sealed class StopFromReceiveServerHandler : RawUnreliableTestHandler
    {
        private readonly IRawUnreliableConformanceFixture<TServer> _fixture;

        public StopFromReceiveServerHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        public override void OnReceived(UnionDataList message)
        {
            try { Assert.That(_fixture.Server.Stop(), Is.True); }
            finally { message.Release(); }
        }
    }

    private sealed class ReturnedFlag
    {
        private int _value;

        public int Value
        {
            get { return Volatile.Read(ref _value); }
            set { Volatile.Write(ref _value, value); }
        }
    }

    private sealed class SendReturnedObservingHandler : RawUnreliableTestHandler
    {
        private readonly ReturnedFlag _flag;

        public ReturnedFlag Flag => _flag;

        public TaskCompletionSource<bool> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SendReturnedObservingHandler(IRawUnreliableConformanceFixture<TServer> fixture, ReturnedFlag flag)
            : base(fixture)
        {
            _flag = flag;
        }

        public override void OnReceived(UnionDataList message)
        {
            try { Observed.TrySetResult(_flag.Value == 1); }
            finally { message.Release(); }
        }
    }

    private sealed class ServerSerializationProbe
    {
        private int _active;
        private int _concurrent;
        private int _completed;

        public ManualResetEventSlim Release { get; } = new();

        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConcurrentCallbacks { get { return Volatile.Read(ref _concurrent); } }

        public void Enter()
        {
            if (Interlocked.Increment(ref _active) > 1)
                Interlocked.Exchange(ref _concurrent, 1);
            FirstEntered.TrySetResult();
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _active);
            if (Interlocked.Increment(ref _completed) == 2)
                AllCompleted.TrySetResult();
        }
    }

    private sealed class BlockingServerHandler : RawUnreliableTestHandler
    {
        private readonly ServerSerializationProbe _probe;

        public BlockingServerHandler(IRawUnreliableConformanceFixture<TServer> fixture, ServerSerializationProbe probe)
            : base(fixture)
        {
            _probe = probe;
        }

        public override void OnReceived(UnionDataList message)
        {
            _probe.Enter();
            try { _probe.Release.Wait(DeliveryTimeout); }
            finally
            {
                message.Release();
                _probe.Exit();
            }
        }
    }

    private sealed class BlockingPreReceiveServerHandler : RawUnreliableTestHandler
    {
        public volatile bool CallbackBegan;

        public ICheckPointCtl? Gate { get; private set; }

        public Task<CheckPointWaitResult>? Reached { get; private set; }

        public BlockingPreReceiveServerHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        protected override void OnStartedCore()
        {
            var control = GetEndpointControl<IRawUnreliableEndpointConformanceControl>(Endpoint!);
            Gate = control.AfterReceivedGate;
            Reached = Gate.Arm();
        }

        public override void OnReceived(UnionDataList message)
        {
            CallbackBegan = true;
            message.Release();
        }
    }

    private sealed class OnStartedOrderClientHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();

        public bool OnStartedCompleted { get; private set; }
        public bool SawStartedCompleted { get; private set; }

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public OnStartedOrderClientHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        protected override void OnStartedCore()
        {
            OnStartedCompleted = true;
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                SawStartedCompleted = OnStartedCompleted;
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }

    private sealed class ThrowingOnStartedHandler : RawUnreliableTestHandler
    {
        public ThrowingOnStartedHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        protected override void OnStartedCore()
        {
            throw new InvalidOperationException("Expected conformance test exception.");
        }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }
    }

    private sealed class SourceCheckingHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();

        public IEndPoint? Source { get; set; }
        public bool OnStartedCompleted { get; private set; }
        public bool SawOnStartedCompleted { get; private set; }
        public bool? OnStartedRemoteEqualsSource { get; private set; }

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public SourceCheckingHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        protected override void OnStartedCore()
        {
            OnStartedCompleted = true;
            OnStartedRemoteEqualsSource = Endpoint!.RemoteEndPoint?.Equals(Source);
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                SawOnStartedCompleted = OnStartedCompleted;
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }

    private sealed class EndpointStopOnReceiveHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onStoppedCount;

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }
        public int OnStoppedCount { get { return Volatile.Read(ref _onStoppedCount); } }

        public EndpointStopOnReceiveHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
                Endpoint!.Stop();
            }
            finally { message.Release(); }
        }

        public override void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _onStoppedCount);
            base.OnStopped(reason);
        }
    }

    private sealed class OnStoppedCountingHandler : RawUnreliableTestHandler
    {
        private int _onStoppedCount;

        public int OnStoppedCount { get { return Volatile.Read(ref _onStoppedCount); } }

        public OnStoppedCountingHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            message.Release();
        }

        public override void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _onStoppedCount);
            base.OnStopped(reason);
        }
    }

    private sealed class ServerRouteRecordingHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onStoppedCount;

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public int OnStoppedCount { get { return Volatile.Read(ref _onStoppedCount); } }

        public ServerRouteRecordingHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }

        public override void OnStopped(StopReason reason)
        {
            Interlocked.Increment(ref _onStoppedCount);
            base.OnStopped(reason);
        }
    }

    private sealed class OnStartedFlagHandler : RawUnreliableTestHandler
    {
        private readonly List<int> _received = new();
        private readonly object _lock = new();
        private int _onStartedRan;

        public bool OnStartedRan { get { return Volatile.Read(ref _onStartedRan) != 0; } }

        public int ReceivedCount { get { lock (_lock) return _received.Count; } }
        public IReadOnlyList<int> ReceivedValues { get { lock (_lock) return _received.ToArray(); } }

        public OnStartedFlagHandler(IRawUnreliableConformanceFixture<TServer> fixture) : base(fixture) { }

        protected override void OnStartedCore()
        {
            Volatile.Write(ref _onStartedRan, 1);
        }

        public override void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value)) { lock (_lock) _received.Add(value); }
            }
            finally { message.Release(); }
        }
    }
}
