using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.RR.Reliable;
using Pontifex.RR.Reliable.Direct;
using Pontifex.Tests.RR.RRReliableDirect.Harness;
using Pontifex.Tests.TestSupport;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableDirect;

/// <summary>
/// Pins request/response delivery, ordering, completion, cancellation, TTLs, and correlation of the
/// in-process RR reliable carrier.
/// </summary>
[TestFixture]
[Category("Direct.RRReliable")]
[Category("Direct.RRReliable.Integration")]
public sealed class RRReliableDirectRoundTripTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly List<ITransport> _transports = new();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _transports.Count - 1; i >= 0; i--)
        {
            _transports[i].Stop();
        }

        _transports.Clear();
    }

    [Test]
    public void MultipleOrderedResponsesAndNormalCompletion_ReachTheClientInOrder()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.TwoResponsesThenFinish);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(5), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True,
            "the client must receive the first response");
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True,
            "the client must receive the completion");

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.RequestValues, Is.EqualTo(new[] { 5 }));
            Assert.That(clientHandler.ResponseValues, Is.EqualTo(new[] { 51, 52 }));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
        });
    }

    [Test]
    public void Reject_ProducesOneRejectedCompletionAndNoResponse()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.Reject);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(6), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.ResponseCount, Is.Zero);
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Rejected }));
        });
    }

    [Test]
    public void Cancel_ProducesOneCanceledCompletion()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(7), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);

        clientHandler.Cancel();

        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.ResponseCount, Is.Zero);
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Canceled }));
        });
    }

    [Test]
    public void RequestTtl_ProducesOneFailedCompletion()
    {
        var serverTimers = new ManualTimerFactory();
        var clientTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(clientTimers, name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(8), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientTimers.ScheduledCount, Is.EqualTo(1));

        Assert.That(clientTimers.Fire(0), Is.True);
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.FinishCount, Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        });
    }

    [Test]
    public void ResponseTtl_ProducesOneFailedCompletionAndDisposesTheRequestTimer()
    {
        var serverTimers = new ManualTimerFactory();
        var clientTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(clientTimers, name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(9), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(serverTimers.ScheduledCount, Is.EqualTo(1));

        Assert.That(serverTimers.Fire(0), Is.True);
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);
        WaitUntil(() => clientTimers.PendingCount == 0, Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.FinishCount, Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
            Assert.That(clientTimers.PendingCount, Is.Zero,
                "the terminal completion must dispose the request timer");
        });
    }

    [Test]
    public void NoServer_RequestReturnsErrorWithoutCallbacksAndALaterServerServes()
    {
        string name = NewName();
        var clientTimers = new ManualTimerFactory();
        var client = StartClient(clientTimers, name);
        using var firstHandler = new RecordingReliableClientHandler();
        using var secondHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(10), firstHandler), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(firstHandler.ResponseCount, Is.Zero);
            Assert.That(firstHandler.FinishCount, Is.Zero);
            Assert.That(clientTimers.ScheduledCount, Is.Zero);
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.True);
        });

        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        StartServer(new ManualTimerFactory(), serverHandler, out string unused, name);

        Assert.That(client.Request(TestMessages.Int(11), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(secondHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(serverHandler.RequestValues, Is.EqualTo(new[] { 11 }));
    }

    [Test]
    public void SynchronousCancelInsideAResponseCallback_ProducesOneCanceledCompletion()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.Respond);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var clientHandler = new RecordingReliableClientHandler();
        clientHandler.OnResponse = _ => clientHandler.Cancel();

        Assert.That(client.Request(TestMessages.Int(12), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.ResponseCount, Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Canceled }));
        });
    }

    [Test]
    public void HandlerReuseAfterFinish_IsAccepted()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(20), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        clientHandler.ResetSignals();

        Assert.That(client.Request(TestMessages.Int(21), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.ResponseValues, Is.EqualTo(new[] { 21, 22 }));
            Assert.That(clientHandler.Finishes,
                Is.EqualTo(new[] { RRReliableCompletionType.Normal, RRReliableCompletionType.Normal }));
        });
    }

    [Test]
    public void PayloadBoundaries_EnforceTheInclusiveMaximum()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var maximumHandler = new RecordingReliableClientHandler();
        using var oversizedHandler = new RecordingReliableClientHandler();

        var maximum = TestMessages.PayloadOfSize(RRDirectInfo.MessageMaxByteSize);
        Assert.That(client.Request(maximum, maximumHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(maximumHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(serverHandler.LastRequestSize, Is.EqualTo(RRDirectInfo.MessageMaxByteSize));

        var oversized = TestMessages.PayloadOfSize(RRDirectInfo.MessageMaxByteSize + 1);
        Assert.That(client.Request(oversized, oversizedHandler), Is.EqualTo(SendResult.MessageTooBig));

        Assert.Multiple(() =>
        {
            Assert.That(oversized.IsAlive, Is.False);
            Assert.That(oversizedHandler.ResponseCount, Is.Zero);
            Assert.That(oversizedHandler.FinishCount, Is.Zero);
        });
    }

    [Test]
    public void TwoConcurrentRequests_SecondAnsweredFirst_KeepSeparateCompletionsAndIds()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(serverTimers, serverHandler, out string name);
        var client = StartClient(new ManualTimerFactory(), name);
        using var firstHandler = new RecordingReliableClientHandler();
        using var secondHandler = new RecordingReliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(30), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(TestMessages.Int(31), secondHandler), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.RequestCount == 2, Timeout);

        IRRReliableResponse second = serverHandler.HandleForValue(31)!;
        Assert.That(second.Response(TestMessages.Int(310)), Is.EqualTo(SendResult.Ok));
        Assert.That(second.Finish(), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Finished.Wait(Timeout), Is.True);

        IRRReliableResponse first = serverHandler.HandleForValue(30)!;
        Assert.That(first.Response(TestMessages.Int(300)), Is.EqualTo(SendResult.Ok));
        Assert.That(first.Finish(), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Finished.Wait(Timeout), Is.True);

        long[] requestIds = serverHandler.RequestIds;

        Assert.Multiple(() =>
        {
            Assert.That(firstHandler.ResponseValues, Is.EqualTo(new[] { 300 }));
            Assert.That(secondHandler.ResponseValues, Is.EqualTo(new[] { 310 }));
            Assert.That(firstHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
            Assert.That(secondHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
            Assert.That(requestIds, Has.Length.EqualTo(2));
            Assert.That(requestIds[0], Is.Not.EqualTo(requestIds[1]));
        });
    }

    [Test]
    public void TwoClients_AreIndependent()
    {
        var serverTimers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        StartServer(serverTimers, serverHandler, out string name);
        var firstClient = StartClient(new ManualTimerFactory(), name);
        var secondClient = StartClient(new ManualTimerFactory(), name);
        using var firstHandler = new RecordingReliableClientHandler();
        using var secondHandler = new RecordingReliableClientHandler();

        Assert.That(firstClient.Request(TestMessages.Int(40), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondClient.Request(TestMessages.Int(41), secondHandler), Is.EqualTo(SendResult.Ok));

        Assert.That(firstHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(secondHandler.Finished.Wait(Timeout), Is.True);

        IEndPoint[] sources = serverHandler.RequestSources;

        Assert.Multiple(() =>
        {
            Assert.That(firstHandler.ResponseValues, Is.EqualTo(new[] { 41 }));
            Assert.That(secondHandler.ResponseValues, Is.EqualTo(new[] { 42 }));
            Assert.That(serverHandler.RequestCount, Is.EqualTo(2));
            Assert.That(sources, Has.Length.EqualTo(2));
            Assert.That(sources[0], Is.Not.EqualTo(sources[1]));
        });
    }

    private static string NewName()
    {
        return "rr-reliable-" + Guid.NewGuid().ToString("N");
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("the condition was not met before the timeout");
            }

            Thread.Yield();
        }
    }

    private RRReliableDirectServer StartServer(
        ManualTimerFactory timers,
        IRRReliableServerHandler handler,
        out string name)
    {
        name = NewName();
        return StartServer(timers, handler, out name, name);
    }

    private RRReliableDirectServer StartServer(
        ManualTimerFactory timers,
        IRRReliableServerHandler handler,
        out string boundName,
        string name)
    {
        boundName = name;
        var server = new RRReliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timers);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RRReliableDirectClient StartClient(ManualTimerFactory timers, string serverName)
    {
        var client = new RRReliableDirectClient(
            new StringEndPoint(serverName),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timers);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        return client;
    }
}
