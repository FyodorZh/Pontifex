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
    internal sealed class RawReliableAckDirectClientEndpoint : RawReliableAckClientEndpoint
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly IEndPoint _clientEndPoint;
        private readonly Action _onChannelDisposed;

        private Channel? _channel;

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

        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            if ((byte)messageType == RawReliableAckDirectServerEndpoint.RejectMessageType)
            {
                message.Release();
                Stop(new AckRejected(TransportName), false);
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
