using System;
using System.Threading;
using Actuarius.Memory;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    /// <summary>
    /// A reliable server that keeps sessions alive across physical reconnections. Every inner connection
    /// negotiates the <c>"reconnectable"</c> dataset; a valid session id and secret reattach the connection
    /// to the existing <see cref="ReconnectableServerLogic"/> and its delivery system.
    /// </summary>
    public sealed class RawReliableReconnectableServer : RawTransport, IRawReliableServer
    {
        private readonly IRawReliableServer _coreTransport;
        private readonly TimeSpan _disconnectTimeout;
        private readonly short _negotiationVersion;
        private readonly string _negotiationName;
        private readonly int _handshakeMaxByteSize;
        private readonly INegotiationClock _clock;
        private readonly Func<ILogicDriver<IPeriodicLogicDriverCtl>>? _sessionsDriverFactory;
        private readonly object _factoryLock = new object();

        private readonly SessionMap<ReconnectableServerLogic> _sessionsMap =
            new SessionMap<ReconnectableServerLogic>(ReconnectableInfo.ServerConnectionsLimit);

        private IRawReliableServerHandlerFactory<IRawReliableServerHandler>? _userFactory;
        private ILogicDriver<IPeriodicLogicDriverCtl>? _sessionsLogicDriver;
        private bool _initialized;

        public RawReliableReconnectableServer(
            IRawReliableServer coreTransport,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental)
            : this(coreTransport, disconnectTimeout, logger, memoryRental,
                ReconnectableInfo.NegotiationVersion, null, ReconnectableInfo.DefaultHandshakeMaxByteSize,
                null, null)
        {
        }

        internal RawReliableReconnectableServer(
            IRawReliableServer coreTransport,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental,
            short negotiationVersion,
            string? negotiationName,
            int handshakeMaxByteSize,
            INegotiationClock? clock,
            Func<ILogicDriver<IPeriodicLogicDriverCtl>>? sessionsDriverFactory)
            : base(ReconnectableInfo.TransportName, logger, memoryRental, new RawReliableConformanceControl())
        {
            _coreTransport = coreTransport ?? throw new ArgumentNullException(nameof(coreTransport));
            _disconnectTimeout = disconnectTimeout;
            _negotiationVersion = negotiationVersion;
            _negotiationName = negotiationName ?? ReconnectableInfo.TransportName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _clock = clock ?? NegotiationClock.Shared;
            _sessionsDriverFactory = sessionsDriverFactory;
        }

        public override TransportType Type => TransportType.RawReliable;

        public override int MessageMaxByteSize => _coreTransport.MessageMaxByteSize;

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

                _userFactory = factory;
                _initialized = true;
                return true;
            }
        }

        protected override bool TryStart()
        {
            if (!_initialized || _userFactory == null)
            {
                Fail("TryStart", "The reconnectable server is not initialized");
                return false;
            }

            _sessionsLogicDriver = _sessionsDriverFactory != null
                ? _sessionsDriverFactory()
                : new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance,
                    TimeSpan.FromMilliseconds(20));

            if (!_coreTransport.Init(new InnerFactory(this)))
            {
                return false;
            }

            return _coreTransport.Start(reason =>
            {
                if (IsStarted)
                {
                    Fail(reason, "Unexpected underlying transport stop");
                }
            });
        }

        protected override void OnStarted()
        {
            // The core transport owns connection establishment.
        }

        protected override void OnStopped(StopReason reason)
        {
            try
            {
                _coreTransport.Stop(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            var driver = _sessionsLogicDriver;
            _sessionsLogicDriver = null;
            driver?.Finish().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Log.wtf(t.Exception);
                }
            });
        }

        public override string ToString()
        {
            return "reconnectable-server";
        }

        private IRawReliableServerHandler? CreateUserHandler(IEndPoint remote)
        {
            var factory = _userFactory;
            if (factory == null || !IsStarted)
            {
                return null;
            }

            lock (_factoryLock)
            {
                try
                {
                    var handler = factory.TryCreate(remote);
                    return handler == null ? null : new StopOnceServerHandler(handler);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    return null;
                }
            }
        }

        private bool StartSessionLogic(ReconnectableServerLogic logic)
        {
            return _sessionsLogicDriver?.Start(logic) == LogicStartResult.Success;
        }

        private void OnSessionConnected(ReconnectableServerLogic logic, IRawReliableServerHandler handler,
            IRawReliableEndpoint endPoint)
        {
            try
            {
                handler.OnConnected(endPoint);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                _sessionsMap.RemoveSession(logic.Id);
                logic.ReleaseNegotiationResponse();
                try
                {
                    handler.OnStopped(new ExceptionFail(Name, ex, "Session OnConnected failed"));
                }
                catch (Exception stopEx)
                {
                    Log.wtf(stopEx);
                }
            }
        }

        private void OnSessionStopped(ReconnectableServerLogic logic, IRawReliableServerHandler handler,
            StopReason reason)
        {
            _sessionsMap.RemoveSession(logic.Id);

            try
            {
                handler.OnDisconnected(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            try
            {
                handler.OnStopped(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            logic.ReleaseNegotiationResponse();
        }

        private sealed class InnerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
        {
            private readonly RawReliableReconnectableServer _server;

            public InnerFactory(RawReliableReconnectableServer server)
            {
                _server = server;
            }

            public IRawReliableServerHandler? TryCreate(IEndPoint remote)
            {
                if (!_server.IsStarted)
                {
                    return null;
                }

                return new InnerSession(_server, remote);
            }
        }

        /// <summary>
        /// One physical connection. It runs the reconnectable negotiation and then forwards application
        /// traffic to the persistent session logic selected by the request tail.
        /// </summary>
        private sealed class InnerSession : IRawReliableServerHandler
        {
            private readonly RawReliableReconnectableServer _server;
            private readonly IEndPoint _remote;

            private IRawReliableEndpoint? _endpoint;
            private NegotiationSession? _negotiation;
            private ReconnectableServerLogic? _logic;
            private IRawReliableServerHandler? _userHandler;
            private UnionDataList? _preparedResponse;
            private bool _newSession;
            private volatile bool _admitted;
            private int _released;

            public InnerSession(RawReliableReconnectableServer server, IEndPoint remote)
            {
                _server = server;
                _remote = remote;
            }

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;

                var negotiation = new NegotiationSession(
                    NegotiationRole.Server,
                    _server._negotiationVersion,
                    _server._negotiationName,
                    SendEnvelope,
                    OnRejected,
                    reason => _endpoint?.Disconnect(reason),
                    HandleAccepted,
                    createResponse: TakeResponse,
                    checkData: CheckNegotiationData,
                    handshakeMaxByteSize: _server._handshakeMaxByteSize,
                    clock: _server._clock,
                    log: _server.Log);

                _negotiation = negotiation;
                negotiation.Start();
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                if (_admitted)
                {
                    var logic = _logic;
                    if (logic != null)
                    {
                        ((IRawReliableHandler)logic).OnReceived(receivedBuffer);
                        return;
                    }

                    receivedBuffer.Release();
                    return;
                }

                var negotiation = _negotiation;
                if (negotiation != null && negotiation.OnReceived(receivedBuffer))
                {
                    return;
                }

                receivedBuffer.Release();
                negotiation?.Reject(new ProtocolMismatch(_server.Name,
                    "expected a reconnectable negotiation request"), "expected a negotiation request");
            }

            public void OnDisconnected(StopReason reason)
            {
                if (_admitted)
                {
                    _logic?.OnDisconnected(reason);
                }
            }

            public void OnStopped(StopReason reason)
            {
                var negotiation = _negotiation;
                _negotiation = null;
                negotiation?.Dispose();

                if (_admitted)
                {
                    var logic = _logic;
                    if (logic != null)
                    {
                        ((IRawHandler)logic).OnStopped(reason);
                    }
                }
                else
                {
                    ReleaseUnadmitted(reason);
                }
            }

            private bool CheckNegotiationData(UnionDataList customTail)
            {
                if (!customTail.TryPopFirst(out int id) ||
                    !customTail.TryPopFirst(out int generation) ||
                    !customTail.TryPopFirst(out IMultiRefReadOnlyByteArray? secret))
                {
                    return false;
                }

                using var secretDisposer = secret.AsDisposable();

                if (customTail.Elements.Count != 0)
                {
                    return false;
                }

                var sessionId = new SessionId(id, generation);
                if (sessionId.IsValid)
                {
                    var logic = _server._sessionsMap.Find(sessionId);
                    if (logic == null)
                    {
                        _server.Log.w("Session[{0}] not found", sessionId);
                        return false;
                    }

                    // The response clone must exist before the session is claimed: a release race would
                    // otherwise leave the reattached connection without an answer.
                    var clonedResponse = logic.CloneNegotiationResponse();
                    if (clonedResponse == null)
                    {
                        _server.Log.w("Session[{0}] has no negotiation response to reattach with",
                            sessionId);
                        return false;
                    }

                    if (!logic.Reattach(secret.Acquire()))
                    {
                        clonedResponse.Release();
                        _server.Log.w("Session[{0}] rejected user reconnection", sessionId);
                        return false;
                    }

                    _logic = logic;
                    _newSession = false;
                    _preparedResponse = clonedResponse;
                    return true;
                }

                var userHandler = _server.CreateUserHandler(_remote);
                if (userHandler == null)
                {
                    return false;
                }

                var newLogic = new ReconnectableServerLogic(userHandler, _server._disconnectTimeout,
                    _server.Log, _server.Memory);
                newLogic.OnConnected += endPoint =>
                    _server.OnSessionConnected(newLogic, userHandler, endPoint);
                newLogic.OnStopped += reason =>
                    _server.OnSessionStopped(newLogic, userHandler, reason);

                var assignedId = _server._sessionsMap.AddSession(newLogic);
                if (!assignedId.IsValid)
                {
                    NotifyUserHandlerStopped(userHandler, "Session map is full");
                    return false;
                }

                newLogic.Attach(assignedId);

                UnionDataList response;
                try
                {
                    response = newLogic.CreateNegotiationResponse();
                }
                catch (Exception ex)
                {
                    _server.Log.wtf(ex);
                    _server._sessionsMap.RemoveSession(newLogic.Id);
                    newLogic.ReleaseNegotiationResponse();
                    NotifyUserHandlerStopped(userHandler, "Failed to compose the negotiation response");
                    return false;
                }

                if (!_server.StartSessionLogic(newLogic))
                {
                    _server._sessionsMap.RemoveSession(newLogic.Id);
                    newLogic.ReleaseNegotiationResponse();
                    response.Release();
                    NotifyUserHandlerStopped(userHandler, "Failed to start the session logic");
                    return false;
                }

                _logic = newLogic;
                _userHandler = userHandler;
                _newSession = true;
                _preparedResponse = response;
                return true;
            }

            private UnionDataList TakeResponse()
            {
                var response = _preparedResponse;
                _preparedResponse = null;
                return response ?? MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            }

            private void HandleAccepted(UnionDataList customTail)
            {
                _ = customTail;
                _admitted = true;

                var logic = _logic;
                if (logic == null)
                {
                    _endpoint?.Disconnect(new TextFail(_server.Name,
                        "The reconnectable session was not prepared"));
                    return;
                }

                ((IRawReliableServerHandler)logic).OnConnected(_endpoint!);
            }

            private void OnRejected(StopReason reason)
            {
                if (_newSession && !_admitted && _logic != null)
                {
                    ReleaseUnadmitted(reason);
                }
            }

            private void ReleaseUnadmitted(StopReason reason)
            {
                if (Interlocked.Exchange(ref _released, 1) != 0)
                {
                    return;
                }

                var logic = _logic;
                if (logic != null)
                {
                    _server._sessionsMap.RemoveSession(logic.Id);
                    logic.ReleaseNegotiationResponse();

                    // Stops the session logic driver when it was already started, which raises the
                    // session's terminal event and notifies the user handler through the stop-once gate.
                    ((IRawReliableEndpoint)logic).Disconnect(reason);
                }

                _preparedResponse?.Release();
                _preparedResponse = null;

                var userHandler = _userHandler;
                if (userHandler != null)
                {
                    try
                    {
                        userHandler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        _server.Log.wtf(ex);
                    }
                }
            }

            private void NotifyUserHandlerStopped(IRawReliableServerHandler userHandler, string text)
            {
                try
                {
                    userHandler.OnStopped(new TextFail(_server.Name, text));
                }
                catch (Exception ex)
                {
                    _server.Log.wtf(ex);
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
                    _server.Log.wtf(ex);
                    return SendResult.Error;
                }
            }
        }
    }
}
