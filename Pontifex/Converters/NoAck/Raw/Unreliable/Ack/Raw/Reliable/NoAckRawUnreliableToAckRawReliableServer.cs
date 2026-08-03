using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Ack.Raw.Reliable;
using dm = Pontifex.DeliveryManager;
using Pontifex.NoAck.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Converters
{
    internal sealed class NoAckRawUnreliableToAckRawReliableServer : AnyTransport, IAckRawReliableServer
    {
        private readonly INoAckRawUnreliableServer _inner;
        private readonly ConcurrentDictionary<IEndPoint, ServerSession> _sessions = new ConcurrentDictionary<IEndPoint, ServerSession>();

        private IRawServerAcknowledger<IAckRawReliableServerHandler>? _acknowledger;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ConcurrentQueue<(IEndPoint, UnionDataList)> _incomingQueue = new ConcurrentQueue<(IEndPoint, UnionDataList)>();

        private sealed class SessionOutgoingConsumer : IConsumer<UnionDataList>
        {
            private readonly INoAckRawUnreliableServer _inner;
            private readonly IEndPoint _destination;
            private readonly List<UnionDataList> _pending = new List<UnionDataList>();

            public SessionOutgoingConsumer(INoAckRawUnreliableServer inner, IEndPoint destination)
            {
                _inner = inner;
                _destination = destination;
            }

            public bool Put(UnionDataList data)
            {
                _pending.Add(data);
                return true;
            }

            public void Flush()
            {
                foreach (var data in _pending)
                {
                    _inner.TrySend(_destination, data);
                    data.Release();
                }
                _pending.Clear();
            }
        }

        private sealed class ServerEndpoint : IAckRawReliableServerSideEndpoint
        {
            private readonly dm.DeliveryManager _dm;
            private readonly IEndPoint _remoteEndPoint;
            private volatile bool _disconnected;

            public ServerEndpoint(dm.DeliveryManager dm, IEndPoint remoteEndPoint)
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
            private readonly NoAckRawUnreliableToAckRawReliableServer _owner;
            private readonly dm.DeliveryManager _dm;
            private readonly dm.RetryDeliveryScheduler _scheduler;
            private readonly ServerEndpoint _endpoint;
            private readonly SessionOutgoingConsumer _consumer;
            private readonly IAckRawReliableServerHandler _handler;
            private readonly IEndPoint _remoteEndPoint;
            private volatile bool _disconnected;

            private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();

            public IEndPoint RemoteEndPoint => _remoteEndPoint;
            public ServerEndpoint Endpoint => _endpoint;

            public ServerSession(
                NoAckRawUnreliableToAckRawReliableServer owner,
                IRawServerAcknowledger<IAckRawReliableServerHandler> acknowledger,
                IEndPoint remoteEndPoint)
            {
                _owner = owner;
                _remoteEndPoint = remoteEndPoint;

                var ackData = owner.Memory.CollectablePool.Acquire<UnionDataList>();
                _handler = acknowledger.TryAck(ackData) ?? throw new InvalidOperationException("Acknowledger rejected client");
                _handler = _handler.Test(text => owner.Log.e(text)).GetSafe(e => owner.Log.wtf(e));

                _dm = new dm.DeliveryManager(
                    owner._inner.MessageMaxByteSize,
                    owner.Memory.ByteArraysPool,
                    owner.Memory.CollectablePool);

                _scheduler = new dm.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
                _endpoint = new ServerEndpoint(_dm, remoteEndPoint);
                _consumer = new SessionOutgoingConsumer(owner._inner, remoteEndPoint);

                _dm.Received += OnDmReceived;
                _dm.Delivered += OnDmDelivered;
                _dm.FailedToDeliver += OnDmFailedToDeliver;

                var ackResponse = owner.Memory.CollectablePool.Acquire<UnionDataList>();
                _handler.FillAckResponse(ackResponse);

                _handler.OnConnected(_endpoint);
            }

            public void EnqueueIncoming(UnionDataList data)
            {
                _incomingQueue.Enqueue(data);
            }

            public void Tick()
            {
                while (_incomingQueue.TryDequeue(out var data))
                {
                    _dm.ProcessIncoming(data);
                }

                _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _consumer);
                _consumer.Flush();
            }

            public void Disconnect(StopReason reason)
            {
                if (_disconnected) return;
                _disconnected = true;

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

        public NoAckRawUnreliableToAckRawReliableServer(
            INoAckRawUnreliableServer inner,
            IMemoryRental? memoryOverride,
            ILogger? loggerOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;
            _inner.OnReceived += OnInnerReceived;
        }

        public override TransportType Type => TransportType.AckRawReliable;

        public int MessageMaxByteSize => _inner.MessageMaxByteSize;

        public bool Init(IRawServerAcknowledger<IAckRawReliableServerHandler> acknowledger)
        {
            _acknowledger = acknowledger;
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
                Name = "NoAckRawUnreliableToAckRawReliableServer.DM"
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

        private void OnInnerReceived(IEndPoint sender, UnionDataList data)
        {
            data.Acquire();
            _incomingQueue.Enqueue((sender, data));
            _workEvent.Set();
        }

        private void ServerDmLoop()
        {
            while (!_stopped)
            {
                _workEvent.WaitOne(TimeSpan.FromMilliseconds(20));
                if (_stopped) break;

                while (_incomingQueue.TryDequeue(out var item))
                {
                    var (sender, data) = item;

                    if (!_sessions.TryGetValue(sender, out var session))
                    {
                        var acknowledger = _acknowledger;
                        if (acknowledger == null) continue;

                        try
                        {
                            session = new ServerSession(this, acknowledger, sender);
                            if (!_sessions.TryAdd(sender, session))
                            {
                                session.Disconnect(new Unknown(Name));
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    session.EnqueueIncoming(data);
                }

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
