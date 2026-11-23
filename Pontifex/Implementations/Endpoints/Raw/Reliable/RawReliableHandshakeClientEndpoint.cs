using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <inheritdoc cref="IRawReliableClientEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Handler callbacks</b>: a connected endpoint delivers <c>OnConnected</c>, then one
    /// <c>OnDisconnected</c>, then one <c>OnStopped</c>. An endpoint that never completes the transport
    /// handshake delivers exactly one <c>OnStopped</c> and no <c>OnDisconnected</c>. The logical connection is
    /// owned by the transport handshake; this endpoint reports it to the handler.</description></item>
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableHandshakeClientEndpoint : RawReliableClientEndpoint, IRawReliableClientEndpointImp
    {
        private const int DefaultHandshakeTimeoutSeconds = 10;

        private enum State
        {
            /// <summary>
            /// Before the handshake request is sent.
            /// </summary>
            Constructed = 0,

            /// <summary>
            /// The handshake request was sent and the response is not received yet.
            /// </summary>
            HandshakeSent,

            /// <summary>
            /// The handshake response was accepted.
            /// </summary>
            LogicallyConnected,

            /// <summary>
            /// The handshake failed or the low-level connection was destroyed.
            /// </summary>
            LogicallyDisconnected
        }

        private readonly IRawReliableClientHandler _handler;
        private readonly string _handshakeName;
        private readonly int _handshakeMaxByteSize;
        private readonly TimeSpan _handshakeTimeout;

        private volatile State _stage;
        private IDisposable? _handshakeTimer;

        /// <summary>
        /// Initializes a new reliable client endpoint that performs the transport handshake, with the given
        /// transport name, logging, and memory rental, and an optional keep-alive configuration.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives the connection lifecycle notifications and inbound
        /// messages.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1 and at least <paramref name="handshakeMaxByteSize"/>.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <param name="pingOptions">The keep-alive configuration, or <see langword="null"/> to disable
        /// keep-alive.</param>
        /// <param name="handshakeName">The transport handshake name. Must be non-null.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum size of a handshake frame; must be at least
        /// 1.</param>
        /// <param name="handshakeTimeout">The handshake deadline, or <see langword="null"/> for the ten-second
        /// default. <see cref="Timeout.InfiniteTimeSpan"/> disables the deadline.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint. The
        /// endpoint starts in the constructed stage.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableHandshakeClientEndpoint(string transportName, IEndPoint remoteEndPoint,
            IRawReliableClientHandler handler, int messageMaxByteSize, long bufferCapacityBytes,
            IMemoryRental memory, ILogger log, RawReliableClientEndpoint.PingOptions? pingOptions,
            string handshakeName, int handshakeMaxByteSize = RawReliableHandshake.DefaultMaxByteSize,
            TimeSpan? handshakeTimeout = null)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log, pingOptions)
        {
            if (handshakeName == null)
            {
                throw new ArgumentNullException(nameof(handshakeName));
            }

            if (handshakeMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeMaxByteSize));
            }

            if (messageMaxByteSize < handshakeMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxByteSize),
                    "The message maximum must be at least the handshake maximum.");
            }

            var resolvedTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(DefaultHandshakeTimeoutSeconds);
            if (resolvedTimeout != Timeout.InfiniteTimeSpan && resolvedTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            _handler = handler;
            _handshakeName = handshakeName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _handshakeTimeout = resolvedTimeout;

            Stopped += OnEndpointStopped;
        }

        /// <summary>
        /// Gets whether the endpoint is logically connected, meaning the low-level connection is up and the
        /// transport handshake has completed successfully.
        /// </summary>
        protected override bool GetIsConnected()
        {
            return IsLowLevelConnected && _stage == State.LogicallyConnected;
        }

        /// <summary>
        /// Gets whether the endpoint sent the handshake request and is still waiting for the response
        /// or the reject frame.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: A concrete carrier that handles a rejection
        /// synchronously uses this to ignore handshake frames that arrive outside the handshake
        /// phase, matching the inherited scheduled path.</description></item>
        /// </list>
        /// </remarks>
        protected bool IsHandshakePending => _stage == State.HandshakeSent;

        /// <summary>
        /// Starts the transport handshake. The inherited keep-alive behavior is deliberately not started here;
        /// it starts only after the handshake response is accepted.
        /// </summary>
        protected override void OnConnected()
        {
            _actionQueue.Schedule(BeginHandshake);
        }

        private void BeginHandshake()
        {
            if (!IsLowLevelConnected || _stage != State.Constructed)
            {
                return;
            }

            _stage = State.HandshakeSent;

            UnionDataList? request = null;
            try
            {
                request = RawReliableHandshake.BuildRequest(_handshakeName, MessageMaxByteSize);
                request.PutFirst((byte)MessageType.HandshakeRequestMessage);

                if (request.GetDataSize() > _handshakeMaxByteSize)
                {
                    Stop(new TextFail(TransportName, "Handshake request exceeds HandshakeMaxByteSize"), false);
                    return;
                }

                var toSend = request;
                request = null;
                var result = ScheduleToSend(toSend);
                if (result != SendResult.Ok)
                {
                    Stop(new TextFail(TransportName, $"Failed to send the handshake request: {result}"), false);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex, "Failed to start the transport handshake"), false);
                return;
            }
            finally
            {
                request?.Release();
            }

            if (!IsRunning)
            {
                return;
            }

            StartHandshakeTimer();
        }

        /// <summary>
        /// Creates the timer that enforces the handshake deadline.
        /// </summary>
        /// <param name="timeout">The handshake deadline.</param>
        /// <param name="onTimeout">The callback invoked when the deadline elapses. Must be non-null.</param>
        /// <returns>The disposable timer, or <see langword="null"/> to disable the deadline.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The default returns a <see cref="System.Threading.Timer"/>.
        /// Tests override this to inject a manual timer, and in-process carriers override it to return
        /// <see langword="null"/> because their handshake is synchronous.</description></item>
        /// </list>
        /// </remarks>
        protected virtual IDisposable? CreateHandshakeTimer(TimeSpan timeout, Action onTimeout)
        {
            return new Timer(_ => onTimeout(), null, timeout, Timeout.InfiniteTimeSpan);
        }

        private void StartHandshakeTimer()
        {
            IDisposable? timer;
            try
            {
                timer = CreateHandshakeTimer(_handshakeTimeout, OnHandshakeTimeout);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex, "Failed to start the handshake timer"), false);
                return;
            }

            if (timer == null)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _handshakeTimer, timer, null) != null)
            {
                // A concurrent stop or completion already installed or cleared the slot.
                try
                {
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }

                return;
            }

            if (!IsRunning)
            {
                StopHandshakeTimer();
            }
        }

        private void OnHandshakeTimeout()
        {
            StopHandshakeTimer();

            _actionQueue.Schedule(() =>
            {
                if (_stage != State.HandshakeSent)
                {
                    return;
                }

                _stage = State.LogicallyDisconnected;
                Stop(new TimeOut(TransportName), false);
            });
        }

        private void StopHandshakeTimer()
        {
            var timer = Interlocked.Exchange(ref _handshakeTimer, null);
            if (timer == null)
            {
                return;
            }

            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private void OnEndpointStopped(StopReason reason)
        {
            StopHandshakeTimer();
        }

        /// <summary>
        /// Handles a handshake response or rejection, and delegates all other frame types to the base
        /// implementation.
        /// </summary>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.HandshakeResponseMessage:
                    if (message.GetDataSize() > _handshakeMaxByteSize)
                    {
                        message.Release();
                        Stop(new TextFail(TransportName, "Handshake response exceeds HandshakeMaxByteSize"), false);
                        return true;
                    }

                    CompleteHandshake(message);
                    return true;

                case MessageType.HandshakeRejectMessage:
                    if (message.GetDataSize() > _handshakeMaxByteSize)
                    {
                        message.Release();
                        Stop(new TextFail(TransportName, "Handshake reject exceeds HandshakeMaxByteSize"), false);
                        return true;
                    }

                    CompleteRejectedHandshake(message);
                    return true;

                default:
                    return base.ProcessInboundMessage(messageType, message);
            }
        }

        private void CompleteHandshake(UnionDataList message)
        {
            bool scheduled = _actionQueue.Schedule(() =>
            {
                if (_stage != State.HandshakeSent)
                {
                    message.Release();
                    return;
                }

                StopHandshakeTimer();

                if (!RawReliableHandshake.TryParseResponse(
                        message, out short serverVersion, out string serverName, out int serverMax) ||
                    serverMax < 1)
                {
                    message.Release();
                    _stage = State.LogicallyDisconnected;
                    Stop(new ProtocolMismatch(TransportName, "Malformed transport handshake response"), false);
                    return;
                }

                message.Release();

                if (serverVersion != RawReliableHandshake.Version)
                {
                    _stage = State.LogicallyDisconnected;
                    Stop(new ProtocolMismatch(TransportName,
                        $"server {serverName}/v{serverVersion}: version mismatch"), false);
                    return;
                }

                if (!RawReliableHandshake.NameEquals(serverName, _handshakeName))
                {
                    _stage = State.LogicallyDisconnected;
                    Stop(new ProtocolMismatch(TransportName,
                        $"server {serverName}/v{serverVersion}: name mismatch"), false);
                    return;
                }

                if (!IsRunning)
                {
                    // A stop won the race; do not turn a normal teardown into an ExceptionFail by using
                    // the state-checked setter.
                    _stage = State.LogicallyDisconnected;
                    return;
                }

                int agreedMax = Math.Min(MessageMaxByteSize, serverMax);
                try
                {
                    SetMessageMaxByteSizeOnce(agreedMax);
                }
                catch (InvalidOperationException ex)
                {
                    Log.wtf(ex);
                    _stage = State.LogicallyDisconnected;
                    return;
                }

                _stage = State.LogicallyConnected;

                base.OnConnected();

                NotifyConnectedCore(() => _handler.OnConnected(this));
            });

            if (!scheduled)
            {
                message.Release();
                Stop(new TextFail(TransportName, "Failed to schedule the handshake completion"), false);
            }
        }

        private void CompleteRejectedHandshake(UnionDataList message)
        {
            bool scheduled = _actionQueue.Schedule(() =>
            {
                if (_stage != State.HandshakeSent)
                {
                    message.Release();
                    return;
                }

                StopHandshakeTimer();

                if (!RawReliableHandshake.TryParseReject(message, out short serverVersion,
                        out string serverName, out var status, out string? text))
                {
                    message.Release();
                    _stage = State.LogicallyDisconnected;
                    Stop(new ProtocolMismatch(TransportName, "Malformed transport handshake reject"), false);
                    return;
                }

                message.Release();

                string detail = string.IsNullOrEmpty(text)
                    ? RawReliableHandshake.FormatRejectText(serverName, serverVersion, status)
                    : text!;

                _stage = State.LogicallyDisconnected;
                Stop(MapReject(status, detail), false);
            });

            if (!scheduled)
            {
                message.Release();
                Stop(new TextFail(TransportName, "Failed to schedule the handshake rejection"), false);
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

        /// <summary>
        /// Handles a fatal endpoint failure by releasing the handshake timer and notifying the handler of the
        /// logical disconnection when the endpoint was connected, then invoking the base implementation.
        /// </summary>
        protected override void FailHard(StopReason reason)
        {
            _stage = State.LogicallyDisconnected;
            StopHandshakeTimer();
            NotifyDisconnectedCore(reason);

            base.FailHard(reason);
        }

        /// <summary>
        /// Releases the handshake timer and notifies the handler of the logical disconnection when the endpoint
        /// was connected.
        /// </summary>
        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            _stage = State.LogicallyDisconnected;
            StopHandshakeTimer();
            NotifyDisconnectedCore(reason);
        }
    }
}
