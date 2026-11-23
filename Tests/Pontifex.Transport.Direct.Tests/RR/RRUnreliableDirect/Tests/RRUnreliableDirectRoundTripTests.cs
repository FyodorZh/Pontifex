using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Pins request/response delivery, correlation, cancellation, and expiry of the in-process RR
/// unreliable carrier.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
public sealed class RRUnreliableDirectRoundTripTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
    public void RequestAndResponse_RoundTripInProcess()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        var server = StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var clientHandler = new RecordingClientHandler();

        var request = TestMessages.Int(42);
        Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.RequestId, Is.Not.Null);

        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True,
            "the server must receive the request");
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True,
            "the client must receive the response");

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.LastRequestValue, Is.EqualTo(42));
            Assert.That(clientHandler.LastResponseValue, Is.EqualTo(43));
            Assert.That(server.CurrentCarrier!.RegisteredClientCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ResponseHandle_IsSingleUse()
    {
        var clock = NewClock();
        using var serverHandler = new SingleUseHandler();
        var server = StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.FirstResult, Is.EqualTo(SendResult.Ok));
            Assert.That(serverHandler.SecondResult, Is.EqualTo(SendResult.Error));
            Assert.That(clientHandler.ResponseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void RejectedRequest_ProducesNoCallbackAndExpiresSilently()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.Reject);
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);

        clock.Now += TimeSpan.FromSeconds(4);
        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(firstHandler.ResponseCount, Is.Zero);
            Assert.That(secondHandler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void CancelledRequest_ProducesNoCallback()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.AcceptSilently);
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var cancelledHandler = new RecordingClientHandler();
        using var nextHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), cancelledHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        cancelledHandler.Cancel();

        Assert.That(client.Request(TestMessages.Int(2), nextHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(cancelledHandler.ResponseCount, Is.Zero);
    }

    [Test]
    public void ExpiredRequest_ProducesNoCallback()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.AcceptSilently);
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var expiredHandler = new RecordingClientHandler();
        using var laterHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), expiredHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);

        clock.Now += TimeSpan.FromSeconds(4);
        Assert.That(client.Request(TestMessages.Int(2), laterHandler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(expiredHandler.ResponseCount, Is.Zero);
            Assert.That(laterHandler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void InvalidPayload_ReturnsInvalidMessage()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.AcceptSilently);
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(client.Request(null!, clientHandler), Is.EqualTo(SendResult.InvalidMessage));
        Assert.That(client.Request(TestMessages.Int(1), clientHandler), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void OversizedPayload_ReturnsMessageTooBigAndReleasesTheFrame()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.AcceptSilently);
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var clientHandler = new RecordingClientHandler();
        var request = TestMessages.PayloadOfSize(RRDirectInfo.MessageMaxByteSize + 1);

        Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(request.IsAlive, Is.False);
    }

    [Test]
    public void ExactlyMaximumPayload_RoundTripsInBothDirections()
    {
        var clock = NewClock();
        using var serverHandler = new MaximumPayloadHandler();
        StartServer(clock, serverHandler, out string name);
        var client = StartClient(clock, name);
        using var clientHandler = new RecordingClientHandler();
        var request = TestMessages.PayloadOfSize(RRDirectInfo.MessageMaxByteSize);

        Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.LastRequestSize,
                Is.EqualTo(RRDirectInfo.MessageMaxByteSize));
            Assert.That(clientHandler.LastResponseSize,
                Is.EqualTo(RRDirectInfo.MessageMaxByteSize));
            Assert.That(serverHandler.ResponseResult, Is.EqualTo(SendResult.Ok));
        });
    }

    private static ManualClock NewClock()
    {
        return new ManualClock { Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    private RRUnreliableDirectServer StartServer(
        ManualClock clock,
        IRRUnreliableServerHandler handler,
        out string name,
        int queueCapacity = RRDirectInfo.DispatchQueueCapacity)
    {
        name = "rr-unreliable-" + Guid.NewGuid().ToString("N");
        var server = new RRUnreliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clock,
            queueCapacity);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RRUnreliableDirectClient StartClient(ManualClock clock, string serverName)
    {
        var client = new RRUnreliableDirectClient(
            new StringEndPoint(serverName),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timeProvider: clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        return client;
    }

    private sealed class SingleUseHandler : IRRUnreliableServerHandler, IDisposable
    {
        public ManualResetEventSlim Requested { get; } = new(false);

        public SendResult FirstResult { get; private set; }

        public SendResult SecondResult { get; private set; }

        public bool OnRequested(IEndPoint client, UnionDataList request,
            IRRUnreliableResponse response)
        {
            try
            {
                var first = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                first.PutLast(1);
                FirstResult = response.Response(first);

                var second = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                second.PutLast(2);
                SecondResult = response.Response(second);
                return true;
            }
            finally
            {
                request.Release();
                Requested.Set();
            }
        }

        public void Dispose()
        {
            Requested.Dispose();
        }
    }

    private sealed class MaximumPayloadHandler : IRRUnreliableServerHandler, IDisposable
    {
        public ManualResetEventSlim Requested { get; } = new(false);

        public int LastRequestSize { get; private set; }

        public SendResult ResponseResult { get; private set; }

        public bool OnRequested(IEndPoint client, UnionDataList request,
            IRRUnreliableResponse response)
        {
            try
            {
                LastRequestSize = request.GetDataSize();
                ResponseResult = response.Response(
                    TestMessages.PayloadOfSize(RRDirectInfo.MessageMaxByteSize));
                return true;
            }
            finally
            {
                request.Release();
                Requested.Set();
            }
        }

        public void Dispose()
        {
            Requested.Dispose();
        }
    }
}
