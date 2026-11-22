using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Reliable server session endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint is created by the server transport only after the
    /// client's handshake request has been validated and the handshake response has been built. It owns the
    /// channel and the response from that point on; the channel is detached from the server registry before
    /// the endpoint starts.</description></item>
    /// <item><description><b>Transport handshake</b>: the pending response is sent by the inherited
    /// <c>OnConnected</c> before the session handler observes the connection.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableDirectServerEndpoint : RawReliableHandshakeServerEndpoint
    {
        /// <summary>
        /// The transport handshake request frame type, exposed to the owning transport.
        /// </summary>
        internal const byte HandshakeRequestMessageType = (byte)MessageType.HandshakeRequestMessage;

        /// <summary>
        /// The transport handshake rejection frame type, exposed to the owning transport.
        /// </summary>
        internal const byte HandshakeRejectMessageType = (byte)MessageType.HandshakeRejectMessage;

        // Direct has no idle-session policy, so the inherited watchdog is never allowed to fire.
        private static readonly TimeSpan NoIdleTimeout = TimeSpan.MaxValue;

        private readonly Action _onChannelDisposed;

        private Channel? _channel;

        internal RawReliableDirectServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Channel channel,
            IRawReliableServerHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            UnionDataList pendingHandshakeResponse)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log,
                NoIdleTimeout)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            SetPendingHandshakeResponse(pendingHandshakeResponse
                ?? throw new ArgumentNullException(nameof(pendingHandshakeResponse)));
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
                    FinishConnect(ReleasePendingHandshakeResponse);
                    return;
                }

                channel.ServerHandler = OnChannelMessage;
                channel.Disposed += _onChannelDisposed;

                // The client may have disconnected between the handshake and the endpoint start.
                if (channel.IsDisposed)
                {
                    Stop(new UnknownRemoteIntention(TransportName), false);
                    FinishConnect(ReleasePendingHandshakeResponse);
                    return;
                }

                FinishConnect(ReleasePendingHandshakeResponse);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex, "Failed to open server endpoint"), false);
                FinishConnect(ReleasePendingHandshakeResponse);
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
            ReleasePendingHandshakeResponse();

            var channel = Interlocked.Exchange(ref _channel, null);
            if (channel == null)
            {
                return;
            }

            channel.Disposed -= _onChannelDisposed;
            channel.ServerHandler = null;
            channel.Dispose();
        }

        public override string ToString()
        {
            try
            {
                return $"{RawReliableDirectInfo.TransportName}-server-session[{RemoteEndPoint}]";
            }
            catch (Exception)
            {
                return $"{RawReliableDirectInfo.TransportName}-server-session[unknown]";
            }
        }
    }
}
