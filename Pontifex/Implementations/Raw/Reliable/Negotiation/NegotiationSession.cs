using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// The role of a <see cref="NegotiationSession"/>.
    /// </summary>
    internal enum NegotiationRole
    {
        /// <summary>
        /// The session sends the request immediately from the transport-level connection.
        /// </summary>
        Client,

        /// <summary>
        /// The session consumes the first payload as the request.
        /// </summary>
        Server
    }

    /// <summary>
    /// Drives one reliable-wrapper negotiation over an already-admitted inner session.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Roles</b>: a client sends the request immediately from the transport-level
    /// <c>OnConnected</c>; a server consumes the first payload as the request. Nothing is delivered to the
    /// application before the negotiation is accepted.</description></item>
    /// <item><description><b>Ownership</b>: <see cref="OnReceived"/> takes ownership of the inbound message and
    /// releases it after processing. It returns <see langword="false"/> only when the message is not a
    /// negotiation message; in that case it does not release the message and the caller retains
    /// ownership.</description></item>
    /// <item><description><b>Hooks</b>: the custom-tail hooks receive a session-owned buffer that is released
    /// once after the hook returns. Hooks must neither retain nor release it. The append hook is append-only.
    /// </description></item>
    /// <item><description><b>Threading</b>: all state transitions are serialized by an internal lock. The
    /// timer callback may race with the inbound path; the lock and the terminal latch make rejection and
    /// timeout idempotent.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class NegotiationSession : IDisposable
    {
        private readonly NegotiationRole _role;
        private readonly short _version;
        private readonly string _name;
        private readonly int _handshakeMaxByteSize;
        private readonly TimeSpan _handshakeTimeout;
        private readonly TimeSpan _rejectLinger;
        private readonly ITimerFactory _timerFactory;
        private readonly Func<UnionDataList, SendResult> _send;
        private readonly Action<UnionDataList>? _appendData;
        private readonly Func<UnionDataList>? _createResponse;
        private readonly Func<UnionDataList, bool>? _checkData;
        private readonly Action<UnionDataList> _onAccepted;
        private readonly Action<StopReason> _onRejected;
        private readonly Action<StopReason> _disconnect;
        private readonly ILogger? _log;

        private readonly object _lock = new object();

        private IDisposable? _timer;
        private IDisposable? _lingerTimer;
        private bool _started;
        private bool _completed;
        private bool _disposed;

        /// <summary>
        /// Initializes a negotiation session.
        /// </summary>
        /// <param name="role">The role of this session.</param>
        /// <param name="version">The negotiated protocol version.</param>
        /// <param name="name">The layer negotiation name. Must be non-null.</param>
        /// <param name="send">Sends one envelope. Ownership transfers to the delegate; the delegate releases the
        /// envelope in every outcome, including an exceptional one.</param>
        /// <param name="onRejected">Invoked exactly once when the negotiation is rejected, times out, or fails
        /// locally.</param>
        /// <param name="disconnect">Invoked after the rejection linger elapses on the server side, or
        /// immediately when the linger is zero. It closes the inner session.</param>
        /// <param name="onAccepted">Invoked exactly once on admission with the session-owned custom tail. The
        /// session releases the tail after the hook returns.</param>
        /// <param name="appendData">Appends the client request custom tail. Append-only; may be
        /// <see langword="null"/>.</param>
        /// <param name="createResponse">Builds the server response custom tail. The returned list is owned and
        /// released by the session; may be <see langword="null"/> for an empty tail.</param>
        /// <param name="checkData">Validates the server request custom tail. May be
        /// <see langword="null"/> to accept every structure-valid request.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum envelope size; must be at least 1.</param>
        /// <param name="handshakeTimeout">The negotiation deadline, or <see langword="null"/> for the
        /// ten-second default. <see cref="Timeout.InfiniteTimeSpan"/> disables the deadline.</param>
        /// <param name="rejectLinger">How long the server keeps the inner session open after sending a reject,
        /// or <see langword="null"/> for the two-second default. Must not be negative.</param>
        /// <param name="timerFactory">The timer seam, or <see langword="null"/> for the system timer.</param>
        /// <param name="log">An optional logger for hook and send exceptions.</param>
        public NegotiationSession(
            NegotiationRole role,
            short version,
            string name,
            Func<UnionDataList, SendResult> send,
            Action<StopReason> onRejected,
            Action<StopReason> disconnect,
            Action<UnionDataList> onAccepted,
            Action<UnionDataList>? appendData = null,
            Func<UnionDataList>? createResponse = null,
            Func<UnionDataList, bool>? checkData = null,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? handshakeTimeout = null,
            TimeSpan? rejectLinger = null,
            ITimerFactory? timerFactory = null,
            ILogger? log = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            if (onRejected == null)
            {
                throw new ArgumentNullException(nameof(onRejected));
            }

            if (disconnect == null)
            {
                throw new ArgumentNullException(nameof(disconnect));
            }

            if (onAccepted == null)
            {
                throw new ArgumentNullException(nameof(onAccepted));
            }

            if (handshakeMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeMaxByteSize));
            }

            var resolvedTimeout = handshakeTimeout ?? RawReliableNegotiation.DefaultHandshakeTimeout;
            if (resolvedTimeout != Timeout.InfiniteTimeSpan && resolvedTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            var resolvedLinger = rejectLinger ?? RawReliableNegotiation.DefaultRejectLinger;
            if (resolvedLinger < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectLinger));
            }

            _role = role;
            _version = version;
            _name = name;
            _send = send;
            _onRejected = onRejected;
            _disconnect = disconnect;
            _onAccepted = onAccepted;
            _appendData = appendData;
            _createResponse = createResponse;
            _checkData = checkData;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _handshakeTimeout = resolvedTimeout;
            _rejectLinger = resolvedLinger;
            _timerFactory = timerFactory ?? SystemTimerFactory.Instance;
            _log = log;
        }

        /// <summary>
        /// Gets the role of this session.
        /// </summary>
        public NegotiationRole Role => _role;

        /// <summary>
        /// Gets the layer negotiation name.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the negotiation deadline.
        /// </summary>
        public TimeSpan HandshakeTimeout => _handshakeTimeout;

        /// <summary>
        /// Gets the rejection linger.
        /// </summary>
        public TimeSpan RejectLinger => _rejectLinger;

        /// <summary>
        /// Starts the session. A client sends its request immediately; both roles arm the deadline.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_started || _disposed || _completed)
                {
                    return;
                }

                _started = true;

                if (_role == NegotiationRole.Client && !SendRequest())
                {
                    return;
                }

                if (_completed || _disposed)
                {
                    return;
                }

                StartDeadline();
            }
        }

        /// <summary>
        /// Processes one inbound payload.
        /// </summary>
        /// <param name="message">The inbound payload. Ownership transfers to this method when it returns
        /// <see langword="true"/>; when it returns <see langword="false"/> the caller retains ownership and
        /// must release it.</param>
        /// <returns><see langword="true"/> when the message was consumed as a negotiation message; otherwise
        /// <see langword="false"/>.</returns>
        public bool OnReceived(UnionDataList message)
        {
            if (message == null)
            {
                return false;
            }

            lock (_lock)
            {
                if (_disposed || _completed)
                {
                    return false;
                }

                return _role == NegotiationRole.Client
                    ? OnClientReceived(message)
                    : OnServerReceived(message);
            }
        }

        /// <summary>
        /// Rejects the peer explicitly. Used by the server wrapper when the first payload is not a negotiation
        /// envelope. The reject envelope is sent, the linger is armed, and the inner session is closed when the
        /// linger elapses.
        /// </summary>
        public void Reject(StopReason reason, string? text)
        {
            if (reason == null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            lock (_lock)
            {
                if (_disposed || _completed)
                {
                    return;
                }

                RejectRemote(reason, text);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                CancelDeadline();
                CancelLinger();
            }
        }

        private bool SendRequest()
        {
            UnionDataList? tail = null;
            UnionDataList? envelope = null;
            try
            {
                tail = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                _appendData?.Invoke(tail);

                envelope = RawReliableNegotiation.BuildRequest(_version, _name, tail);
                if (envelope.GetDataSize() > _handshakeMaxByteSize)
                {
                    envelope.Release();
                    envelope = null;
                    RejectLocal(new ProtocolMismatch(Source,
                        "negotiation request exceeds the handshake maximum"));
                    return false;
                }

                SendResult result = SendEnvelope(envelope);
                envelope = null;
                if (result != SendResult.Ok)
                {
                    RejectLocal(new TextFail(Source, "failed to send the negotiation request"));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                envelope?.Release();
                RejectLocal(new ExceptionFail(Source, ex, "failed to build the negotiation request"));
                return false;
            }
            finally
            {
                tail?.Release();
            }
        }

        private bool OnClientReceived(UnionDataList message)
        {
            if (!RawReliableNegotiation.TryReadEnvelope(message, out short version, out string name,
                    out NegotiationStatus status, out UnionDataList tail))
            {
                return false;
            }

            CancelDeadline();

            try
            {
                if (status == NegotiationStatus.Rejected)
                {
                    string? text = RawReliableNegotiation.ReadRejectText(tail);
                    _completed = true;
                    InvokeRejected(new HandshakeRejected(Source, text ?? "rejected"));
                }
                else if (status != NegotiationStatus.Accepted)
                {
                    _completed = true;
                    InvokeRejected(new ProtocolMismatch(Source,
                        "unexpected negotiation status"));
                }
                else if (version != _version)
                {
                    _completed = true;
                    InvokeRejected(new ProtocolMismatch(Source,
                        "negotiation version mismatch"));
                }
                else if (!RawReliableNegotiation.NameEquals(name, _name))
                {
                    _completed = true;
                    InvokeRejected(new ProtocolMismatch(Source,
                        "negotiation name mismatch"));
                }
                else
                {
                    _completed = true;
                    try
                    {
                        _onAccepted(tail);
                    }
                    catch (Exception ex)
                    {
                        InvokeRejected(new ExceptionFail(Source, ex,
                            "negotiation acceptance hook failed"));
                    }
                }
            }
            finally
            {
                tail.Release();
                message.Release();
            }

            return true;
        }

        private bool OnServerReceived(UnionDataList message)
        {
            if (!RawReliableNegotiation.TryReadEnvelope(message, out short version, out string name,
                    out NegotiationStatus status, out UnionDataList tail))
            {
                return false;
            }

            CancelDeadline();

            try
            {
                if (status != NegotiationStatus.Request)
                {
                    RejectRemote(new ProtocolMismatch(Source, "expected a negotiation request"),
                        "expected a negotiation request");
                    return true;
                }

                if (version != _version)
                {
                    RejectRemote(new ProtocolMismatch(Source, "negotiation version mismatch"),
                        "version mismatch");
                    return true;
                }

                if (!RawReliableNegotiation.NameEquals(name, _name))
                {
                    RejectRemote(new ProtocolMismatch(Source, "negotiation name mismatch"),
                        "name mismatch");
                    return true;
                }

                bool accepted;
                try
                {
                    accepted = _checkData == null || _checkData(tail);
                }
                catch (Exception ex)
                {
                    LogRejectException(ex);
                    accepted = false;
                }

                if (!accepted)
                {
                    RejectRemote(new HandshakeRejected(Source, "rejected"), "rejected");
                    return true;
                }

                if (!SendResponse())
                {
                    return true;
                }

                if (_disposed)
                {
                    return true;
                }

                _completed = true;
                try
                {
                    _onAccepted(tail);
                }
                catch (Exception ex)
                {
                    LogRejectException(ex);
                }
            }
            finally
            {
                tail.Release();
                message.Release();
            }

            return true;
        }

        private bool SendResponse()
        {
            UnionDataList? responseTail = null;
            UnionDataList? envelope = null;
            try
            {
                try
                {
                    responseTail = _createResponse?.Invoke()
                                   ?? MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                }
                catch (Exception ex)
                {
                    LogRejectException(ex);
                    RejectRemote(new HandshakeRejected(Source,
                        "negotiation response hook failed"), "response hook failed");
                    return false;
                }

                envelope = RawReliableNegotiation.BuildResponse(_version, _name, responseTail);
                if (envelope.GetDataSize() > _handshakeMaxByteSize)
                {
                    envelope.Release();
                    envelope = null;
                    RejectRemote(new HandshakeRejected(Source,
                        "negotiation response exceeds the handshake maximum"), "response too large");
                    return false;
                }

                SendResult result = SendEnvelope(envelope);
                envelope = null;
                if (result != SendResult.Ok)
                {
                    RejectRemote(new TextFail(Source, "failed to send the negotiation response"),
                        "response send failed");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                envelope?.Release();
                RejectRemote(new ExceptionFail(Source, ex, "failed to build the negotiation response"),
                    "response build failed");
                return false;
            }
            finally
            {
                responseTail?.Release();
            }
        }

        private void StartDeadline()
        {
            if (_handshakeTimeout == Timeout.InfiniteTimeSpan)
            {
                return;
            }

            _timer = _timerFactory.Schedule(_handshakeTimeout, OnDeadline);
        }

        private void OnDeadline()
        {
            lock (_lock)
            {
                if (_disposed || _completed)
                {
                    return;
                }

                _timer = null;

                if (_role == NegotiationRole.Server)
                {
                    RejectRemote(new TimeOut(Source), "negotiation timeout");
                }
                else
                {
                    _completed = true;
                    InvokeRejected(new TimeOut(Source));
                }
            }
        }

        private void RejectLocal(StopReason reason)
        {
            if (_completed || _disposed)
            {
                return;
            }

            _completed = true;
            CancelDeadline();
            InvokeRejected(reason);
        }

        private void RejectRemote(StopReason reason, string? text)
        {
            if (_completed || _disposed)
            {
                return;
            }

            _completed = true;
            CancelDeadline();

            UnionDataList? envelope = null;
            try
            {
                envelope = RawReliableNegotiation.BuildReject(_version, _name, text);
                if (envelope.GetDataSize() > _handshakeMaxByteSize)
                {
                    envelope.Release();
                    envelope = null;
                }
                else
                {
                    SendEnvelope(envelope);
                    envelope = null;
                }
            }
            catch (Exception ex)
            {
                envelope?.Release();
                LogRejectException(ex);
            }
            finally
            {
                envelope?.Release();
            }

            InvokeRejected(reason);

            if (_role != NegotiationRole.Server)
            {
                return;
            }

            ScheduleLinger(reason);
        }

        private void ScheduleLinger(StopReason reason)
        {
            if (_rejectLinger <= TimeSpan.Zero)
            {
                InvokeDisconnect(reason);
                return;
            }

            _lingerTimer = _timerFactory.Schedule(_rejectLinger, () =>
            {
                lock (_lock)
                {
                    _lingerTimer = null;
                    if (_disposed)
                    {
                        return;
                    }

                    InvokeDisconnect(reason);
                }
            });
        }

        private SendResult SendEnvelope(UnionDataList envelope)
        {
            try
            {
                return _send(envelope);
            }
            catch (Exception ex)
            {
                LogRejectException(ex);
                return SendResult.Error;
            }
        }

        private void InvokeRejected(StopReason reason)
        {
            try
            {
                _onRejected(reason);
            }
            catch (Exception ex)
            {
                LogRejectException(ex);
            }
        }

        private void InvokeDisconnect(StopReason reason)
        {
            try
            {
                _disconnect(reason);
            }
            catch (Exception ex)
            {
                LogRejectException(ex);
            }
        }

        private void CancelDeadline()
        {
            var timer = _timer;
            _timer = null;
            timer?.Dispose();
        }

        private void CancelLinger()
        {
            var timer = _lingerTimer;
            _lingerTimer = null;
            timer?.Dispose();
        }

        private string Source => $"negotiation:{_name}";

        private void LogRejectException(Exception ex)
        {
            _log?.wtf(ex);
        }
    }
}
