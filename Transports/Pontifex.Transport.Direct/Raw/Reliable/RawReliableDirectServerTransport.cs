using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Reliable server transport carried by in-process <see cref="Channel"/> instances.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock.
    /// Channel creation and handshake callbacks may run on any client thread; the handshake registry and the
    /// inherited handshake-factory lock protect the shared state.</description></item>
    /// <item><description><b>Transport handshake</b>: the first channel message must be the handshake request.
    /// The server validates it, calls the factory, and creates the session endpoint with the pending response.
    /// A rejection is written synchronously and the channel is closed immediately: the Direct reject linger is
    /// exactly zero.</description></item>
    /// <item><description><b>Other notes</b>: Once admitted, a session endpoint owns its channel and all
    /// data-plane traffic. There are no connection, handshake, idle, or keep-alive limits.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableDirectServerTransport : RawReliableAdmissionServerTransport, IRawReliableServer
    {
        private readonly StringEndPoint _serverEndPoint;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;

        private readonly object _channelsLock = new();
        private readonly Dictionary<IEndPoint, HandshakeEntry> _channels = new();

        /// <summary>
        /// Initializes a reliable Direct server transport.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// direct default.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted but not yet transmitted, or
        /// <see langword="null"/> for the direct default.</param>
        /// <param name="handshakeName">The transport handshake name. Must match the client name.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum size of a handshake frame.</param>
        /// <param name="handshakeTimeout">The server handshake deadline, or <see langword="null"/> for ten
        /// seconds. The public constructor rejects a non-positive or infinite value; only the internal test
        /// constructor may disable the deadline. The reject linger is zero.</param>
        public RawReliableDirectServerTransport(
            string serverName,
            ILogger logger,
            IMemoryRental memory,
            int? messageMaxSize = null,
            long? bufferCapacityBytes = null,
            string handshakeName = RawReliableDirectInfo.HandshakeName,
            int handshakeMaxByteSize = RawReliableDirectInfo.DefaultHandshakeMaxByteSize,
            TimeSpan? handshakeTimeout = null)
            : this(serverName, logger, memory, messageMaxSize, bufferCapacityBytes, handshakeName,
                handshakeMaxByteSize, handshakeTimeout ?? TimeSpan.FromSeconds(10), null)
        {
            if (handshakeTimeout == Timeout.InfiniteTimeSpan || handshakeTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }
        }

        internal RawReliableDirectServerTransport(
            string serverName,
            ILogger logger,
            IMemoryRental memory,
            int? messageMaxSize,
            long? bufferCapacityBytes,
            string handshakeName,
            int handshakeMaxByteSize,
            TimeSpan handshakeTimeout,
            IRawReliableHandshakeTimerFactory? timerFactory)
            : base(RawReliableDirectInfo.TransportName, logger, memory,
                int.MaxValue, int.MaxValue, int.MaxValue,
                handshakeName, handshakeMaxByteSize, handshakeTimeout, TimeSpan.Zero, timerFactory)
        {
            if (handshakeTimeout != Timeout.InfiniteTimeSpan && handshakeTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

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

            if (_messageMaxByteSize < HandshakeMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxSize),
                    "The message maximum must be at least the handshake maximum.");
            }
        }

        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Gets the configured outbound byte budget for parameter tests.
        /// </summary>
        internal long BufferCapacityBytesForTests => _bufferCapacityBytes;

        protected override bool DisconnectionByTimeoutCheck => false;

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
            // Disposes every channel that is still waiting for a handshake request. Admitted sessions were
            // detached from the registry and are stopped by the base transport.
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

            StartHandshakeTimer(entry);

            // The channel may have been disposed between registration and our subscription, for example by
            // a concurrent stop. The disposal notification would then be lost, so observe the state directly.
            if (channel.IsDisposed)
            {
                FailHandshake(entry);
            }
        }

        private void StartHandshakeTimer(HandshakeEntry entry)
        {
            if (HandshakeTimeout == Timeout.InfiniteTimeSpan)
            {
                return;
            }

            IDisposable? timer = null;
            try
            {
                timer = HandshakeTimerFactory.Schedule(HandshakeTimeout,
                    () => OnHandshakeTimeout(entry));
                entry.SetTimer(timer);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                timer?.Dispose();
                FailHandshake(entry);
            }
        }

        private void OnHandshakeTimeout(HandshakeEntry entry)
        {
            if (!entry.TryClaim())
            {
                return;
            }

            RemoveEntry(entry, true);
            AbandonAdmission();

            if (!entry.Channel.IsDisposed)
            {
                SendRejection(entry.Channel, RawReliableHandshake.HandshakeStatus.Timeout,
                    "handshake deadline elapsed");
            }

            RejectChannel(entry.Channel);
        }

        private void OnHandshakeMessage(HandshakeEntry entry, IEndPoint clientEp, UnionDataList message)
        {
            if (!entry.TryClaim())
            {
                message.Release();
                return;
            }

            RemoveEntry(entry, true);

            UnionDataList? response = null;
            IRawReliableServerHandler? handler = null;
            bool handedOff = false;
            try
            {
                if (message.GetDataSize() > HandshakeMaxByteSize)
                {
                    message.Release();
                    message = null!;
                    RejectHandshake(entry.Channel, RawReliableHandshake.HandshakeStatus.Rejected,
                        "handshake request exceeds HandshakeMaxByteSize");
                    return;
                }

                if (!message.TryPopFirst(out byte messageType) ||
                    messageType != RawReliableDirectServerEndpoint.HandshakeRequestMessageType)
                {
                    message.Release();
                    message = null!;
                    RejectHandshake(entry.Channel, RawReliableHandshake.HandshakeStatus.Rejected,
                        "unexpected handshake request frame");
                    return;
                }

                var request = message;
                message = null!;
                HandshakeOutcome outcome;
                try
                {
                    outcome = TryHandshake(clientEp, request);
                }
                finally
                {
                    request.Release();
                }

                if (!outcome.IsAccepted)
                {
                    RejectHandshake(entry.Channel, outcome.Status);
                    return;
                }

                handler = outcome.Handler!;
                response = outcome.Response;
                if (response == null)
                {
                    AbandonAdmission(handler, new TextFail(Name, "The handshake outcome has no response"));
                    RejectChannel(entry.Channel);
                    return;
                }

                RawReliableDirectServerEndpoint endpoint;
                try
                {
                    endpoint = new RawReliableDirectServerEndpoint(
                        Name,
                        clientEp,
                        entry.Channel,
                        handler,
                        outcome.AgreedMaxByteSize,
                        _bufferCapacityBytes,
                        HandshakeMaxByteSize,
                        Memory,
                        Log,
                        response);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    response.Release();
                    response = null;
                    AbandonAdmission(handler, new ExceptionFail(Name, ex, "Failed to create the server endpoint"));
                    RejectChannel(entry.Channel);
                    return;
                }

                // Ownership of the handshake response and of the channel transfers to the endpoint.
                response = null;
                handedOff = true;

                if (!ChannelManager.Instance.Detach(_serverEndPoint, clientEp))
                {
                    Log.d("The direct channel was already detached from the server registry");
                }

                try
                {
                    if (!CompleteAdmission(endpoint, handler,
                            new TextFail(Name, "Failed to start or register a direct session")))
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
                message?.Release();
                response?.Release();
                if (!handedOff)
                {
                    RejectHandshake(entry.Channel, RawReliableHandshake.HandshakeStatus.Rejected);
                }
                else
                {
                    RejectChannel(entry.Channel);
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

        private void RejectHandshake(Channel channel, RawReliableHandshake.HandshakeStatus status,
            string? text = null)
        {
            AbandonAdmission();
            SendRejection(channel, status, text);
            RejectChannel(channel);
        }

        /// <summary>
        /// Sends the rejection frame synchronously and closes the channel immediately. The Direct reject linger
        /// is zero.
        /// </summary>
        private void SendRejection(Channel channel, RawReliableHandshake.HandshakeStatus status,
            string? text = null)
        {
            string detail = text ?? RawReliableHandshake.FormatRejectText(
                HandshakeName, RawReliableHandshake.Version, status);

            if (TrySendRejection(channel, status, detail))
            {
                return;
            }

            // The detail made the frame too large: fall back to a minimal reject with no text. If even
            // that cannot fit, close without sending.
            if (TrySendRejection(channel, status, null))
            {
                return;
            }

            Log.w("Handshake reject exceeds HandshakeMaxByteSize and was not sent");
        }

        private bool TrySendRejection(Channel channel, RawReliableHandshake.HandshakeStatus status,
            string? detail)
        {
            UnionDataList? rejection = null;
            try
            {
                rejection = RawReliableHandshake.BuildReject(
                    RawReliableHandshake.Version, HandshakeName, status, detail);
                rejection.PutFirst(RawReliableDirectServerEndpoint.HandshakeRejectMessageType);
                if (rejection.GetDataSize() > HandshakeMaxByteSize)
                {
                    return false;
                }

                var toSend = rejection;
                rejection = null;
                channel.SendToClient(toSend);
                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
            finally
            {
                rejection?.Release();
            }
        }

        private void RejectChannel(Channel channel)
        {
            // Removes the channel from the server registry; the registry may already be gone, so the
            // explicit dispose covers that case.
            ChannelManager.Instance.Disconnect(_serverEndPoint, channel.ClientEp);
            channel.Dispose();
        }

        private void RemoveEntry(HandshakeEntry entry, bool unsubscribe)
        {
            entry.DisposeTimer();

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
                return $"{RawReliableDirectInfo.TransportName}-server[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return $"{RawReliableDirectInfo.TransportName}-server[unknown]";
            }
        }

        private sealed class HandshakeEntry
        {
            private readonly object _timerGate = new object();
            private int _claimed;
            private IDisposable? _timer;

            public readonly Channel Channel;

            public Action? OnDisposed;

            public HandshakeEntry(Channel channel)
            {
                Channel = channel;
            }

            public bool TryClaim()
            {
                if (Interlocked.Exchange(ref _claimed, 1) != 0)
                {
                    return false;
                }

                DisposeTimer();
                return true;
            }

            public void SetTimer(IDisposable timer)
            {
                lock (_timerGate)
                {
                    if (_claimed != 0 || _timer != null)
                    {
                        timer.Dispose();
                        return;
                    }

                    _timer = timer;
                }
            }

            public void DisposeTimer()
            {
                IDisposable? timer;
                lock (_timerGate)
                {
                    timer = _timer;
                    _timer = null;
                }

                timer?.Dispose();
            }
        }
    }
}
