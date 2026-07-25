namespace Pontifex.NoAck.Raw.Unreliable.Tests;

[TestFixtureSource(typeof(ConformanceAdapterSource), nameof(ConformanceAdapterSource.GetAdapters))]
public sealed class StatusPropertyTests : ConformanceTestBase
{
    public StatusPropertyTests(
        INoAckRawUnreliableConformanceTestAdapter adapter) : base(adapter) { }

    [Test]
    public async Task ClientStatusReadsRemainSafeWhileStopInProgress()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStopStateTransitionGate;

        var readerExceptions = new List<Exception>();

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => client.Stop());
            await lease.Reached;

            var cts = new CancellationTokenSource();
            var readerTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        _ = client.IsValid;
                        _ = client.IsStarted;
                        _ = client.MessageMaxByteSize;
                    }
                    catch (Exception ex)
                    {
                        lock (readerExceptions) readerExceptions.Add(ex);
                    }

                    await Task.Yield();
                }
            });

            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            await readerTask;
        }

        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(readerExceptions, Is.Empty);
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ServerStatusReadsRemainSafeWhileStopInProgress()
    {
        var server = Scope.CreateServer(instrumented: true);
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var gate = control.BeforeStopStateTransitionGate;

        var readerExceptions = new List<Exception>();

        using (var lease = new CheckpointLease(gate))
        {
            var stopTask = Task.Run(() => server.Stop());
            await lease.Reached;

            var cts = new CancellationTokenSource();
            var readerTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        _ = server.IsValid;
                        _ = server.IsStarted;
                        _ = server.MessageMaxByteSize;
                    }
                    catch (Exception ex)
                    {
                        lock (readerExceptions) readerExceptions.Add(ex);
                    }

                    await Task.Yield();
                }
            });

            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            await readerTask;
        }

        await recorder.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(readerExceptions, Is.Empty);
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ClientStatusReadsRemainSafeWhileFatalFailureInitiated()
    {
        var client = Scope.CreateClient(instrumented: true);
        var control = GetControl(client);
        var recorder = new StopRecorder();

        Assert.That(client.Start(recorder.Callback), Is.True);

        var readerExceptions = new List<Exception>();
        var cts = new CancellationTokenSource();

        var readerTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    _ = client.IsValid;
                    _ = client.IsStarted;
                    _ = client.MessageMaxByteSize;
                }
                catch (Exception ex)
                {
                    lock (readerExceptions) readerExceptions.Add(ex);
                }

                await Task.Yield();
            }
        });

        control.InjectUnrecoverableFailure();
        await recorder.WaitAsync();

        cts.Cancel();
        await readerTask;

        Assert.Multiple(() =>
        {
            Assert.That(readerExceptions, Is.Empty);
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task ServerStatusReadsRemainSafeWhileFatalFailureInitiated()
    {
        var server = Scope.CreateServer(instrumented: true);
        var control = GetControl(server);
        var recorder = new StopRecorder();

        Assert.That(server.Start(recorder.Callback), Is.True);

        var readerExceptions = new List<Exception>();
        var cts = new CancellationTokenSource();

        var readerTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    _ = server.IsValid;
                    _ = server.IsStarted;
                    _ = server.MessageMaxByteSize;
                }
                catch (Exception ex)
                {
                    lock (readerExceptions) readerExceptions.Add(ex);
                }

                await Task.Yield();
            }
        });

        control.InjectUnrecoverableFailure();
        await recorder.WaitAsync();

        cts.Cancel();
        await readerTask;

        Assert.Multiple(() =>
        {
            Assert.That(readerExceptions, Is.Empty);
            Assert.That(server.IsValid, Is.False);
            Assert.That(server.IsStarted, Is.False);
        });
    }

    [Test]
    public async Task MessageMaxByteSizeIsImmutableForClientAndServerLifetime()
    {
        var client = Scope.CreateClient(instrumented: false);
        var clientInitialMax = client.MessageMaxByteSize;

        var server = Scope.CreateServer(instrumented: false);
        var serverInitialMax = server.MessageMaxByteSize;

        var clientRecorder = new StopRecorder();
        Assert.That(client.Start(clientRecorder.Callback), Is.True);
        Assert.That(client.MessageMaxByteSize, Is.EqualTo(clientInitialMax));

        var serverRecorder = new StopRecorder();
        Assert.That(server.Start(serverRecorder.Callback), Is.True);
        Assert.That(server.MessageMaxByteSize, Is.EqualTo(serverInitialMax));

        client.Stop();
        await clientRecorder.WaitAsync();
        Assert.That(client.MessageMaxByteSize, Is.EqualTo(clientInitialMax));

        server.Stop();
        await serverRecorder.WaitAsync();
        Assert.That(server.MessageMaxByteSize, Is.EqualTo(serverInitialMax));

        using var scope2 = Adapter.CreateScope();
        var failedClient = scope2.CreateClient(instrumented: true);
        var failedControl = GetControl(failedClient);
        var failedInitialMax = failedClient.MessageMaxByteSize;

        failedControl.FailNextStart();
        Assert.That(failedClient.Start(new StopRecorder().Callback), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(clientInitialMax, Is.GreaterThan(0));
            Assert.That(serverInitialMax, Is.GreaterThan(0));
            Assert.That(failedInitialMax, Is.GreaterThan(0));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(clientInitialMax));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(serverInitialMax));
            Assert.That(failedClient.MessageMaxByteSize, Is.EqualTo(failedInitialMax));
        });
    }
}
