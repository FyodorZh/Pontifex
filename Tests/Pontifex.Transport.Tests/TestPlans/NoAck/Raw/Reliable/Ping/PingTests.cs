using System.Collections.Concurrent;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.NoAck.Raw;
using Pontifex.NoAck.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.NoAckRawReliable.Tests.Ping;

[TestFixtureSource(typeof(NoAckRawReliableStacks))]
public class PingTests
{
    private readonly ITransportStack _stack;

    public PingTests(ITransportStack stack)
    {
        _stack = stack;
    }

    /// <summary>
    /// Runs the core ping scenario: <paramref name="clientCount"/> concurrent clients each
    /// send 100 sequential pings over raw NoAckRawReliable transport and verify each
    /// echo response carries the correct sequence number.
    /// </summary>
    private async Task RunPing(int clientCount, int concurrency)
    {
        const int pingCount = 100;
        Console.WriteLine($"Run '{clientCount}' clients, '{pingCount}' sequential pings each, using '{concurrency}' tasks");

        var memory = TransportRegistry.Memory;
        var factory = _stack.GetTransportFactory(true);

        var server = (INoAckRawReliableServer)factory.BuildServer();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        server.OnReceived += (endpoint, data) =>
        {
            using var disposer = data.AsDisposable();
            if (data.TryPopFirst(out int seq))
            {
                var echo = memory.CollectablePool.Acquire<UnionDataList>();
                echo.PutFirst(seq);
                server.Send(endpoint, echo);
            }
        };

        Assert.That(server.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (_, ct) =>
            {
                try
                {
                    var responseTcs = new TaskCompletionSource[pingCount];
                    var responses = new int[pingCount];

                    for (var i = 0; i < pingCount; i++)
                    {
                        responseTcs[i] = new TaskCompletionSource();
                        responses[i] = -1;
                    }

                    var client = (INoAckRawReliableClient)factory.BuildClient();
                    var stoppedTcs = new TaskCompletionSource<StopReason>();

                    client.OnReceived += data =>
                    {
                        using var disposer = data.AsDisposable();
                        if (data.TryPopFirst(out int seq) && seq >= 0 && seq < pingCount)
                        {
                            responses[seq] = seq;
                            responseTcs[seq].TrySetResult();
                        }
                    };

                    if (!client.Start(reason => stoppedTcs.TrySetResult(reason)))
                    {
                        errors.Add($"{_stack.Id}: ClientTransport.Start failed");
                        return;
                    }

                    for (var i = 0; i < pingCount; i++)
                    {
                        var msg = memory.CollectablePool.Acquire<UnionDataList>();
                        msg.PutFirst(i);
                        client.Send(msg);

                        await responseTcs[i].Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

                        if (responses[i] != i)
                        {
                            errors.Add($"{_stack.Id}: Ping {i} response has Seq={responses[i]}, expected {i}");
                            return;
                        }
                    }

                    client.Stop(new UserIntention("test", "done"));
                    var stopReason = await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                    if (stopReason is AnyFail)
                    {
                        errors.Add($"{_stack.Id}: Client transport stopped with error: {stopReason}");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add($"{_stack.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            });

        Assert.That(errors, Is.Empty,
            $"{_stack.Id}: {errors.Count}/{clientCount} clients failed. " +
            $"First error: {errors.FirstOrDefault()}");

        server.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// A single client connects, sends 100 sequential pings, and verifies each echo response.
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
