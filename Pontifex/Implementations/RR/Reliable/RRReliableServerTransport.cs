using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Base class of the RR reliable server transports.
    /// </summary>
    /// <remarks>
    /// The class owns the server request mechanics: per-client sessions, the completed-id window,
    /// response handles, the serialized <c>OnRequested</c> dispatch, duplicate suppression, the
    /// response TTL, and session closure. A carrier supplies <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRReliableServerTransport
        : RRReliableTransport<IRRReliableServerHandler>, IRRReliableServerTransport
    {
        private readonly TimeSpan _responseTtl;
        private readonly int _dispatchCapacity;
        private readonly object _sessionsLocker = new object();
        private readonly Dictionary<IEndPoint, RRReliableServerSession> _sessions =
            new Dictionary<IEndPoint, RRReliableServerSession>();
        private readonly object _dispatchLocker = new object();
        private readonly List<DispatchItem> _pendingFrames = new List<DispatchItem>();

        private readonly Func<int, string, Action<DispatchItem>, Action<DispatchItem>,
            ISerializedCallbackQueue<DispatchItem>> _dispatchQueueFactory;
        private ISerializedCallbackQueue<DispatchItem>? _dispatchQueue;
        private bool _dispatchReady;
        private bool _serverStopping;

        /// <summary>
        /// Initializes a new RR reliable server transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="responseTtl">How long the application may take to answer one request. Must
        /// be positive.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timerFactory">The timer seam for TTL callbacks, or <see langword="null"/>
        /// for the system timer.</param>
        /// <param name="dispatchCapacity">The number of frames the serialized dispatch queue and
        /// its pre-start pending list may hold.</param>
        protected RRReliableServerTransport(string typeName, TimeSpan responseTtl, ILogger logger,
            IMemoryRental memory, ITimerFactory? timerFactory = null,
            int dispatchCapacity = 1024)
            : this(typeName, responseTtl, logger, memory, timerFactory, dispatchCapacity,
                (capacity, name, handler, disposer) =>
                    new SerializedCallbackQueue<DispatchItem>(capacity, name, handler, disposer))
        {
        }

        /// <summary>
        /// Initializes a new RR reliable server transport with an injectable dispatch queue.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal seam for the Direct wiring test, which injects a
        /// manual queue. The protected constructor keeps the production serialized queue.</description></item>
        /// </list>
        /// </remarks>
        internal RRReliableServerTransport(string typeName, TimeSpan responseTtl, ILogger logger,
            IMemoryRental memory, ITimerFactory? timerFactory, int dispatchCapacity,
            Func<int, string, Action<DispatchItem>, Action<DispatchItem>,
                ISerializedCallbackQueue<DispatchItem>> dispatchQueueFactory)
            : base(typeName, logger, memory, timerFactory)
        {
            if (responseTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTtl), "The TTL must be positive.");
            }

            if (dispatchCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dispatchCapacity),
                    "The dispatch capacity must be positive.");
            }

            _responseTtl = responseTtl;
            _dispatchCapacity = dispatchCapacity;
            _dispatchQueueFactory = dispatchQueueFactory ??
                throw new ArgumentNullException(nameof(dispatchQueueFactory));
        }

        /// <inheritdoc/>
        public TimeSpan ResponseTtl => _responseTtl;

        /// <summary>
        /// Creates the serialized <c>OnRequested</c> queue. A concrete carrier calls this before it
        /// registers with its carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the queue exists; it is idempotent.</returns>
        protected bool StartDispatch()
        {
            lock (_dispatchLocker)
            {
                if (_dispatchQueue != null)
                {
                    return true;
                }

                _dispatchQueue = _dispatchQueueFactory(
                    _dispatchCapacity, Name + "-dispatch", Dispatch, DisposeItem);
                return true;
            }
        }

        /// <summary>
        /// Disposes the dispatch queue without waiting for a running callback. Frames still queued
        /// or pending are released, and their handles are closed and recorded terminal.
        /// </summary>
        protected void StopDispatch()
        {
            ISerializedCallbackQueue<DispatchItem>? queue;
            var pending = new List<DispatchItem>();

            lock (_dispatchLocker)
            {
                _dispatchReady = false;
                queue = _dispatchQueue;
                _dispatchQueue = null;
                pending.AddRange(_pendingFrames);
                _pendingFrames.Clear();
            }

            queue?.Dispose();

            foreach (DispatchItem item in pending)
            {
                DisposeItem(item);
            }
        }

        /// <inheritdoc/>
        protected override void OnStarted()
        {
            base.OnStarted();
            StartDispatch();

            lock (_dispatchLocker)
            {
                _dispatchReady = true;

                foreach (DispatchItem item in _pendingFrames)
                {
                    PostOrDisposeLocked(item);
                }

                _pendingFrames.Clear();
            }
        }

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            List<RRReliableServerSession> sessions;

            lock (_sessionsLocker)
            {
                _serverStopping = true;
                sessions = new List<RRReliableServerSession>(_sessions.Values);
                _sessions.Clear();
            }

            foreach (RRReliableServerSession session in sessions)
            {
                CloseSession(session);
            }
        }

        /// <summary>
        /// Sends one encoded frame to <paramref name="destination"/>.
        /// </summary>
        /// <param name="destination">The route that received the request.</param>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on
        /// every path.</param>
        protected abstract SendResult SendFrame(IEndPoint destination, UnionDataList frame);

        /// <summary>
        /// Handles one inbound frame received by the concrete carrier.
        /// </summary>
        /// <param name="source">The route that sent the frame.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
        protected void OnFrameReceived(IEndPoint source, UnionDataList frame)
        {
            if (frame == null!)
            {
                return;
            }

            if (!RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header))
            {
                Log.w("{0} received a malformed frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (header.FrameType == RRReliableFrameType.Cancel)
            {
                HandleCancel(source, header.RequestId);
                frame.Release();
                return;
            }

            if (header.FrameType != RRReliableFrameType.Request)
            {
                frame.Release();
                return;
            }

            int size;

            try
            {
                size = frame.GetDataSize();
            }
            catch (Exception)
            {
                frame.Release();
                return;
            }

            if (size > MessageMaxByteSize)
            {
                Log.w("{0} dropped an oversized request from {1}", Name, source);
                frame.Release();
                return;
            }

            HandleRequest(source, header.RequestId, frame);
        }

        /// <summary>
        /// Closes the session of <paramref name="source"/> without sending anything and removes it.
        /// Other sessions stay usable.
        /// </summary>
        /// <param name="source">The route of the closed session.</param>
        protected void OnSessionClosed(IEndPoint source)
        {
            RRReliableServerSession? session;

            lock (_sessionsLocker)
            {
                if (!_sessions.TryGetValue(source, out session))
                {
                    return;
                }

                _sessions.Remove(source);
            }

            CloseSession(session);
        }

        /// <summary>
        /// Sends one frame produced by a response handle.
        /// </summary>
        internal SendResult SendResponse(RRReliableResponse response, UnionDataList payload)
        {
            if (payload == null!)
            {
                return SendResult.InvalidMessage;
            }

            try
            {
                return SendFrame(response.Source, payload);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return SendResult.Error;
            }
        }

        private void HandleRequest(IEndPoint source, ushort clientLocalId, UnionDataList request)
        {
            RRReliableServerSession session;

            lock (_sessionsLocker)
            {
                if (_serverStopping)
                {
                    request.Release();
                    return;
                }

                if (!_sessions.TryGetValue(source, out RRReliableServerSession? existing))
                {
                    session = new RRReliableServerSession();
                    _sessions.Add(source, session);
                }
                else
                {
                    session = existing;
                }
            }

            RRReliableResponse? response = null;
            bool reject = false;

            lock (session.SyncRoot)
            {
                if (session.IsClosed)
                {
                    request.Release();
                    return;
                }

                if (session.IsLive(clientLocalId))
                {
                    request.Release();
                    return;
                }

                if (session.IsFull)
                {
                    reject = true;
                }
                else if (session.IsCompleted(clientLocalId))
                {
                    request.Release();
                    return;
                }
                else
                {
                    response = new RRReliableResponse(this, NextServerRequestId(), source,
                        clientLocalId, session);
                    session.AddLive(clientLocalId, response);

                    try
                    {
                        IDisposable timer = TimerFactory.Schedule(_responseTtl,
                            () => OnResponseTtl(response));
                        response.AttachTimer(timer);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                        session.TryTakeLive(clientLocalId, response);
                        response.CloseForDisposal();
                        request.Release();
                        return;
                    }
                }
            }

            if (reject)
            {
                var rejecting = new RRReliableResponse(this, NextServerRequestId(), source,
                    clientLocalId, session);
                SendResult result = rejecting.Reject();

                if (result != SendResult.Ok)
                {
                    rejecting.CloseForDisposal();
                }

                request.Release();
                return;
            }

            var item = new DispatchItem(source, request, DateTime.UtcNow, clientLocalId,
                response!, session);

            lock (_dispatchLocker)
            {
                if (!_dispatchReady)
                {
                    if (_pendingFrames.Count >= _dispatchCapacity)
                    {
                        DisposeItem(item);
                        return;
                    }

                    _pendingFrames.Add(item);
                    return;
                }

                PostOrDisposeLocked(item);
            }
        }

        private void HandleCancel(IEndPoint source, ushort clientLocalId)
        {
            RRReliableServerSession? session;

            lock (_sessionsLocker)
            {
                if (!_sessions.TryGetValue(source, out session))
                {
                    return;
                }
            }

            RRReliableResponse? handle = null;

            lock (session.SyncRoot)
            {
                if (!session.IsClosed)
                {
                    session.TryTakeLive(clientLocalId, out handle);
                }
            }

            handle?.MarkClosed();
        }

        private void PostOrDisposeLocked(DispatchItem item)
        {
            ISerializedCallbackQueue<DispatchItem>? queue = _dispatchQueue;

            if (queue == null || !queue.Post(item))
            {
                DisposeItem(item);
            }
        }

        private void Dispatch(DispatchItem item)
        {
            if (item.Response.IsClosed)
            {
                item.Request.Release();
                return;
            }

            IRRReliableServerHandler? handler = DispatchHandler;

            if (handler == null)
            {
                item.Response.CloseForDisposal();
                item.Request.Release();
                return;
            }

            try
            {
                handler.OnRequested(item.Source, item.Request, item.Response);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);

                if (item.Response.IsUndecided)
                {
                    SendResult result = item.Response.Reject();

                    if (result != SendResult.Ok)
                    {
                        item.Response.CloseForDisposal();
                    }
                }
            }
        }

        private void DisposeItem(DispatchItem item)
        {
            item.Response.CloseForDisposal();
            item.Request.Release();
        }

        private void OnResponseTtl(RRReliableResponse response)
        {
            try
            {
                response.SendFailed();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private void CloseSession(RRReliableServerSession session)
        {
            RRReliableResponse[] handles;

            lock (session.SyncRoot)
            {
                handles = session.Close();
            }

            foreach (RRReliableResponse handle in handles)
            {
                handle.MarkClosed();
            }
        }

        internal sealed class DispatchItem
        {
            public DispatchItem(IEndPoint source, UnionDataList request, DateTime receivedAt,
                ushort clientLocalId, RRReliableResponse response,
                RRReliableServerSession session)
            {
                Source = source;
                Request = request;
                ReceivedAt = receivedAt;
                ClientLocalId = clientLocalId;
                Response = response;
                Session = session;
            }

            public IEndPoint Source { get; }

            public UnionDataList Request { get; }

            public DateTime ReceivedAt { get; }

            public ushort ClientLocalId { get; }

            public RRReliableResponse Response { get; }

            public RRReliableServerSession Session { get; }
        }
    }
}
