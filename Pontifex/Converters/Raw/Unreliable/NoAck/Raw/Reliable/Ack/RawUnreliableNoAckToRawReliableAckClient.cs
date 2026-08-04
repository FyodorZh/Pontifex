using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using dm = Pontifex.DeliveryManager;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Converters
{
    internal sealed class RawUnreliableNoAckToRawReliableAckClient : AnyTransport, IRawReliableAckClient
    {
        private readonly IRawUnreliableNoAckClient _inner;
        private readonly dm.DeliveryManager _dm;
        private readonly dm.RetryDeliveryScheduler _scheduler;
        private readonly ClientEndpoint _endpoint;
        private readonly SendConsumer _sendConsumer;

        private IRawReliableAckClientHandler? _handler;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();
        private bool _connectedSignaled;

        private sealed class SendConsumer : IConsumer<UnionDataList>
        {
            private readonly IRawUnreliableNoAckClient _inner;
            public SendConsumer(IRawUnreliableNoAckClient inner) => _inner = inner;

            public bool Put(UnionDataList data)
            {
                _inner.TrySend(data);
                data.Release();
                return true;
            }
        }

        private sealed class ClientEndpoint : IRawReliableEndpoint
        {
            private readonly dm.DeliveryManager _dm;
            private readonly RawUnreliableNoAckToRawReliableAckClient _owner;
            private volatile bool _disconnected;

            public ClientEndpoint(dm.DeliveryManager dm, RawUnreliableNoAckToRawReliableAckClient owner)
            {
                _dm = dm;
                _owner = owner;
            }

            public IEndPoint? RemoteEndPoint => null;
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
                _owner.Stop(reason);
                return true;
            }

            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }
        }

        public RawUnreliableNoAckToRawReliableAckClient(
            IRawUnreliableNoAckClient inner,
            IMemoryRental? memoryOverride,
            ILogger? loggerOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;

            _dm = new dm.DeliveryManager(
                inner.MessageMaxByteSize,
                Memory.ByteArraysPool,
                Memory.CollectablePool);

            _scheduler = new dm.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
            _endpoint = new ClientEndpoint(_dm, this);
            _sendConsumer = new SendConsumer(inner);

            _dm.Received += OnDmReceived;
            _dm.Delivered += OnDmDelivered;
            _dm.FailedToDeliver += OnDmFailedToDeliver;

            _inner.OnReceived += OnInnerReceived;
        }

        public override TransportType Type => TransportType.RawReliableAck;

        public int MessageMaxByteSize => _endpoint.MessageMaxByteSize;

        public bool Init(IRawReliableAckClientHandler handler)
        {
            _handler = handler;
            return true;
        }

        protected override bool TryStart()
        {
            if (!_inner.Start(r =>
            {
                if (IsStarted)
                {
                    Stop(r);
                }
            }))
            {
                return false;
            }

            return true;
        }

        protected override void OnStarted()
        {
            _stopped = false;
            _dmThread = new Thread(DmLoop)
            {
                IsBackground = true,
                Name = "RawUnreliableNoAckToRawReliableAckClient.DM"
            };
            _dmThread.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            _stopped = true;
            _workEvent.Set();

            _dmThread?.Join(TimeSpan.FromSeconds(3));

            _dm.Clear();
            _endpoint.Disconnect(reason);

            var handler = _handler;
            if (handler != null)
            {
                try { handler.OnDisconnected(reason); }
                catch (Exception ex) { Log.wtf(ex); }

                try { handler.OnStopped(reason); }
                catch (Exception ex) { Log.wtf(ex); }
            }

            _inner.Stop(reason);
        }

        private void OnInnerReceived(UnionDataList data)
        {
            data.Acquire();
            _incomingQueue.Enqueue(data);
            _workEvent.Set();
        }

        private void DmLoop()
        {
            while (!_stopped)
            {
                _workEvent.WaitOne(TimeSpan.FromMilliseconds(20));
                if (_stopped) break;

                if (!_connectedSignaled)
                {
                    _connectedSignaled = true;
                    var handler = _handler;
                    if (handler != null)
                    {
                        try
                        {
                            var ackData = Memory.CollectablePool.Acquire<UnionDataList>();
                            handler.FillAckData(ackData);
                            _inner.TrySend(ackData);

                            var ackResponse = Memory.CollectablePool.Acquire<UnionDataList>();
                            ackResponse.PutFirst((long)7777);
                            handler.OnConnected(_endpoint, ackResponse);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }
                }

                while (_incomingQueue.TryDequeue(out var data))
                {
                    _dm.ProcessIncoming(data);
                }

                _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _sendConsumer);
            }
        }

        private void OnDmReceived(DeliveryId id, UnionDataList data)
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
                    Log.wtf(ex);
                }
            }
            else
            {
                data.Release();
            }
        }

        private void OnDmDelivered(DeliveryId id)
        {
        }

        private void OnDmFailedToDeliver(DeliveryId id)
        {
            Log.e("Delivery failed: {0}", id);
        }

        public override string ToString()
        {
            return $"{Name}<dm-client>";
        }
    }
}
