namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class StopLifecycleTests : ConformanceTestBase
{
    public StopLifecycleTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public async Task ClientStopBeforeStartIsNoOpThatPreservesStartability()
    {
        var client = Scope.CreateClient(instrumented: true);

        Assert.That(client.Stop(), Is.True);

        var recorder = new StopRecorder();
        Assert.That(client.Start(recorder.Callback), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });

        client.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public async Task ServerStopBeforeStartIsNoOpThatPreservesStartability()
    {
        var server = Scope.CreateServer(instrumented: true);

        Assert.That(server.Stop(), Is.True);

        var recorder = new StopRecorder();
        Assert.That(server.Start(recorder.Callback), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
        });

        server.Stop();
        await recorder.WaitAsync();
    }

    [Test]
    public async Task ClientStopInvokesWinningCallbackExactlyOnceAndIsTerminal()
    {
        var client = Scope.CreateClient(instrumented: true);
        var firstRecorder = new StopRecorder();

        Assert.That(client.Start(firstRecorder.Callback), Is.True);
        Assert.That(client.Stop(), Is.True);

        await firstRecorder.WaitAsync();

        var secondRecorder = new StopRecorder();
        Assert.That(client.Stop(), Is.True);
        Assert.That(client.Start(secondRecorder.Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(firstRecorder.Count, Is.EqualTo(1));
            Assert.That(secondRecorder.Count, Is.Zero);
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ServerStopInvokesWinningCallbackExactlyOnceAndIsTerminal()
    {
        var server = Scope.CreateServer(instrumented: true);
        var firstRecorder = new StopRecorder();

        Assert.That(server.Start(firstRecorder.Callback), Is.True);
        Assert.That(server.Stop(), Is.True);

        await firstRecorder.WaitAsync();

        var secondRecorder = new StopRecorder();
        Assert.That(server.Stop(), Is.True);
        Assert.That(server.Start(secondRecorder.Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(firstRecorder.Count, Is.EqualTo(1));
            Assert.That(secondRecorder.Count, Is.Zero);
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task RepeatedStopCannotDuplicatePausedClientStoppedCallback()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeStoppedCallback);
        var stopResults = new List<bool>();
        List<Task> stopTasks;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => stopResults.Add(client.Stop()));
            await lease.Reached;

            stopTasks = new List<Task> { stopTask };
            for (var i = 0; i < 4; i++)
            {
                stopTasks.Add(Task.Run(() => stopResults.Add(client.Stop())));
            }
        }

        await Task.WhenAll(stopTasks);
        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stopResults, Has.All.True);
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task RepeatedStopCannotDuplicatePausedServerStoppedCallback()
    {
        var server = Scope.CreateServer(instrumented: true);
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var gate = control.GetGate(
            NoAckRawUnreliableCarrierIndependentCoreConformanceCheckpoint.BeforeStoppedCallback);
        var stopResults = new List<bool>();
        List<Task> stopTasks;

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => stopResults.Add(server.Stop()));
            await lease.Reached;

            stopTasks = new List<Task> { stopTask };
            for (var i = 0; i < 4; i++)
            {
                stopTasks.Add(Task.Run(() => stopResults.Add(server.Stop())));
            }
        }

        await Task.WhenAll(stopTasks);
        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stopResults, Has.All.True);
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }
}
