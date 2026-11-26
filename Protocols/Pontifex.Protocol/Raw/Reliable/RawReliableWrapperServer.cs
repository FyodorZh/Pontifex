using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Protocols
{
    /// <summary>
    /// Base reliable server wrapper: negotiates a layer dataset for every admitted inner session and defers
    /// the user handler factory until the negotiation is accepted.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Factory contract</b>: the wrapped factory is invoked after the negotiation
    /// request validates the layer dataset, and every invocation is serialized by one lock. A
    /// <see langword="null"/> result rejects the connection.</description></item>
    /// <item><description><b>Lifecycle</b>: an admitted session observes <c>OnConnected</c> →
    /// <c>OnDisconnected</c> → <c>OnStopped</c> exactly once. A never-admitted session that created a handler
    /// observes exactly one <c>OnStopped</c> and no <c>OnDisconnected</c>.</description></item>
    /// <item><description><b>Ownership</b>: the wrapper owns the negotiation envelopes. The logic hooks receive
    /// only the custom tail and must not retain or release it.</description></item>
    /// <item><description><b>Per-session logic</b>: the preferred constructor takes a logic factory and creates a
    /// fresh logic instance for every accepted session. A session-scoped logic may release state in
    /// <c>OnDisconnected</c> (for example a compressor), so it must not be shared between sequential sessions.
    /// The single-instance constructor is retained for stateless logic and for tests; it shares one instance
    /// across every session.</description></item>
    /// </list>
    /// </remarks>
    public class RawReliableWrapperServer : RawReliableTransport, IRawReliableServer
    {
        private readonly IRawReliableServer _inner;
        private readonly short _version;
        private readonly string _negotiationName;
        private readonly int _handshakeMaxByteSize;
        private readonly TimeSpan _rejectLinger;
        private readonly ITimerFactory _timerFactory;

        private readonly object _factoryLock = new object();

        private Func<IRawReliableWrapperLogic>? _logicFactory;
        private IRawReliableServerHandlerFactory<IRawReliableServerHandler>? _factory;
        private bool _initialized;

        /// <summary>
        /// Initializes a wrapper server.
        /// </summary>
        /// <param name="typeName">The display name of the wrapper transport.</param>
        /// <param name="negotiationName">The layer negotiation name. Must be non-null.</param>
        /// <param name="inner">The wrapped reliable server. Must be non-null.</param>
        /// <param name="logic">The layer logic. Must be non-null.</param>
        /// <param name="version">The layer negotiation version.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum negotiation envelope size.</param>
        /// <param name="rejectLinger">How long a rejected session stays open, or <see langword="null"/> for the
        /// two-second default.</param>
        public RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            IRawReliableWrapperLogic logic, short version = RawReliableNegotiation.DefaultVersion,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? rejectLinger = null)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, CreateSharedFactory(logic),
                version, handshakeMaxByteSize, rejectLinger, null)
        {
        }

        /// <summary>
        /// Initializes a wrapper server that creates a fresh logic instance for every accepted session.
        /// </summary>
        /// <param name="typeName">The display name of the wrapper transport.</param>
        /// <param name="negotiationName">The layer negotiation name. Must be non-null.</param>
        /// <param name="inner">The wrapped reliable server. Must be non-null.</param>
        /// <param name="logicFactory">Creates the layer logic for one accepted session. Must be non-null and
        /// must not return <see langword="null"/>.</param>
        /// <param name="version">The layer negotiation version.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum negotiation envelope size.</param>
        /// <param name="rejectLinger">How long a rejected session stays open, or <see langword="null"/> for the
        /// two-second default.</param>
        public RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            Func<IRawReliableWrapperLogic> logicFactory, short version = RawReliableNegotiation.DefaultVersion,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? rejectLinger = null)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, logicFactory,
                version, handshakeMaxByteSize, rejectLinger, null)
        {
        }

        /// <summary>
        /// Initializes a wrapper server whose logic is supplied by a derived type through
        /// <see cref="SetupLogic"/>.
        /// </summary>
        protected RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            ILogger logger, IMemoryRental memory, short version = RawReliableNegotiation.DefaultVersion,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? rejectLinger = null)
            : this(typeName, negotiationName, inner, logger, memory, null, version, handshakeMaxByteSize,
                rejectLinger, null)
        {
        }

        /// <summary>
        /// Initializes a wrapper server with an explicit time seam. Intended for deterministic tests.
        /// </summary>
        internal RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            IRawReliableWrapperLogic logic, short version, int handshakeMaxByteSize, TimeSpan? rejectLinger,
            ITimerFactory? timerFactory)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, CreateSharedFactory(logic),
                version, handshakeMaxByteSize, rejectLinger, timerFactory)
        {
        }

        /// <summary>
        /// Initializes a wrapper server with an explicit time seam and a per-session logic factory. Intended
        /// for deterministic tests.
        /// </summary>
        internal RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            Func<IRawReliableWrapperLogic> logicFactory, short version, int handshakeMaxByteSize,
            TimeSpan? rejectLinger, ITimerFactory? timerFactory)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, logicFactory,
                version, handshakeMaxByteSize, rejectLinger, timerFactory)
        {
        }

        private RawReliableWrapperServer(string typeName, string negotiationName, IRawReliableServer inner,
            ILogger logger, IMemoryRental memory, Func<IRawReliableWrapperLogic>? logicFactory, short version,
            int handshakeMaxByteSize, TimeSpan? rejectLinger, ITimerFactory? timerFactory)
            : base(typeName, logger, memory, new RawReliableConformanceControl())
        {
            if (negotiationName == null)
            {
                throw new ArgumentNullException(nameof(negotiationName));
            }

            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }

            _inner = inner;
            _version = version;
            _negotiationName = negotiationName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _rejectLinger = rejectLinger ?? RawReliableNegotiation.DefaultRejectLinger;
            _timerFactory = timerFactory ?? SystemTimerFactory.Instance;
            _logicFactory = logicFactory;
        }

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RawReliable;

        /// <inheritdoc/>
        public override int MessageMaxByteSize => _inner.MessageMaxByteSize;

        /// <summary>
        /// Sets the layer logic once, before the transport is started.
        /// </summary>
        /// <remarks>
        /// The supplied instance is shared by every accepted session. Use the logic-factory constructor when
        /// the logic owns session-scoped state.
        /// </remarks>
        protected void SetupLogic(IRawReliableWrapperLogic logic)
        {
            if (logic == null)
            {
                throw new ArgumentNullException(nameof(logic));
            }

            var factory = CreateSharedFactory(logic);

            lock (_locker)
            {
                if (HasStartBeenAttempted)
                {
                    throw new InvalidOperationException("The wrapper logic must be set before start.");
                }

                _logicFactory = factory;
            }
        }

        private static Func<IRawReliableWrapperLogic> CreateSharedFactory(IRawReliableWrapperLogic logic)
        {
            if (logic == null)
            {
                throw new ArgumentNullException(nameof(logic));
            }

            return () => logic;
        }

        /// <inheritdoc/>
        public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (_locker)
            {
                if (!IsValid || HasStartBeenAttempted || _initialized)
                {
                    return false;
                }

                _factory = factory;
                _initialized = true;
                return true;
            }
        }

        /// <inheritdoc/>
        protected override bool TryStart()
        {
            if (!_initialized || _logicFactory == null)
            {
                Fail("TryStart", "Wrapper server is not initialized");
                return false;
            }

            try
            {
                if (!_inner.Init(new InnerFactory(this)))
                {
                    return false;
                }

                return _inner.Start(OnInnerStopped);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void OnStarted()
        {
            // The inner transport owns connection establishment.
        }

        /// <inheritdoc/>
        protected override void OnStopped(StopReason reason)
        {
            try
            {
                _inner.Stop(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name}<{_inner}>";
        }

        private void OnInnerStopped(StopReason reason)
        {
            if (IsStarted)
            {
                Fail(reason, "Unexpected underlying transport stop");
            }
        }

        private sealed class InnerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
        {
            private readonly RawReliableWrapperServer _wrapper;

            public InnerFactory(RawReliableWrapperServer wrapper)
            {
                _wrapper = wrapper;
            }

            public IRawReliableServerHandler? TryCreate(IEndPoint remote)
            {
                var logicFactory = _wrapper._logicFactory;
                var factory = _wrapper._factory;
                if (logicFactory == null || factory == null || !_wrapper.IsStarted)
                {
                    return null;
                }

                IRawReliableWrapperLogic? logic;
                try
                {
                    logic = logicFactory();
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    return null;
                }

                if (logic == null)
                {
                    _wrapper.Log.e("The wrapper logic factory returned null");
                    return null;
                }

                return new ServerSession(_wrapper, logic, factory, remote);
            }
        }

        private sealed class ServerSession : IRawReliableServerHandler, IRawReliableEndpoint
        {
            private readonly RawReliableWrapperServer _wrapper;
            private readonly IRawReliableWrapperLogic _logic;
            private readonly IRawReliableServerHandlerFactory<IRawReliableServerHandler> _factory;
            private readonly IEndPoint _remote;
            private readonly object _sendLock = new object();

            private volatile IRawReliableEndpoint? _endpoint;
            private NegotiationSession? _negotiation;
            private IRawReliableServerHandler? _userHandler;

            private int _admitted;
            private int _disconnectedRaised;
            private int _terminalRaised;

            public ServerSession(RawReliableWrapperServer wrapper, IRawReliableWrapperLogic logic,
                IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory, IEndPoint remote)
            {
                _wrapper = wrapper;
                _logic = logic;
                _factory = factory;
                _remote = remote;
            }

            void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;

                var negotiation = new NegotiationSession(
                    NegotiationRole.Server,
                    _wrapper._version,
                    _wrapper._negotiationName,
                    SendEnvelope,
                    OnRejected,
                    reason => _endpoint?.Disconnect(reason),
                    HandleAccepted,
                    createResponse: _logic.CreateNegotiationResponse,
                    checkData: CheckNegotiationData,
                    handshakeMaxByteSize: _wrapper._handshakeMaxByteSize,
                    rejectLinger: _wrapper._rejectLinger,
                    timerFactory: _wrapper._timerFactory,
                    log: _wrapper.Log);

                _negotiation = negotiation;
                negotiation.Start();
            }

            void IRawHandler.OnReceived(UnionDataList receivedBuffer)
            {
                var negotiation = _negotiation;
                if (Volatile.Read(ref _admitted) == 0)
                {
                    if (negotiation != null && negotiation.OnReceived(receivedBuffer))
                    {
                        return;
                    }

                    receivedBuffer.Release();
                    negotiation?.Reject(new ProtocolMismatch(_wrapper.Name,
                        "expected a wrapper negotiation request"), "expected a negotiation request");
                    return;
                }

                try
                {
                    if (_logic.ProcessReceivedData(receivedBuffer))
                    {
                        _userHandler?.OnReceived(receivedBuffer.Acquire());
                        return;
                    }

                    _endpoint?.Disconnect(new TextFail(_wrapper.Name,
                        "failed to process received wrapper data"));
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    _endpoint?.Disconnect(new ExceptionFail(_wrapper.Name, ex,
                        "failed to process received wrapper data"));
                }
                finally
                {
                    receivedBuffer.Release();
                }
            }

            void IRawReliableHandler.OnDisconnected(StopReason reason)
            {
                if (Volatile.Read(ref _admitted) != 0)
                {
                    RaiseDisconnected(reason);
                }
            }

            void IRawHandler.OnStopped(StopReason reason)
            {
                RaiseTerminal(reason);
            }

            IEndPoint? IRawEndpoint.RemoteEndPoint => _endpoint?.RemoteEndPoint ?? _remote;

            bool IRawReliableEndpoint.IsConnected
            {
                get
                {
                    var endpoint = _endpoint;
                    return Volatile.Read(ref _admitted) != 0 && endpoint != null && endpoint.IsConnected;
                }
            }

            int IRawEndpoint.MessageMaxByteSize
            {
                get
                {
                    var endpoint = _endpoint;
                    return endpoint?.MessageMaxByteSize ?? _wrapper._inner.MessageMaxByteSize;
                }
            }

            SendResult IRawEndpoint.Send(UnionDataList bufferToSend)
            {
                if (bufferToSend == null)
                {
                    return SendResult.InvalidMessage;
                }

                if (Volatile.Read(ref _admitted) == 0)
                {
                    bufferToSend.Release();
                    return SendResult.NotConnected;
                }

                lock (_sendLock)
                {
                    try
                    {
                        var endpoint = _endpoint;
                        if (endpoint == null)
                        {
                            return SendResult.NotConnected;
                        }

                        if (!_logic.ProcessSentData(bufferToSend))
                        {
                            return SendResult.Error;
                        }

                        return endpoint.Send(bufferToSend.Acquire());
                    }
                    catch (Exception ex)
                    {
                        _wrapper.Log.wtf(ex);
                        return SendResult.Error;
                    }
                    finally
                    {
                        bufferToSend.Release();
                    }
                }
            }

            bool IRawReliableEndpoint.Disconnect(StopReason reason)
            {
                var endpoint = _endpoint;
                if (endpoint == null)
                {
                    return false;
                }

                return endpoint.Disconnect(reason);
            }

            void IBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
            {
                _logic.GetControls(dst, predicate);
            }

            private bool CheckNegotiationData(UnionDataList customTail)
            {
                bool accepted;
                try
                {
                    accepted = _logic.CheckNegotiationData(customTail);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    return false;
                }

                if (!accepted)
                {
                    return false;
                }

                IRawReliableServerHandler? handler;
                lock (_wrapper._factoryLock)
                {
                    try
                    {
                        handler = _factory.TryCreate(_remote);
                    }
                    catch (Exception ex)
                    {
                        _wrapper.Log.wtf(ex);
                        handler = null;
                    }
                }

                if (handler == null)
                {
                    return false;
                }

                _userHandler = new StopOnceServerHandler(handler);
                return true;
            }

            private void OnRejected(StopReason reason)
            {
                var userHandler = _userHandler;
                if (userHandler == null)
                {
                    return;
                }

                try
                {
                    userHandler.OnStopped(reason);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }
            }

            private void HandleAccepted(UnionDataList customTail)
            {
                _ = customTail;
                Volatile.Write(ref _admitted, 1);

                try
                {
                    _logic.OnConnected();
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    _endpoint?.Disconnect(new ExceptionFail(_wrapper.Name, ex, "wrapper OnConnected failed"));
                    return;
                }

                var userHandler = _userHandler;
                if (userHandler == null)
                {
                    _endpoint?.Disconnect(new TextFail(_wrapper.Name, "missing wrapper session handler"));
                    return;
                }

                try
                {
                    userHandler.OnConnected(this);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    _endpoint?.Disconnect(new ExceptionFail(_wrapper.Name, ex, "handler OnConnected failed"));
                }
            }

            private void RaiseDisconnected(StopReason reason)
            {
                if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0)
                {
                    return;
                }

                try
                {
                    _logic.OnDisconnected();
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }

                var userHandler = _userHandler;
                if (userHandler == null)
                {
                    return;
                }

                try
                {
                    userHandler.OnDisconnected(reason);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }
            }

            private void RaiseTerminal(StopReason reason)
            {
                if (Interlocked.Exchange(ref _terminalRaised, 1) != 0)
                {
                    return;
                }

                var negotiation = Interlocked.Exchange(ref _negotiation, null);
                negotiation?.Dispose();

                if (Volatile.Read(ref _admitted) != 0)
                {
                    RaiseDisconnected(reason);
                }

                var userHandler = _userHandler;
                if (userHandler == null)
                {
                    return;
                }

                try
                {
                    userHandler.OnStopped(reason);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }
            }

            private SendResult SendEnvelope(UnionDataList envelope)
            {
                var endpoint = _endpoint;
                if (endpoint == null)
                {
                    envelope.Release();
                    return SendResult.NotConnected;
                }

                try
                {
                    return endpoint.Send(envelope);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                    return SendResult.Error;
                }
            }
        }
    }
}
