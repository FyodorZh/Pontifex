using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// White-box integration tests for the reliable acknowledgement TCP server transport admission hand-off.
/// The accept completion path is internal, so these tests drive it directly through a friend-assembly test
/// double instead of reflection.
/// </summary>
[TestFixture]
[Category("Integration")]
public sealed class RawReliableAckTcpServerTransportTests
{
    private static IEndPoint Remote => new StringEndPoint("remote");

    [Test]
    public void FaultedHandshakeResult_NotifiesProvidedHandlerOnce()
    {
        var log = new Logger([]);
        var transport = new TestRawReliableAckTcpServerTransport(log);
        try
        {
            var recording = new TransportRecordingAckServerHandler();
            var acknowledger = new FixedAcknowledger(recording);
            Assert.That(transport.Init(acknowledger), Is.True);
            Assert.That(transport.Start(_ => { }), Is.True);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
            IRawReliableAckServerHandler? handler = Acknowledge(transport);
            Assert.That(handler, Is.Not.Null);

            transport.ExposedOnHandshakeCompleted(
                socket,
                AckAcceptResult.Faulted(
                    "test", new InvalidOperationException("endpoint ctor failed"), handler));

            Assert.That(recording.StoppedCount, Is.EqualTo(1));

            Assert.That(transport.ExposedTryBeginAdmission(), Is.True);
            transport.ExposedOnHandshakeCompleted(
                socket,
                AckAcceptResult.Faulted(
                    "test", new InvalidOperationException("endpoint ctor failed"), handler));

            Assert.That(recording.StoppedCount, Is.EqualTo(1),
                "the shared stop-once gate must deduplicate the admission failure notification");
        }
        finally
        {
            try
            {
                transport.Stop(new UserIntention("integration-test", "cleanup"));
            }
            catch (Exception)
            {
                // best-effort cleanup
            }

            log.Dispose();
        }
    }

    private static IRawReliableAckServerHandler? Acknowledge(TestRawReliableAckTcpServerTransport transport)
    {
        UnionDataList ackData = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        try
        {
            ackData.PutLast(new UnionData(1));
            return transport.ExposedTryAcknowledge(Remote, ackData);
        }
        finally
        {
            ackData.Release();
        }
    }

    private sealed class TestRawReliableAckTcpServerTransport : RawReliableAckTcpServerTransport
    {
        public TestRawReliableAckTcpServerTransport(ILogger logger)
            : base(
                IPAddress.Loopback,
                0,
                2,
                TimeSpan.FromSeconds(30),
                null,
                logger,
                MemoryRental.Shared)
        {
        }

        public bool ExposedTryBeginAdmission()
        {
            return TryBeginAdmission();
        }

        public IRawReliableAckServerHandler? ExposedTryAcknowledge(IEndPoint remote, UnionDataList ackData)
        {
            return TryAcknowledge(remote, ackData);
        }

        public void ExposedOnHandshakeCompleted(Socket socket, AckAcceptResult result)
        {
            OnHandshakeCompleted(socket, result);
        }
    }

    private sealed class FixedAcknowledger : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        private readonly IRawReliableAckServerHandler _handler;

        public FixedAcknowledger(IRawReliableAckServerHandler handler)
        {
            _handler = handler;
        }

        public IRawReliableAckServerHandler? TryAck(IEndPoint remote, UnionDataList ackData)
        {
            ackData.Release();
            return _handler;
        }
    }

    private sealed class TransportRecordingAckServerHandler : IRawReliableAckServerHandler
    {
        private readonly object _lock = new();
        private readonly List<string> _events = new();

        public StopReason? DisconnectedReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        /// <summary>
        /// Gets the ordered lifecycle callbacks observed by the handler.
        /// </summary>
        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events.ToArray();
                }
            }
        }

        public void FillAckResponse(UnionDataList ackData)
        {
            ackData.PutLast(new UnionData(0xCD));
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            lock (_lock)
            {
                _events.Add("connected");
            }
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
            lock (_lock)
            {
                DisconnectedCount++;
                DisconnectedReason = reason;
                _events.Add("disconnected");
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedCount++;
                StoppedReason = reason;
                _events.Add("stopped");
            }
        }
    }
}
