using System.Collections.Concurrent;
using Archivarius;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.Ack.Raw.Reliable.Tests;

public struct PingRequest : IDataStruct
{
    public int Seq;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref Seq);
    }
}

public struct PongResponse : IDataStruct
{
    public int Seq;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref Seq);
    }
}

public class PingApi : ApiRoot
{
    public readonly RRDecl<PingRequest, PongResponse> Ping = new();
}

public class PingApiClient : PingApi
{
    public Task<PongResponse> SendPing(int seq)
    {
        return Ping.RequestAsync(new PingRequest { Seq = seq });
    }
}

public class PingApiServer : PingApi
{
    public PingApiServer()
    {
        Ping.SetProcessor(r =>
        {
            r.Response(new PongResponse { Seq = r.Data.Seq });
        });
    }
}

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class Ping
{
    private readonly ITransportStack _stack;

    public Ping(ITransportStack stack)
    {
        _stack = stack;
    }

    /// <summary>
    /// Runs the core ping scenario: <paramref name="clientCount"/> concurrent clients each
    /// send 100 sequential pings and verify each response carries the correct sequence number.
    /// </summary>
    private async Task RunPing(int clientCount, int concurrency)
    {
        const int pingCount = 100;
        Console.WriteLine($"Run '{clientCount}' clients, '{pingCount}' sequential pings each, using '{concurrency}' tasks");

        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory(true);

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var serverFactory = new ServerSideApiFactory<PingApiServer>(
            _ => new TestServerSideApiInstance<PingApiServer>(new PingApiServer(), memory, logger));

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
                    var api = new PingApiClient();
                    var handler = new ClientSideApi(api, memory, logger);
                    var connectedTcs = new TaskCompletionSource();
                    var disconnectedTcs = new TaskCompletionSource<StopReason>();
                    var stoppedTcs = new TaskCompletionSource<StopReason>();

                    handler.Connected += _ => connectedTcs.TrySetResult();
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

                    for (var i = 0; i < pingCount; i++)
                    {
                        var response = await api.SendPing(i);
                        if (response.Seq != i)
                        {
                            errors.Add($"{_stack.Id}: Ping {i} response has Seq={response.Seq}, expected {i}");
                            return;
                        }
                    }

                    api.GracefulShutdown(TimeSpan.FromMilliseconds(100));

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

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// A single client connects, sends 100 sequential pings, and verifies responses.
    /// </summary>
    [Test]
    [Category("Small")]
    public async Task Test_Single()
    {
        await RunPing(1, 1);
    }

    /// <summary>
    /// Small-scale concurrent ping test using
    /// <see cref="ITransportStack.GetSmallTestSize"/> parameters.
    /// </summary>
    [Test]
    [Category("Small")]
    public async Task Test_Small()
    {
        var (count, concurrency) = _stack.GetSmallTestSize();
        await RunPing(count, concurrency);
    }

    /// <summary>
    /// Large-scale concurrent ping test using
    /// <see cref="ITransportStack.GetBigTestSize"/> parameters.
    /// </summary>
    [Test]
    [Category("Big")]
    public async Task Test_Big()
    {
        var (count, concurrency) = _stack.GetBigTestSize();
        await RunPing(count, concurrency);
    }
}
