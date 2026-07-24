namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class InitialStateAndStartTests : ConformanceTestBase
{
    public InitialStateAndStartTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public void ClientInitialStateIsValidAndUnstarted()
    {
        var client = Scope.CreateClient(instrumented: false);

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public void ServerInitialStateIsValidAndUnstarted()
    {
        var server = Scope.CreateServer(instrumented: false);

        Assert.Multiple(() =>
        {
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ConcurrentClientStartsHaveExactlyOneWinner()
    {
        var client = Scope.CreateClient(instrumented: true);
        _ = GetControl(client);
        const int taskCount = 8;
        var results = new bool[taskCount];
        var recorders = new StopRecorder[taskCount];
        var tasks = new Task[taskCount];
        var barrier = new Barrier(taskCount + 1);

        for (var i = 0; i < taskCount; i++)
        {
            var idx = i;
            recorders[idx] = new StopRecorder();
            tasks[idx] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                results[idx] = client.Start(recorders[idx].Callback);
            });
        }

        barrier.SignalAndWait();
        await Task.WhenAll(tasks);

        var trueCount = results.Count(r => r);

        Assert.Multiple(() =>
        {
            Assert.That(trueCount, Is.EqualTo(1));
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });

        var winnerIndex = Array.IndexOf(results, true);
        client.Stop();
        await recorders[winnerIndex].WaitAsync();
    }

    [Test]
    public async Task ConcurrentServerStartsHaveExactlyOneWinner()
    {
        var server = Scope.CreateServer(instrumented: true);
        _ = GetControl(server);
        const int taskCount = 8;
        var results = new bool[taskCount];
        var recorders = new StopRecorder[taskCount];
        var tasks = new Task[taskCount];
        var barrier = new Barrier(taskCount + 1);

        for (var i = 0; i < taskCount; i++)
        {
            var idx = i;
            recorders[idx] = new StopRecorder();
            tasks[idx] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                results[idx] = server.Start(recorders[idx].Callback);
            });
        }

        barrier.SignalAndWait();
        await Task.WhenAll(tasks);

        var trueCount = results.Count(r => r);

        Assert.Multiple(() =>
        {
            Assert.That(trueCount, Is.EqualTo(1));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
        });

        var winnerIndex = Array.IndexOf(results, true);
        server.Stop();
        await recorders[winnerIndex].WaitAsync();
    }

    [Test]
    public async Task LaterClientStartAfterSuccessfulStartIsRejected()
    {
        var client = Scope.CreateClient(instrumented: true);
        var firstRecorder = new StopRecorder();
        var secondRecorder = new StopRecorder();

        Assert.That(client.Start(firstRecorder.Callback), Is.True);
        Assert.That(client.Start(secondRecorder.Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
            Assert.That(secondRecorder.Count, Is.Zero);
        });

        client.Stop();
        await firstRecorder.WaitAsync();
    }

    [Test]
    public async Task LaterServerStartAfterSuccessfulStartIsRejected()
    {
        var server = Scope.CreateServer(instrumented: true);
        var firstRecorder = new StopRecorder();
        var secondRecorder = new StopRecorder();

        Assert.That(server.Start(firstRecorder.Callback), Is.True);
        Assert.That(server.Start(secondRecorder.Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
            Assert.That(secondRecorder.Count, Is.Zero);
        });

        server.Stop();
        await firstRecorder.WaitAsync();
    }
}
