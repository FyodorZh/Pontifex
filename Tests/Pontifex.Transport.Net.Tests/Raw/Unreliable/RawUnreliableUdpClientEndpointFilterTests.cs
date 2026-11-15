using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using NUnit.Framework;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Udp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Real-loopback test for the unreliable UDP client endpoint's inbound source filter.
/// </summary>
/// <remarks>
/// <para>
/// A loose integration test owned by the transport implementation, not by the carrier-independent
/// conformance suite. It drives the internal endpoint directly because the filter is carrier-specific and
/// cannot be provoked through the public transport API: a public client only ever receives from its
/// configured remote endpoint.
/// </para>
/// <para>
/// Cross-socket loopback FIFO is assumed for the ordering barrier: both datagrams are sent sequentially from
/// the test thread to the same client socket, with the legitimate one second. The receiver is a single serial
/// loop, so if the foreign source were not filtered its message would already have reached the handler when
/// the legitimate one does. Making the drop detection unconditional would need either a time-based negative
/// wait or a production drop counter; this test deliberately uses neither.
/// </para>
/// </remarks>
[Category("Integration")]
public sealed class RawUnreliableUdpClientEndpointFilterTests
{
    private const int MessageMaxByteSize = 490;
    private const long BufferCapacityBytes = (long)MessageMaxByteSize * 100;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static IMemoryRental Memory => MemoryRental.Shared;

    private static ILogger Log => new Logger([]);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    [Test]
    public async Task Inbound_DatagramFromUnconfiguredSource_IsDroppedBeforeDelivery()
    {
        using Socket legitimateSocket = CreateBoundSocket(out IPEndPoint legitimateEndPoint);
        using Socket foreignSocket = CreateBoundSocket(out _);

        var handler = new RecordingHandler();
        var endpoint = new RawUnreliableUdpClientEndpoint(
            "udp-filter-test",
            legitimateEndPoint,
            handler,
            MessageMaxByteSize,
            BufferCapacityBytes,
            inboxCapacityMessages: 100,
            inboxCapacityBytes: BufferCapacityBytes,
            Memory,
            Log);

        try
        {
            Assert.That(endpoint.Start(), Is.True, "the client endpoint start request must be accepted");
            await handler.Started.Task.WaitAsync(Timeout);

            IPEndPoint? local = endpoint.BoundLocalEndPoint;
            Assert.That(local, Is.Not.Null, "the endpoint must bind its socket during start");
            var destination = new IPEndPoint(IPAddress.Loopback, local!.Port);

            // The foreign datagram is sent first and the receiver is a single serial loop, so the later
            // legitimate datagram is an ordering barrier (assuming cross-socket loopback FIFO): if the
            // foreign source were not filtered, its message would already have reached the handler by the
            // time the legitimate one does.
            Send(foreignSocket, destination, 111);
            Send(legitimateSocket, destination, 222);

            await handler.LegitimateReceived.Task.WaitAsync(Timeout);

            Assert.That(handler.Values, Is.EqualTo(new[] { 222 }),
                "only the configured remote source may be delivered");
        }
        finally
        {
            ((IRawUnreliableEndpoint)endpoint).Stop();
        }
    }

    private static Socket CreateBoundSocket(out IPEndPoint endPoint)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        endPoint = (IPEndPoint)socket.LocalEndPoint!;
        return socket;
    }

    private static void Send(Socket socket, IPEndPoint destination, int value)
    {
        UnionDataList message = Memory.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);

        var sender = new UdpSyncSender(
            socket,
            destination,
            MessageMaxByteSize,
            Memory.ByteArraysPool,
            _ => { },
            new TrafficCollectorSlim("udp-filter-test", UtcNowDateTimeProvider.Instance));

        Assert.That(sender.Send(message), Is.EqualTo(SendResult.Ok));
    }

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _values = new();

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LegitimateReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<int> Values
        {
            get { lock (_lock) { return _values.ToArray(); } }
        }

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Started.TrySetResult();
        }

        public void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value))
                {
                    lock (_lock) { _values.Add(value); }

                    if (value == 222)
                    {
                        LegitimateReceived.TrySetResult();
                    }
                }
            }
            finally
            {
                message.Release();
            }
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
