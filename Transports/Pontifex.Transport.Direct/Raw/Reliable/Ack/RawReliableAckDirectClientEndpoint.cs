using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// Reliable acknowledgement client endpoint carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint owns its channel after a successful connect. The
    /// channel's <see cref="Channel.Disposed"/> notification is the only disconnection signal: in-process
    /// delivery has no end-of-stream, so either side disposing the channel is reported to both
    /// peers.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableAckDirectClientEndpoint : RawReliableAckClientEndpoint, IRawReliableEndpoint
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly IEndPoint _clientEndPoint;
        private readonly object _startGate = new();
        private readonly Action _onChannelDisposed;

        private Channel? _channel;
        private bool _deferStopUntilStarted = true;
        private StopReason? _pendingStopReason;

        internal RawReliableAckDirectClientEndpoint(
            string transportName,
            IEndPoint serverEndPoint,
            IEndPoint clientEndPoint,
            IRawReliableAckClientHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, serverEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log,
                null)
        {
            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _clientEndPoint = clientEndPoint ?? throw new ArgumentNullException(nameof(clientEndPoint));
            _onChannelDisposed = OnChannelDisposed;
        }

        protected override void BeginConnect()
        {
            try
            {
                Channel? channel = ChannelManager.Instance.Connect(_serverEndPoint, _clientEndPoint);
                if (channel == null)
                {
                    RequestStop(new TextFail(TransportName, $"Direct server '{_serverEndPoint}' is not available"));
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
                    RequestStop(new UnknownRemoteIntention(TransportName));
                    FinishConnect(CleanupChannel);
                    return;
                }

                FinishConnect(CleanupChannel);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                RequestStop(new ExceptionFail(TransportName, ex, "Failed to connect"));
                FinishConnect(CleanupChannel);
            }
        }

        protected override void OnConnected()
        {
            // When the connection already failed, do not run the acknowledgement handshake: the handler
            // supplied no ack data and the start will be stopped from the ready-to-connect step.
            lock (_startGate)
            {
                if (_pendingStopReason != null)
                {
                    return;
                }
            }

            base.OnConnected();
        }

        /// <summary>
        /// Takes the failure that was observed while the endpoint was still starting, without ending the
        /// start-time stop deferral.
        /// </summary>
        internal StopReason? TakePendingStopReason()
        {
            lock (_startGate)
            {
                var reason = _pendingStopReason;
                _pendingStopReason = null;
                return reason;
            }
        }

        /// <summary>
        /// Ends the start-time stop deferral. Failures observed from now on stop the endpoint
        /// immediately.
        /// </summary>
        internal void FinishTransportStart()
        {
            lock (_startGate)
            {
                _deferStopUntilStarted = false;
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

        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            if ((byte)messageType == RawReliableAckDirectServerEndpoint.RejectMessageType)
            {
                message.Release();
                RequestStop(new AckRejected(TransportName));
                return true;
            }

            return base.ProcessInboundMessage(messageType, message);
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

        private void OnChannelMessage(UnionDataList message)
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
            RequestStop(new UnknownRemoteIntention(TransportName));
        }

        /// <summary>
        /// Requests a logical disconnect, deferring it while the transport start transition is still running.
        /// </summary>
        /// <remarks>
        /// In-process delivery is fully synchronous, so application protocol code can disconnect the endpoint
        /// while the synchronous start is still inside the endpoint's connect completion. Deferring the stop
        /// keeps the transport start successful and reports the failure through the handler's
        /// <c>OnStopped</c>, matching the asynchronous TCP carrier.
        /// </remarks>
        bool IRawReliableEndpoint.Disconnect(StopReason reason)
        {
            RequestStop(reason);
            return true;
        }

        private void RequestStop(StopReason reason)
        {
            lock (_startGate)
            {
                if (_deferStopUntilStarted)
                {
                    if (_pendingStopReason == null)
                    {
                        _pendingStopReason = reason;
                    }

                    return;
                }
            }

            Stop(reason, false);
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
            ChannelManager.Instance.Disconnect(_serverEndPoint, _clientEndPoint);
            channel.Dispose();
        }

        public override string ToString()
        {
            try
            {
                return $"direct-client[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-client[unknown]";
            }
        }
    }
}
