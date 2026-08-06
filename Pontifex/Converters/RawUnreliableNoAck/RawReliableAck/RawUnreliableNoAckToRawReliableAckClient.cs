using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using dm = Pontifex.DeliveryManager;
using Pontifex.Raw.Unreliable;
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
        private readonly InnerHandler _innerHandler;
        private readonly SendConsumer _sendConsumer;

        private IRawReliableAckClientHandler? _handler;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();
        private bool _connectedSignaled;

        private sealed class InnerHandler : IRawUnreliableHandler
        {
            private readonly RawUnreliableNoAckToRawReliableAckClient _owner;
            private readonly TaskCompletionSource<IRawUnreliableEndpoint> _endpointTcs =
                new TaskCompletionSource<IRawUnreliableEndpoint>(TaskCreationOptions.RunContinuationsAsynchronously);
            private volatile IRawUnreliableEndpoint? _endpoint;

            public InnerHandler(RawUnreliableNoAckToRawReliableAckClient owner) => _owner = owner;

            public IRawUnreliableEndpoint? Endpoint => _endpoint;

            public IRawUnreliableEndpoint? WaitForEndpoint(TimeSpan timeout)
            {
                if (_endpoint == null)
                {
                    _endpointTcs.Task.Wait(timeout);
                }
                return _endpoint;
            }

            public void OnStarted(IRawUnreliableEndpoint endpoint)
            {
                _endpoint = endpoint;
                _endpointTcs.TrySetResult(endpoint);
            }

            public void OnReceived(UnionDataList data)
            {
                _owner._incomingQueue.Enqueue(data);
                _owner._workEvent.Set();
            }

            public void OnStopped(StopReason reason)
            {
                _owner.Log.i("Inner client endpoint stopped: {0}", reason);
            }
        }

        private sealed class SendConsumer : IConsumer<UnionDataList>
        {
            private readonly InnerHandler _innerHandler;
            public SendConsumer(InnerHandler innerHandler) => _innerHandler = innerHandler;

            public bool Put(UnionDataList data)
            {
                var endpoint = _innerHandler.Endpoint;
                if (endpoint != null)
                {
                    endpoint.UnreliableSend(data);
                }
                else
                {
                    data.Release();
                }
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
            _innerHandler = new InnerHandler(this);
            _sendConsumer = new SendConsumer(_innerHandler);

            _dm.Received += OnDmReceived;
            _dm.Delivered += OnDmDelivered;
            _dm.FailedToDeliver += OnDmFailedToDeliver;

            _inner.Init(_innerHandler);
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
                            var endpoint = _innerHandler.WaitForEndpoint(TimeSpan.FromSeconds(5));

                            var ackData = Memory.CollectablePool.Acquire<UnionDataList>();
                            handler.FillAckData(ackData);
                            if (endpoint != null)
                            {
                                endpoint.UnreliableSend(ackData);
                            }
                            else
                            {
                                ackData.Release();
                            }

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
