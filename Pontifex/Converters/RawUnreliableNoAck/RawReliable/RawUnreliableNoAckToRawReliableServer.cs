using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;
using Status = Pontifex.Raw.Reliable.RawReliableHandshake.HandshakeStatus;

namespace Pontifex.Converters
{
    /// <summary>
    /// Converts a raw-unreliable-noack server into a raw-reliable server. Each inner source route gets a
    /// delivery-managed session whose first message is the converter handshake; the application factory is
    /// invoked only after that request is validated, and the session handler is notified only after the
    /// response has been sent.
    /// </summary>
    internal sealed class RawUnreliableNoAckToRawReliableServer : AnyTransport, IRawReliableServer
    {
        private readonly IRawUnreliableNoAckServer _inner;
        private readonly ConcurrentDictionary<IEndPoint, ServerSession> _sessions =
            new ConcurrentDictionary<IEndPoint, ServerSession>();

        private IRawReliableServerHandlerFactory<IRawReliableServerHandler>? _factory;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _loopExited = new ManualResetEventSlim(false);

        private sealed class SessionOutgoingConsumer : IConsumer<UnionDataList>
        {
            private readonly ServerSession _session;
            private readonly List<UnionDataList> _pending = new List<UnionDataList>();

            public SessionOutgoingConsumer(ServerSession session) => _session = session;

            public bool Put(UnionDataList data)
            {
                _pending.Add(data);
                return true;
            }

            public void Flush()
            {
                foreach (var data in _pending)
                {
                    var endpoint = _session.UnreliableEndpoint;
                    if (endpoint != null)
                    {
                        endpoint.Send(data);
                    }
                    else
                    {
                        data.Release();
                    }
                }
                _pending.Clear();
            }
        }

        private sealed class SessionInnerHandler : IRawUnreliableHandler
        {
            private readonly ServerSession _session;

            public SessionInnerHandler(ServerSession session) => _session = session;

            public void OnStarted(IRawUnreliableEndpoint endpoint) => _session.SetEndpoint(endpoint);

            public void OnReceived(UnionDataList data) => _session.EnqueueIncoming(data);

            public void OnStopped(StopReason reason) => _session.RequestDisconnect(reason);
        }

        private sealed class ServerEndpoint : IRawReliableEndpoint
        {
            private readonly Delivery.DeliveryManager _dm;
            private readonly IEndPoint _remoteEndPoint;
            private readonly ServerSession _session;
            private int _messageMaxByteSize;
            private volatile bool _accepted;
            private volatile bool _disconnected;

            public ServerEndpoint(Delivery.DeliveryManager dm, IEndPoint remoteEndPoint, ServerSession session)
            {
                _dm = dm;
                _remoteEndPoint = remoteEndPoint;
                _session = session;
                _messageMaxByteSize = dm.DeliveryMaxByteSize;
            }

            public IEndPoint? RemoteEndPoint => _remoteEndPoint;
            public bool IsConnected => _accepted && !_disconnected;
            public int MessageMaxByteSize => Volatile.Read(ref _messageMaxByteSize);
            public bool WasAccepted => _accepted;

            public void SetAccepted(int messageMaxByteSize)
            {
                Volatile.Write(ref _messageMaxByteSize, messageMaxByteSize);
                _accepted = true;
            }

            public void MarkDisconnected()
            {
                _disconnected = true;
            }

            public SendResult Send(UnionDataList bufferToSend)
            {
                if (_disconnected || !_accepted)
                {
                    bufferToSend.Release();
                    return SendResult.NotConnected;
                }

                if (bufferToSend.GetDataSize() > MessageMaxByteSize)
                {
                    bufferToSend.Release();
                    return SendResult.MessageTooBig;
                }

                return _dm.ScheduleDelivery(bufferToSend, out _);
            }

            public bool Disconnect(StopReason reason)
            {
                _disconnected = true;
                _session.RequestDisconnect(reason);
                return true;
            }

            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }
        }

