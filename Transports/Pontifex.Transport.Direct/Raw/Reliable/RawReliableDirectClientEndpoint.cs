using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Reliable client endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Transport handshake</b>: the endpoint sends the handshake request as the first
    /// channel message and reports the connection to the handler only after the server response is accepted.
    /// The in-process peer delivers frames synchronously, so the handshake needs no deadline timer.</description></item>
    /// <item><description><b>Rejection</b>: a <c>HandshakeRejectMessage</c> is processed synchronously, before
    /// the channel's disposal callback can claim the stop. That keeps the observed reason precise
    /// (<c>ProtocolMismatch</c>/<c>HandshakeRejected</c>) instead of <c>UnknownRemoteIntention</c>.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint owns its channel after a successful connect. The
    /// channel's <see cref="Channel.Disposed"/> notification is the only low-level disconnection
    /// signal.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableDirectClientEndpoint : RawReliableHandshakeClientEndpoint
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly IEndPoint _clientEndPoint;
        private readonly int _handshakeMaxByteSize;
        private readonly Action _onChannelDisposed;

        private Channel? _channel;

        internal RawReliableDirectClientEndpoint(
            string transportName,
            IEndPoint serverEndPoint,
            IEndPoint clientEndPoint,
            IRawReliableClientHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            string handshakeName,
            int handshakeMaxByteSize,
            TimeSpan handshakeTimeout,
            IRawReliableHandshakeTimerFactory? timerFactory)
            : base(transportName, serverEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log,
                null, handshakeName, handshakeMaxByteSize, handshakeTimeout, timerFactory)
        {
            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _clientEndPoint = clientEndPoint ?? throw new ArgumentNullException(nameof(clientEndPoint));
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _onChannelDisposed = OnChannelDisposed;
        }

        /// <summary>
        /// Gets whether the endpoint currently owns a handshake deadline timer.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The Direct client now enforces the configured deadline like
        /// every other carrier. Tests inject a manual factory to avoid a real timer.</description></item>
        /// </list>
        /// </remarks>
        internal bool HasHandshakeTimer => HasHandshakeTimerForTests;

        /// <summary>
        /// Gets how many times the inherited handshake engine asked for a deadline timer.
        /// </summary>
        internal int HandshakeTimerCreateCalls => HandshakeTimerCreateCallsForTests;

        protected override void BeginConnect()
        {
            try
            {
                Channel? channel = ChannelManager.Instance.Connect(RawReliableDirectInfo.TransportName, _serverEndPoint, _clientEndPoint);
                if (channel == null)
                {
                    Stop(new TextFail(TransportName, $"Direct server '{_serverEndPoint}' is not available"), false);
                    FinishConnect(() => { });
                    return;
                }

                _channel = channel;
                channel.ClientHandler = OnChannelMessage;
                channel.Disposed += _onChannelDisposed;

                // The server may have rejected the connection synchronously, before the endpoint had a
                // chance to subscribe to the channel.
                if (channel.IsDisposed)
                {
                    Stop(new UnknownRemoteIntention(TransportName), false);
                    FinishConnect(CleanupChannel);
                    return;
                }

                FinishConnect(CleanupChannel);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex, "Failed to connect"), false);
                FinishConnect(CleanupChannel);
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
            if (channel.SendToServer(bufferToSend) != SendResult.Ok && !channel.IsDisposed)
            {
                Stop(new TextFail(TransportName, "Failed to send message through the direct channel"), false);
            }
        }

        /// <summary>
        /// Handles a transport handshake rejection synchronously and delegates every other frame to the
        /// inherited handshake endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the channel's delivery thread. The synchronous
        /// stop wins the one-shot stop latch before the channel-disposal callback runs.</description></item>
        /// <item><description><b>Other notes</b>: The inherited implementation schedules the rejection on the
        /// serial action queue. In-process delivery closes the channel immediately after writing the reject, so
        /// the queued path could lose the stop race and report <c>UnknownRemoteIntention</c>.</description></item>
        /// </list>
        /// </remarks>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            if (messageType != MessageType.HandshakeRejectMessage)
            {
                return base.ProcessInboundMessage(messageType, message);
            }

            if (!IsHandshakePending)
            {
                // The inherited engine ignores handshake frames outside the handshake phase. A reject
                // that arrives after the session is established must not tear the session down.
                message.Release();
                return true;
            }

            if (message.GetDataSize() > _handshakeMaxByteSize)
            {
                message.Release();
                Stop(new TextFail(TransportName, "Handshake reject exceeds HandshakeMaxByteSize"), false);
                return true;
            }

            if (!RawReliableHandshake.TryParseReject(message, out short serverVersion,
                    out string serverName, out var status, out string? text))
            {
                message.Release();
                Stop(new ProtocolMismatch(TransportName, "Malformed transport handshake reject"), false);
                return true;
            }

            message.Release();

            string detail = string.IsNullOrEmpty(text)
                ? RawReliableHandshake.FormatRejectText(serverName, serverVersion, status)
                : text!;

            Stop(MapReject(status, detail), false);
            return true;
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

        private StopReason MapReject(RawReliableHandshake.HandshakeStatus status, string text)
        {
            switch (status)
            {
                case RawReliableHandshake.HandshakeStatus.VersionMismatch:
                case RawReliableHandshake.HandshakeStatus.NameMismatch:
                    return new ProtocolMismatch(TransportName, text);
                case RawReliableHandshake.HandshakeStatus.Rejected:
                case RawReliableHandshake.HandshakeStatus.CapacityExceeded:
                    return new HandshakeRejected(TransportName, text);
                case RawReliableHandshake.HandshakeStatus.Timeout:
                    return new TimeOut(TransportName);
                default:
                    return new ProtocolMismatch(TransportName, text);
            }
        }

        private void OnChannelMessage(UnionDataList message)
        {
            // The authoritative payload limit is the post-pop body check in RawReliableEndpoint: the framed
            // message includes the transport frame type, which must not count toward MessageMaxByteSize.
            ProcessInboundMessage(message);
        }

        private void OnChannelDisposed()
        {
            Stop(new UnknownRemoteIntention(TransportName), false);
        }

        private void CleanupChannel()
        {
            var channel = Interlocked.Exchange(ref _channel, null);
            if (channel == null)
            {
                return;
            }

            channel.Disposed -= _onChannelDisposed;
            channel.ClientHandler = null;

            // The manager removes the channel from the server registry. When the server is already gone
            // there is no entry left to remove, so the channel is disposed directly.
            ChannelManager.Instance.Disconnect(RawReliableDirectInfo.TransportName, _serverEndPoint, _clientEndPoint);
            channel.Dispose();
        }

        public override string ToString()
        {
            try
            {
                return $"{RawReliableDirectInfo.TransportName}-client[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return $"{RawReliableDirectInfo.TransportName}-client[unknown]";
            }
        }
    }
}
