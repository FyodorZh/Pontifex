using System.Collections.Concurrent;
using System.Threading;
using Archivarius;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.Ack.Raw.Reliable.Tests;

public struct KickMessage : IDataStruct
{
    public void Serialize(ISerializer serializer)
    {
    }
}

public class KickApi : ApiRoot
{
    public readonly S2CMessageDecl<KickMessage> Kick = new();
}

public class KickApiClient : KickApi
{
}

public class KickApiServer : KickApi
{
}

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class ConnectReceiveKick
{
    private readonly ITransportStack _stack;

    public ConnectReceiveKick(ITransportStack stack)
    {
        _stack = stack;
    }

    /// <summary>
    /// Runs the core connect-receive-kick scenario: <paramref name="clientCount"/> concurrent
    /// clients connect. The server sends a kick message then gracefully disconnects.
    /// Asserts every client receives the kick and observes a non-error stop reason.
    /// </summary>
    private async Task RunConnectReceiveKick(int clientCount, int concurrency)
    {
        Console.WriteLine($"Run '{clientCount}' connect-receive-kick clients using '{concurrency}' tasks");

        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory(true);

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var kickCount = 0;

        var serverFactory = new ServerSideApiFactory<KickApiServer>(ackData =>
        {
            var api = new KickApiServer();
            var instance = new TestServerSideApiInstance<KickApiServer>(api, memory, logger);
            instance.ApiStarted += _ =>
            {
                Interlocked.Increment(ref kickCount);
                var fireAndForget = Task.Run(() =>
                {
                    api.Kick.Send(new KickMessage());
                    api.GracefulShutdown(TimeSpan.FromMilliseconds(100));
                });
            };
            return instance;
        });

        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.Id}: ServerTransport.Init failed");
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (_, ct) =>
            {
                try
                {
                    var api = new KickApiClient();
                    var handler = new ClientSideApi(api, memory, logger);
                    var connectedTcs = new TaskCompletionSource();
                    var kickedTcs = new TaskCompletionSource();
                    var disconnectedTcs = new TaskCompletionSource<StopReason>();
                    var stoppedTcs = new TaskCompletionSource<StopReason>();

                    handler.Connected += _ => connectedTcs.TrySetResult();
                    api.Kick.SetProcessor(_ => kickedTcs.TrySetResult());
                    api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

                    var transport = (IAckRawReliableClient)factory.BuildClient();

                    if (!transport.Init(handler))
                    {
                        errors.Add($"{_stack.Id}: ClientTransport.Init failed");
                        return;
                    }

                    if (!transport.Start(reason => stoppedTcs.TrySetResult(reason)))
                    {
                        errors.Add($"{_stack.Id}: ClientTransport.Start failed");
                        return;
                    }

                    await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                    await kickedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

                    var disconnectReason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                    if (disconnectReason is AnyFail)
                    {
                        errors.Add($"{_stack.Id}: Client disconnected with error: {disconnectReason}");
                        return;
                    }

                    await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add($"{_stack.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            });

        Assert.That(errors, Is.Empty,
            $"{_stack.Id}: {errors.Count}/{clientCount} clients failed. " +
            $"First error: {errors.FirstOrDefault()}");
        Assert.That(kickCount, Is.EqualTo(clientCount),
            $"{_stack.Id}: Server should have kicked {clientCount} clients, got {kickCount}");

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// A single client connects, receives a kick, and gracefully disconnects.
    /// </summary>
    [Test]
    [Category("Small")]
    public async Task Test_Single()
    {
        await RunConnectReceiveKick(1, 1);
    }

    /// <summary>
    /// Small-scale concurrent connect-receive-kick test using
    /// <see cref="ITransportStack.GetSmallTestSize"/> parameters.
    /// </summary>
    [Test]
    [Category("Small")]
    public async Task Test_Small()
    {
        var (count, concurrency) = _stack.GetSmallTestSize();
        await RunConnectReceiveKick(count, concurrency);
    }

    /// <summary>
    /// Large-scale concurrent connect-receive-kick test using
    /// <see cref="ITransportStack.GetBigTestSize"/> parameters.
    /// </summary>
    [Test]
    [Category("Big")]
    public async Task Test_Big()
    {
        var (count, concurrency) = _stack.GetBigTestSize();
        await RunConnectReceiveKick(count, concurrency);
    }
}
