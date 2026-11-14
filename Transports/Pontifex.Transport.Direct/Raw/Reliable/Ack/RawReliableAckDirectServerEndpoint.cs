using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// Reliable acknowledgement server session endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint is created by the server transport only after the
    /// client's acknowledgement request has been validated. It owns the channel from that point on; the
    /// channel is detached from the server registry before the endpoint starts.</description></item>
    /// <item><description><b>Other notes</b>: The pending acknowledgement request is buffered and processed
    /// once the endpoint becomes connected, mirroring the TCP carrier.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableAckDirectServerEndpoint : RawReliableAckServerEndpoint
    {
        /// <summary>
        /// The acknowledgement request frame type, exposed to the owning transport.
        /// </summary>
        internal const byte AckRequestMessageType = (byte)MessageType.AckRequestMessage;

        /// <summary>
        /// The explicit rejection frame the server transport sends when it refuses a connection. It is a
        /// carrier-private control frame; the shared reliable protocol has no rejection frame type.
        /// </summary>
        internal const byte RejectMessageType = 0x7F;

        // Direct has no idle-session policy, so the inherited watchdog is never allowed to fire.
        private static readonly TimeSpan NoIdleTimeout = TimeSpan.MaxValue;

        private readonly Action _onChannelDisposed;

        private Channel? _channel;
        private UnionDataList? _pendingAckData;

        internal RawReliableAckDirectServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Channel channel,
            IRawReliableAckServerHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            UnionDataList pendingAckData)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log,
                NoIdleTimeout)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _pendingAckData = pendingAckData ?? throw new ArgumentNullException(nameof(pendingAckData));
            _onChannelDisposed = OnChannelDisposed;
        }

        protected override void BeginConnect()
        {
            try
            {
                var channel = _channel;
                if (channel == null)
                {
                    Stop(new TextFail(TransportName, "Server endpoint has no channel"), false);
                    FinishConnect(ReleasePendingAckData);
                    return;
                }

                channel.ServerHandler = OnChannelMessage;
                channel.Disposed += _onChannelDisposed;

                // The client may have disconnected between the handshake and the endpoint start.
                if (channel.IsDisposed)
                {
                    Stop(new UnknownRemoteIntention(TransportName), false);
                    FinishConnect(ReleasePendingAckData);
                    return;
                }

                FinishConnect(ReleasePendingAckData);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex, "Failed to open server endpoint"), false);
                FinishConnect(ReleasePendingAckData);
            }
        }

        protected override void OnConnected()
        {
            base.OnConnected();

            var ackData = Interlocked.Exchange(ref _pendingAckData, null);
            if (ackData != null)
            {
                base.ProcessInboundMessage(MessageType.AckRequestMessage, ackData);
            }
        }

        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            var channel = _channel;
            if (channel == null)
            {
                bufferToSend.Release();
                return;
            }

            // The channel releases the buffer when it cannot deliver.
            if (channel.SendToClient(bufferToSend) != SendResult.Ok && !channel.IsDisposed)
            {
                Stop(new TextFail(TransportName, "Failed to send message through the direct channel"), false);
            }
        }

        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            base.BeginDisconnect(reason, tryBeGentle);
            CleanupChannel();
            FinishDisconnect();
        }

        protected override void FailHard(StopReason reason)
        {
            try
            {
                base.FailHard(reason);
            }
            finally
            {
                CleanupChannel();
            }
        }

        private void OnChannelMessage(IEndPoint clientEp, UnionDataList message)
        {
            if (message.GetDataSize() > MessageMaxByteSize)
            {
                message.Release();
                Log.e("Inbound message exceeds MessageMaxByteSize and was discarded");
                return;
            }

            ProcessInboundMessage(message);
        }

        private void OnChannelDisposed()
        {
            Stop(new UnknownRemoteIntention(TransportName), false);
        }

        private void CleanupChannel()
        {
            ReleasePendingAckData();

            var channel = Interlocked.Exchange(ref _channel, null);
            if (channel == null)
            {
                return;
            }

            channel.Disposed -= _onChannelDisposed;
            channel.ServerHandler = null;
            channel.Dispose();
        }

        private void ReleasePendingAckData()
        {
            var ackData = Interlocked.Exchange(ref _pendingAckData, null);
            ackData?.Release();
        }

        public override string ToString()
        {
            try
            {
                return $"direct-server-session[{RemoteEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-server-session[unknown]";
            }
        }
    }
}
