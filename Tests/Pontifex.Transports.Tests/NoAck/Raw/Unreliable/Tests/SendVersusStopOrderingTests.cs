namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class SendVersusStopOrderingTests : ConformanceTestBase
{
    public SendVersusStopOrderingTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public async Task ClientSendLosesWhenStopWinsStateRace()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.BeforeSendCommitGate;

        SendResult sendResult;

        using (var lease = new CheckpointLease(gate))
        {
            var sendTask = Task.Run(() =>
            {
                var msg = Scope.CreateSmallValidMessage(client);
                return client.TrySend(msg);
            });

            await lease.Reached;

            client.Stop();
            await recorder.WaitAsync();

            sendResult = await sendTask;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.EqualTo(SendResult.NotConnected));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ServerSendLosesWhenStopWinsStateRace()
    {
        var server = Scope.CreateServer();
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var gate = control.BeforeSendCommitGate;

        SendResult sendResult;

        using (var lease = new CheckpointLease(gate))
        {
            var foreignDest = Scope.CreateForeignServerDestination();
            var sendTask = Task.Run(() =>
            {
                var msg = Scope.CreateSmallValidMessage(server);
                return server.TrySend(foreignDest, msg);
            });

            await lease.Reached;

            server.Stop();
            await recorder.WaitAsync();

            sendResult = await sendTask;
        }

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.EqualTo(SendResult.NotConnected));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ClientSendDoesNotLoseSolelyBecauseLaterStopBegins()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStopStateTransitionGate;

        SendResult sendResult;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => client.Stop());
            await lease.Reached;

            var msg = Scope.CreateSmallValidMessage(client);
            sendResult = client.TrySend(msg);
        }

        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.Not.EqualTo(SendResult.NotConnected));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ClientSendWhileStoppingReturnsNotConnected()
    {
        var client = Scope.CreateClient();
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStoppedCallbackGate;

        SendResult sendResult;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => client.Stop());
            await lease.Reached;

            var msg = Scope.CreateSmallValidMessage(client);
            sendResult = client.TrySend(msg);
        }

        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.EqualTo(SendResult.NotConnected));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ServerSendWhileStoppingReturnsNotConnected()
    {
        var server = Scope.CreateServer();
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStoppedCallbackGate;

        SendResult sendResult;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => server.Stop());
            await lease.Reached;

            var foreignDest = Scope.CreateForeignServerDestination();
            var msg = Scope.CreateSmallValidMessage(server);
            sendResult = server.TrySend(foreignDest, msg);
        }

        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sendResult, Is.EqualTo(SendResult.NotConnected));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }
}