        private sealed class ServerSession
        {
            private readonly RawUnreliableNoAckToRawReliableServer _owner;
            private readonly Delivery.DeliveryManager _dm;
            private readonly Delivery.RetryDeliveryScheduler _scheduler;
            private readonly ServerEndpoint _endpoint;
            private readonly SessionOutgoingConsumer _consumer;
            private readonly IEndPoint _remoteEndPoint;
            private readonly ManualResetEventSlim _endpointReady = new ManualResetEventSlim(false);
            private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();
            private readonly ConcurrentQueue<UnionDataList> _deferredIncoming = new ConcurrentQueue<UnionDataList>();

            private volatile IRawUnreliableEndpoint? _unreliableEndpoint;
            private IRawReliableServerHandler? _handler;
            private int _accepted;
            private int _handlerNotified;
            private int _handshakeHandled;
            private int _pendingCleanup;
            private int _terminal;
            private int _connected;
            private volatile bool _stopRequested;
            private volatile StopReason? _pendingStopReason;
            private StopReason? _cleanupReason;
            private int _agreedMax;

            public IEndPoint RemoteEndPoint => _remoteEndPoint;
            public ServerEndpoint Endpoint => _endpoint;
            public IRawUnreliableEndpoint? UnreliableEndpoint => _unreliableEndpoint;

            public ServerSession(
                RawUnreliableNoAckToRawReliableServer owner,
                IEndPoint remoteEndPoint)
            {
                _owner = owner;
                _remoteEndPoint = remoteEndPoint;

                _dm = new Delivery.DeliveryManager(
                    owner._inner.MessageMaxByteSize,
                    owner.Memory.ByteArraysPool,
                    owner.Memory.CollectablePool);

                _scheduler = new Delivery.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
                _endpoint = new ServerEndpoint(_dm, remoteEndPoint, this);
                _consumer = new SessionOutgoingConsumer(this);

                _dm.Received += OnDmReceived;
                _dm.Delivered += OnDmDelivered;
                _dm.FailedToDeliver += OnDmFailedToDeliver;
            }

            public void SetEndpoint(IRawUnreliableEndpoint endpoint)
            {
                _unreliableEndpoint = endpoint;
                _endpointReady.Set();
            }

            public IRawUnreliableHandler CreateInnerHandler()
            {
                return new SessionInnerHandler(this);
            }

            public void EnqueueIncoming(UnionDataList data)
            {
                if (Volatile.Read(ref _terminal) != 0)
                {
                    data.Release();
                    return;
                }

                _incomingQueue.Enqueue(data);
                _owner._workEvent.Set();
            }

            public void RequestDisconnect(StopReason reason)
            {
                _pendingStopReason = reason;
                _stopRequested = true;
                _owner._workEvent.Set();
            }

            public void Tick()
            {
                if (Volatile.Read(ref _terminal) != 0)
                {
                    return;
                }

                if (!_endpointReady.IsSet)
                {
                    _endpointReady.Wait(TimeSpan.FromSeconds(5));
                }

                while (_incomingQueue.TryDequeue(out var data))
                {
                    _dm.ProcessIncoming(data);

                    if (Volatile.Read(ref _pendingCleanup) != 0 ||
                        (Volatile.Read(ref _accepted) != 0 && Volatile.Read(ref _handlerNotified) == 0))
                    {
                        break;
                    }
                }

                _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _consumer);
                _consumer.Flush();

                if (Volatile.Read(ref _accepted) != 0 && Volatile.Read(ref _handlerNotified) == 0)
                {
                    NotifyConnected();
                }

                if (_stopRequested)
                {
                    CleanupTerminal(_pendingStopReason ?? new Unknown(_owner.Name));
                    return;
                }

