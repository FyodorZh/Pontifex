namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class ForcedFailureTests : ConformanceTestBase
{
    public ForcedFailureTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public async Task ForcedClientFailureStopsInvalidatesAndNotifiesOnce()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        control.InjectUnrecoverableFailure();
        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(client.IsStarted, Is.False);
            Assert.That(client.IsValid, Is.False);
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(client.Stop(), Is.False);
            Assert.That(client.Start(new StopRecorder().Callback), Is.False);
        });
    }

    [Test]
    public async Task ForcedServerFailureStopsInvalidatesAndNotifiesOnce()
    {
        var server = Scope.CreateServer(instrumented: true);
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        control.InjectUnrecoverableFailure();
        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(server.IsStarted, Is.False);
            Assert.That(server.IsValid, Is.False);
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(server.Stop(), Is.False);
            Assert.That(server.Start(new StopRecorder().Callback), Is.False);
        });
    }

    [Test]
    public async Task RepeatedStopCannotDuplicatePausedFatalFailureCallback()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStoppedCallbackGate;
        var stopResults = new List<bool>();
        List<Task> stopTasks;

        using (var lease = new CheckpointLease(gate))
        {
            var failureTask = Task.Run(() => control.InjectUnrecoverableFailure());
            await lease.Reached;

            stopTasks = new List<Task> { failureTask };
            for (var i = 0; i < 4; i++)
            {
                stopTasks.Add(Task.Run(() => stopResults.Add(client.Stop())));
            }
        }

        await Task.WhenAll(stopTasks);
        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(stopResults, Has.All.False);
        });
    }
}
