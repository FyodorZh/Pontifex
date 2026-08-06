using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Converters
{
    internal sealed class RawUnreliableNoAckToRawReliableAckServer : AnyTransport, IRawReliableAckServer
    {
        private readonly IRawUnreliableNoAckServer _inner;
        private readonly ConcurrentDictionary<IEndPoint, ServerSession> _sessions = new ConcurrentDictionary<IEndPoint, ServerSession>();

        private IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? _acknowledger;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);

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
                        endpoint.UnreliableSend(data);
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

            public void OnStopped(StopReason reason) => _session.Disconnect(reason);
        }

        private sealed class ServerEndpoint : IRawReliableEndpoint
        {
            private readonly Delivery.DeliveryManager _dm;
            private readonly IEndPoint _remoteEndPoint;
            private volatile bool _disconnected;

            public ServerEndpoint(Delivery.DeliveryManager dm, IEndPoint remoteEndPoint)
            {
                _dm = dm;
                _remoteEndPoint = remoteEndPoint;
            }

            public IEndPoint? RemoteEndPoint => _remoteEndPoint;
            public bool IsConnected => !_disconnected;
            public int MessageMaxByteSize => _dm.DeliveryMaxByteSize;

            public SendResult Send(UnionDataList bufferToSend)
            {
                if (_disconnected)
                {
                    bufferToSend.Release();
                    return SendResult.NotConnected;
                }
                return _dm.ScheduleDelivery(bufferToSend, out _);
            }

            public bool Disconnect(StopReason reason)
            {
                _disconnected = true;
                return true;
            }

            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }
        }

        private sealed class ServerSession
        {
            private readonly RawUnreliableNoAckToRawReliableAckServer _owner;
            private readonly Delivery.DeliveryManager _dm;
            private readonly Delivery.RetryDeliveryScheduler _scheduler;
            private readonly ServerEndpoint _endpoint;
            private readonly SessionOutgoingConsumer _consumer;
            private readonly IRawReliableAckServerHandler _handler;
            private readonly IEndPoint _remoteEndPoint;
            private readonly ManualResetEventSlim _endpointReady = new ManualResetEventSlim(false);
            private volatile IRawUnreliableEndpoint? _unreliableEndpoint;
            private int _disconnectInitiated;

            private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();

            public IEndPoint RemoteEndPoint => _remoteEndPoint;
            public ServerEndpoint Endpoint => _endpoint;
            public IRawUnreliableEndpoint? UnreliableEndpoint => _unreliableEndpoint;

            public ServerSession(
                RawUnreliableNoAckToRawReliableAckServer owner,
                IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger,
                IEndPoint remoteEndPoint)
            {
                _owner = owner;
                _remoteEndPoint = remoteEndPoint;

                var ackData = owner.Memory.CollectablePool.Acquire<UnionDataList>();
                _handler = acknowledger.TryAck(ackData) ?? throw new InvalidOperationException("Acknowledger rejected client");
                _handler = _handler.Test(text => owner.Log.e(text)).GetSafe(e => owner.Log.wtf(e));

                _dm = new Delivery.DeliveryManager(
                    owner._inner.MessageMaxByteSize,
                    owner.Memory.ByteArraysPool,
                    owner.Memory.CollectablePool);

                _scheduler = new Delivery.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
                _endpoint = new ServerEndpoint(_dm, remoteEndPoint);
                _consumer = new SessionOutgoingConsumer(this);

                _dm.Received += OnDmReceived;
                _dm.Delivered += OnDmDelivered;
                _dm.FailedToDeliver += OnDmFailedToDeliver;

                var ackResponse = owner.Memory.CollectablePool.Acquire<UnionDataList>();
                _handler.FillAckResponse(ackResponse);

                _handler.OnConnected(_endpoint);
            }

            public void SetEndpoint(IRawUnreliableEndpoint endpoint)
            {
                _unreliableEndpoint = endpoint;
                _endpointReady.Set();
            }

            public IRawUnreliableHandler CreateInnerHandler() => new SessionInnerHandler(this);

            public void EnqueueIncoming(UnionDataList data)
            {
                _incomingQueue.Enqueue(data);
            }

            public void Tick()
            {
                if (!_endpointReady.IsSet)
                {
                    _endpointReady.Wait(TimeSpan.FromSeconds(5));
                }

                while (_incomingQueue.TryDequeue(out var data))
                {
                    _dm.ProcessIncoming(data);
                }

                _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _consumer);
                _consumer.Flush();
            }

            public void Disconnect(StopReason reason)
            {
                if (Interlocked.CompareExchange(ref _disconnectInitiated, 1, 0) != 0) return;

                _dm.Clear();
                _endpoint.Disconnect(reason);

                try { _handler.OnDisconnected(reason); }
                catch (Exception ex) { _owner.Log.wtf(ex); }
            }

            private void OnDmReceived(DeliveryId id, UnionDataList data)
            {
                try
                {
                    _handler.OnReceived(data);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                }
            }

            private void OnDmDelivered(DeliveryId id)
            {
            }

            private void OnDmFailedToDeliver(DeliveryId id)
            {
                _owner.Log.e("Delivery to {0} failed: {1}", _remoteEndPoint, id);
            }
        }

        public RawUnreliableNoAckToRawReliableAckServer(
            IRawUnreliableNoAckServer inner,
            IMemoryRental? memoryOverride,
            ILogger? loggerOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;
        }

        public override TransportType Type => TransportType.RawReliableAck;

        public int MessageMaxByteSize => _inner.MessageMaxByteSize;

        public bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            _acknowledger = acknowledger;
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
                Name = "RawUnreliableNoAckToRawReliableAckServer.DM"
            };
            _dmThread.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            _stopped = true;
            _workEvent.Set();

            _dmThread?.Join(TimeSpan.FromSeconds(3));

            foreach (var session in _sessions.Values)
            {
                session.Disconnect(reason);
            }
            _sessions.Clear();

            _inner.Stop(reason);
        }

        private IRawUnreliableHandler? HandleNewSource(IEndPoint source)
        {
            var acknowledger = _acknowledger;
            if (acknowledger == null)
            {
                return null;
            }

            ServerSession session;
            try
            {
                session = new ServerSession(this, acknowledger, source);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return null;
            }

            if (!_sessions.TryAdd(source, session))
            {
                session.Disconnect(new Unknown(Name));
                return null;
            }

            return session.CreateInnerHandler();
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
        }

        public override string ToString()
        {
            return $"{Name}<dm-server>";
        }
    }
}