                if (Volatile.Read(ref _pendingCleanup) != 0)
                {
                    CleanupTerminal(_cleanupReason ?? new HandshakeRejected(_owner.Name, "converter handshake rejected"));
                }
            }

            private void OnDmReceived(DeliveryId id, UnionDataList data)
            {
                if (Volatile.Read(ref _accepted) == 0)
                {
                    if (Interlocked.Exchange(ref _handshakeHandled, 1) == 0)
                    {
                        HandleHandshakeRequest(data);
                    }
                    else
                    {
                        data.Release();
                    }

                    return;
                }

                if (Volatile.Read(ref _handlerNotified) == 0)
                {
                    _deferredIncoming.Enqueue(data);
                    return;
                }

                Deliver(data);
            }

            private void HandleHandshakeRequest(UnionDataList data)
            {
                try
                {
                    if (!ConverterHandshake.TryParseRequest(data, out var version, out var name, out var max))
                    {
                        Reject(Status.Rejected, "malformed converter handshake request");
                        return;
                    }

                    if (version != ConverterHandshake.Version)
                    {
                        Reject(Status.VersionMismatch, ConverterHandshake.DescribeStatus(Status.VersionMismatch));
                        return;
                    }

                    if (!ConverterHandshake.NameEquals(name, ConverterHandshake.Name))
                    {
                        Reject(Status.NameMismatch, ConverterHandshake.DescribeStatus(Status.NameMismatch));
                        return;
                    }

                    if (max < 1)
                    {
                        Reject(Status.Rejected, "invalid converter handshake capacity");
                        return;
                    }

                    var factory = _owner._factory;
                    if (factory == null)
                    {
                        Reject(Status.Rejected, "converter server is not initialized");
                        return;
                    }

                    IRawReliableServerHandler? handler;
                    try
                    {
                        // All sessions are ticked by one delivery-manager thread, so factory calls are
                        // serialized and never overlap, as the handler-factory contract requires.
                        handler = factory.TryCreate(_remoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        _owner.Log.wtf(ex);
                        handler = null;
                    }

                    if (handler == null)
                    {
                        Reject(Status.Rejected, "converter server rejected the session");
                        return;
                    }

                    _handler = handler;
                    _agreedMax = Math.Min(max, _dm.DeliveryMaxByteSize);

                    var response = ConverterHandshake.BuildAccepted(_agreedMax);
                    if (_dm.ScheduleDelivery(response, out _) != SendResult.Ok)
                    {
                        _cleanupReason = new TextFail(_owner.Name, "failed to schedule the converter handshake response");
                        Volatile.Write(ref _pendingCleanup, 1);
                        return;
                    }

                    Volatile.Write(ref _accepted, 1);
                }
                finally
                {
                    data.Release();
                }
            }

            private void Reject(Status status, string? text)
            {
                var detail = text ?? ConverterHandshake.DescribeStatus(status);
                var reject = ConverterHandshake.BuildReject(
                    ConverterHandshake.Version,
                    ConverterHandshake.Name,
                    Status.Rejected,
                    detail);
                _dm.ScheduleDelivery(reject, out _);
                _cleanupReason = status == Status.VersionMismatch || status == Status.NameMismatch
                    ? new ProtocolMismatch(_owner.Name, detail)
                    : new HandshakeRejected(_owner.Name, detail);
                Volatile.Write(ref _pendingCleanup, 1);
            }

            private void NotifyConnected()
            {
                var handler = _handler;
                if (handler == null)
                {
                    _cleanupReason = new HandshakeRejected(_owner.Name, "converter server has no admitted handler");
                    Volatile.Write(ref _pendingCleanup, 1);
                    return;
                }

                Interlocked.Exchange(ref _handlerNotified, 1);
                _endpoint.SetAccepted(_agreedMax);
                Interlocked.Exchange(ref _connected, 1);

                try
                {
                    handler.OnConnected(_endpoint);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                    _cleanupReason = new ExceptionFail(_owner.Name, ex, "OnConnected threw an exception");
                    Volatile.Write(ref _pendingCleanup, 1);
                }

                while (_deferredIncoming.TryDequeue(out var data))
                {
                    Deliver(data);
                }
            }

            private void Deliver(UnionDataList data)
            {
                var handler = _handler;
                if (handler != null)
                {
                    try
                    {
                        handler.OnReceived(data);
                    }
                    catch (Exception ex)
                    {
                        _owner.Log.wtf(ex);
                    }
                }
                else
                {
                    data.Release();
                }
            }

            public void CleanupTerminal(StopReason reason)
            {
                if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0)
                {
                    return;
                }

                _dm.Clear();
                _endpoint.MarkDisconnected();

                while (_incomingQueue.TryDequeue(out var pending))
                {
                    pending.Release();
                }

                while (_deferredIncoming.TryDequeue(out var deferred))
                {
                    deferred.Release();
                }

                var handler = _handler;
                if (handler != null)
                {
                    if (Volatile.Read(ref _connected) != 0)
                    {
                        try { handler.OnDisconnected(reason); }
                        catch (Exception ex) { _owner.Log.wtf(ex); }
                    }

                    try { handler.OnStopped(reason); }
                    catch (Exception ex) { _owner.Log.wtf(ex); }
                }

                _owner.RemoveSession(_remoteEndPoint, this);
            }

