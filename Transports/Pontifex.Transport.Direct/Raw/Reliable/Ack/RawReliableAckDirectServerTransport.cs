using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// Reliable acknowledgement server transport carried by in-process <see cref="Channel"/> instances.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock.
    /// Channel creation and handshake callbacks may run on any client thread; the handshake registry and the
    /// acknowledger serialization lock protect the shared state.</description></item>
    /// <item><description><b>Other notes</b>: The transport only performs the acknowledgement handshake and
    /// session admission. Once admitted, a session endpoint owns its channel and all data-plane traffic. There
    /// are no connection, handshake, idle, or keep-alive limits.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableAckDirectServerTransport : RawReliableAckServerTransport
    {
        private readonly StringEndPoint _serverEndPoint;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;

        private readonly object _channelsLock = new();
        private readonly Dictionary<IEndPoint, HandshakeEntry> _channels = new();

        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Initializes a reliable acknowledgement direct server transport.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// direct default.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted but not yet transmitted, or
        /// <see langword="null"/> for the direct default.</param>
        public RawReliableAckDirectServerTransport(
            string serverName,
            ILogger logger,
            IMemoryRental memory,
            int? messageMaxSize = null,
            long? bufferCapacityBytes = null)
            : base(DirectInfo.TransportName, logger, memory, int.MaxValue, int.MaxValue, int.MaxValue)
        {
            if (serverName == null)
            {
                throw new ArgumentNullException(nameof(serverName));
            }

            if (messageMaxSize.HasValue && messageMaxSize.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxSize));
            }

            if (bufferCapacityBytes.HasValue && bufferCapacityBytes.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCapacityBytes));
            }

            _serverEndPoint = new StringEndPoint(serverName);
            _messageMaxByteSize = messageMaxSize ?? DirectInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? DirectInfo.DefaultBufferCapacity;
        }

        protected override bool UsesSessionTicker => false;

        protected override bool StartCarrier()
        {
            if (!ChannelManager.Instance.RegisterServer(_serverEndPoint, OnChannelCreated))
            {
                Log.e("Failed to register direct server '{0}'. Name already in use.", _serverEndPoint);
                return false;
            }

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            // Disposes every channel that is still waiting for an acknowledgement request. Admitted
            // sessions were detached from the registry and are stopped by the base transport.
            ChannelManager.Instance.UnregisterServer(_serverEndPoint);

            HandshakeEntry[] snapshot;
            lock (_channelsLock)
            {
                if (_channels.Count == 0)
                {
                    return;
                }

                snapshot = new HandshakeEntry[_channels.Count];
                _channels.Values.CopyTo(snapshot, 0);
                _channels.Clear();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                FailHandshake(snapshot[i]);
            }
        }

        private void OnChannelCreated(Channel channel)
        {
            if (IsStopping || !IsStarted)
            {
                RejectChannel(channel);
                return;
            }

            if (!TryBeginAdmission())
            {
                RejectChannel(channel);
                return;
            }

            var entry = new HandshakeEntry(channel);
            entry.OnDisposed = () => OnHandshakeChannelDisposed(entry);
            channel.ServerHandler = (clientEp, message) => OnHandshakeMessage(entry, clientEp, message);
            channel.Disposed += entry.OnDisposed;

            bool added;
            lock (_channelsLock)
            {
                added = !_channels.ContainsKey(channel.ClientEp);
                if (added)
                {
                    _channels.Add(channel.ClientEp, entry);
                }
            }

            if (!added)
            {
                channel.Disposed -= entry.OnDisposed;
                channel.ServerHandler = null;
                AbandonAdmission();
                RejectChannel(channel);
                return;
            }

            // The channel may have been disposed between registration and our subscription, for example by
            // a concurrent stop. The disposal notification would then be lost, so observe the state directly.
            if (channel.IsDisposed)
            {
                FailHandshake(entry);
            }
        }

        private void OnHandshakeMessage(HandshakeEntry entry, IEndPoint clientEp, UnionDataList message)
        {
            if (!entry.TryClaim())
            {
                message.Release();
                return;
            }

            RemoveEntry(entry, true);

            UnionDataList? ackData = null;
            bool handedOff = false;
            try
            {
                if (message.GetDataSize() > RawReliableAckInfo.AckMaxByteSize)
                {
                    message.Release();
                    RejectHandshake(entry);
                    return;
                }

                if (!message.TryPopFirst(out byte messageType) || messageType != RawReliableAckDirectServerEndpoint.AckRequestMessageType)
                {
                    message.Release();
                    RejectHandshake(entry);
                    return;
                }

                ackData = message;

                IRawReliableAckServerHandler? handler = TryAcknowledge(clientEp, ackData);

                if (handler == null)
                {
                    ackData.Release();
                    ackData = null;

                    AbandonAdmission();
                    try
                    {
                        SendRejection(entry.Channel);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }

                    RejectChannel(entry.Channel);
                    return;
                }

                RawReliableAckDirectServerEndpoint endpoint;
                try
                {
                    endpoint = new RawReliableAckDirectServerEndpoint(
                        Name,
                        clientEp,
                        entry.Channel,
                        handler,
                        _messageMaxByteSize,
                        _bufferCapacityBytes,
                        Memory,
                        Log,
                        ackData);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    ackData.Release();
                    ackData = null;
                    RejectHandshake(entry);
                    return;
                }

                // Ownership of the acknowledgement data and of the channel transfers to the endpoint.
                ackData = null;
                handedOff = true;

                if (!ChannelManager.Instance.Detach(_serverEndPoint, clientEp))
                {
                    Log.d("The direct channel was already detached from the server registry");
                }

                try
                {
                    if (!CompleteAdmission(endpoint))
                    {
                        Log.w("Failed to start or register a direct session");
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                ackData?.Release();
                if (!handedOff)
                {
                    RejectHandshake(entry);
                }
            }
        }

        private void OnHandshakeChannelDisposed(HandshakeEntry entry)
        {
            FailHandshake(entry);
        }

        private void FailHandshake(HandshakeEntry entry)
        {
            if (!entry.TryClaim())
            {
                return;
            }

            RemoveEntry(entry, true);
            AbandonAdmission();
            RejectChannel(entry.Channel);
        }

        private void RejectHandshake(HandshakeEntry entry)
        {
            AbandonAdmission();
            RejectChannel(entry.Channel);
        }

        private void RejectChannel(Channel channel)
        {
            // Removes the channel from the server registry; the registry may already be gone, so the
            // explicit dispose covers that case.
            ChannelManager.Instance.Disconnect(_serverEndPoint, channel.ClientEp);
            channel.Dispose();
        }

        /// <summary>
        /// Sends the carrier-private rejection frame so the client can report <c>AckRejected</c> even though a
        /// channel close carries no reason.
        /// </summary>
        private void SendRejection(Channel channel)
        {
            var message = Memory.CollectablePool.Acquire<UnionDataList>();
            message.PutFirst(RawReliableAckDirectServerEndpoint.RejectMessageType);
            channel.SendToClient(message);
        }

        private void RemoveEntry(HandshakeEntry entry, bool unsubscribe)
        {
            if (unsubscribe)
            {
                entry.Channel.Disposed -= entry.OnDisposed;
            }

            lock (_channelsLock)
            {
                _channels.Remove(entry.Channel.ClientEp);
            }
        }

        public override string ToString()
        {
            try
            {
                return $"direct-server[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-server[unknown]";
            }
        }

        private sealed class HandshakeEntry
        {
            private int _claimed;

            public readonly Channel Channel;

            public Action? OnDisposed;

            public HandshakeEntry(Channel channel)
            {
                Channel = channel;
            }

            public bool TryClaim()
            {
                return Interlocked.Exchange(ref _claimed, 1) == 0;
            }
        }
    }
}