            private void OnDmDelivered(DeliveryId id)
            {
            }

            private void OnDmFailedToDeliver(DeliveryId id)
            {
                _owner.Log.e("Delivery to {0} failed: {1}", _remoteEndPoint, id);
            }
        }

        public RawUnreliableNoAckToRawReliableServer(
            IRawUnreliableNoAckServer inner,
            IMemoryRental? memoryOverride,
            ILogger? loggerOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;
        }

        public override TransportType Type => TransportType.RawReliable;

        public int MessageMaxByteSize => _inner.MessageMaxByteSize;

        public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
        {
            _factory = factory;
            _inner.Init(HandleNewSource);
            return true;
        }

        protected override bool TryStart()
        {
            return _inner.Start(r =>
            {
                if (IsStarted)
                {
                    Stop(r);
                }
            });
        }

        protected override void OnStarted()
        {
            _stopped = false;
            _dmThread = new Thread(ServerDmLoop)
            {
                IsBackground = true,
                Name = "RawUnreliableNoAckToRawReliableServer.DM"
            };
            _dmThread.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            _stopped = true;
            _workEvent.Set();

            if (_dmThread != null && _dmThread != Thread.CurrentThread)
            {
                _loopExited.Wait(TimeSpan.FromSeconds(3));
            }

            var sessions = new List<ServerSession>(_sessions.Values);
            foreach (var session in sessions)
            {
                session.CleanupTerminal(reason);
            }
            _sessions.Clear();

            _inner.Stop(reason);
        }

        private IRawUnreliableHandler? HandleNewSource(IEndPoint source)
        {
            if (_stopped || _factory == null)
            {
                return null;
            }

            var session = new ServerSession(this, source);
            if (!_sessions.TryAdd(source, session))
            {
                session.CleanupTerminal(new Unknown(Name));
                return null;
            }

            return session.CreateInnerHandler();
        }

        private void RemoveSession(IEndPoint remote, ServerSession session)
        {
            if (_sessions.TryGetValue(remote, out var current) && ReferenceEquals(current, session))
            {
                _sessions.TryRemove(remote, out _);
            }
        }

        private void ServerDmLoop()
        {
            while (!_stopped)
            {
                _workEvent.WaitOne(TimeSpan.FromMilliseconds(20));
                if (_stopped) break;

                foreach (var session in _sessions.Values)
                {
                    session.Tick();
                }
            }

            _loopExited.Set();
        }

        public override string ToString()
        {
            return $"{Name}<dm-server>";
        }
    }
}
