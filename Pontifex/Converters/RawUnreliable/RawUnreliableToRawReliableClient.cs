using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;
using Status = Pontifex.Raw.Reliable.RawReliableHandshake.HandshakeStatus;

namespace Pontifex.Converters
{
    /// <summary>
    /// Converts a raw-unreliable client into a raw-reliable client by emulating delivery over a
    /// <c>DeliveryManager</c>. Before the application handler observes a logical connection, the converter
    /// performs its own handshake as the first delivery-managed message.
    /// </summary>
    internal sealed class RawUnreliableToRawReliableClient : AnyTransport, IRawReliableClient
    {
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

        private readonly IRawUnreliableClient _inner;
        private readonly Delivery.DeliveryManager _dm;
        private readonly Delivery.RetryDeliveryScheduler _scheduler;
        private readonly ClientEndpoint _endpoint;
        private readonly InnerHandler _innerHandler;
        private readonly SendConsumer _sendConsumer;

        private IRawReliableClientHandler? _handler;
        private Thread? _dmThread;
        private volatile bool _stopped;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _loopExited = new ManualResetEventSlim(false);
        private readonly ConcurrentQueue<UnionDataList> _incomingQueue = new ConcurrentQueue<UnionDataList>();

        private bool _handshakeSent;
        private bool _handshakeCompleted;
        private DateTime _handshakeDeadline;
        private volatile StopReason? _pendingStop;

        private bool _disconnectedNotified;
        private bool _stoppedNotified;

        private sealed class InnerHandler : IRawUnreliableHandler
        {
            private readonly RawUnreliableToRawReliableClient _owner;
            private volatile IRawUnreliableEndpoint? _endpoint;

            public InnerHandler(RawUnreliableToRawReliableClient owner) => _owner = owner;

            public IRawUnreliableEndpoint? Endpoint => _endpoint;

            public void OnStarted(IRawUnreliableEndpoint endpoint)
            {
                _endpoint = endpoint;
            }

            public void OnReceived(UnionDataList data)
            {
                if (_owner._stopped)
                {
                    data.Release();
                    return;
                }

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
                    endpoint.Send(data);
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
            private readonly Delivery.DeliveryManager _dm;
            private readonly RawUnreliableToRawReliableClient _owner;
            private int _messageMaxByteSize;
            private volatile bool _accepted;
            private volatile bool _disconnected;

            public ClientEndpoint(Delivery.DeliveryManager dm, RawUnreliableToRawReliableClient owner)
            {
                _dm = dm;
                _owner = owner;
                _messageMaxByteSize = dm.DeliveryMaxByteSize;
            }

            public IEndPoint? RemoteEndPoint => null;
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
                _owner.RequestStop(reason);
                return true;
            }

            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }
        }

        public RawUnreliableToRawReliableClient(
            IRawUnreliableClient inner,
            IMemoryRental? memoryOverride,
            ILogger? loggerOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;

            _dm = new Delivery.DeliveryManager(
                inner.MessageMaxByteSize,
                Memory.ByteArraysPool,
                Memory.CollectablePool);

            _scheduler = new Delivery.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
            _endpoint = new ClientEndpoint(_dm, this);
            _innerHandler = new InnerHandler(this);
            _sendConsumer = new SendConsumer(_innerHandler);

            _dm.Received += OnDmReceived;
            _dm.Delivered += OnDmDelivered;
            _dm.FailedToDeliver += OnDmFailedToDeliver;

            _inner.Init(_innerHandler);
        }

        public override TransportType Type => TransportType.RawReliable;

        public int MessageMaxByteSize => _endpoint.MessageMaxByteSize;

        public bool Init(IRawReliableClientHandler handler)
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
                Name = "RawUnreliableToRawReliableClient.DM"
            };
            _dmThread.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            _stopped = true;
            _pendingStop = null;
            _workEvent.Set();

            if (_dmThread != null && _dmThread != Thread.CurrentThread)
            {
                _loopExited.Wait(TimeSpan.FromSeconds(3));
            }

            _dm.Clear();
            _endpoint.MarkDisconnected();

            while (_incomingQueue.TryDequeue(out var pending))
            {
                pending.Release();
            }

            var handler = _handler;
            if (handler != null)
            {
                if (_endpoint.WasAccepted && !_disconnectedNotified)
                {
                    _disconnectedNotified = true;
                    try { handler.OnDisconnected(reason); }
                    catch (Exception ex) { Log.wtf(ex); }
                }

                if (!_stoppedNotified)
                {
                    _stoppedNotified = true;
                    try { handler.OnStopped(reason); }
                    catch (Exception ex) { Log.wtf(ex); }
                }
            }

            _inner.Stop(reason);
        }

        private void RequestStop(StopReason reason)
        {
            _pendingStop = reason;
            _workEvent.Set();
        }

        private void DmLoop()
        {
            while (!_stopped)
            {
                _workEvent.WaitOne(TimeSpan.FromMilliseconds(20));
                if (_stopped) break;

                if (!_handshakeSent)
                {
                    var innerEndpoint = _innerHandler.Endpoint;
                    if (innerEndpoint != null)
                    {
                        var request = ConverterHandshake.BuildRequest(_dm.DeliveryMaxByteSize);
                        var result = _dm.ScheduleDelivery(request, out _);
                        if (result != SendResult.Ok)
                        {
                            _pendingStop = new HandshakeRejected(Name, "failed to schedule the handshake request");
                        }
                        else
                        {
                            _handshakeSent = true;
                            _handshakeDeadline = DateTime.UtcNow + HandshakeTimeout;
                        }
                    }
                }

                while (_incomingQueue.TryDequeue(out var data))
                {
                    _dm.ProcessIncoming(data);
                    if (_pendingStop != null)
                    {
                        break;
                    }
                }

                _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _sendConsumer);

                if (_pendingStop == null && _handshakeSent && !_handshakeCompleted &&
                    DateTime.UtcNow >= _handshakeDeadline)
                {
                    _pendingStop = new TimeOut(Name);
                }

                if (_pendingStop != null)
                {
                    break;
                }
            }

            _loopExited.Set();

            var pendingStop = _pendingStop;
            if (pendingStop != null && !_stopped)
            {
                ThreadPool.QueueUserWorkItem(_ => Stop(pendingStop));
            }
        }

        private void OnDmReceived(DeliveryId id, UnionDataList data)
        {
            if (!_handshakeCompleted)
            {
                HandleHandshakeMessage(data);
                return;
            }

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

        private void HandleHandshakeMessage(UnionDataList data)
        {
            try
            {
                if (!ConverterHandshake.TryReadStatus(data, out var status))
                {
                    _pendingStop = new HandshakeRejected(Name, "malformed converter handshake response");
                    return;
                }

                if (status == Status.Accepted)
                {
                    if (!ConverterHandshake.TryParseAccepted(data, out var version, out var name, out var agreedMax) ||
                        version != ConverterHandshake.Version ||
                        !ConverterHandshake.NameEquals(name, ConverterHandshake.Name))
                    {
                        _pendingStop = new ProtocolMismatch(Name, "unexpected converter handshake response");
                        return;
                    }

                    _endpoint.SetAccepted(agreedMax);
                    _handshakeCompleted = true;

                    var handler = _handler;
                    if (handler != null)
                    {
                        try
                        {
                            handler.OnConnected(_endpoint);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                            _pendingStop = new ExceptionFail(Name, ex, "OnConnected threw an exception");
                        }
                    }

                    return;
                }

                if (!ConverterHandshake.TryParseReject(data, out var serverVersion, out var serverName,
                        out var rejectStatus, out var text))
                {
                    _pendingStop = new HandshakeRejected(Name, "malformed converter handshake reject");
                    return;
                }

                _handshakeCompleted = true;
                _pendingStop = MapReject(serverVersion, serverName, rejectStatus, text);
            }
            finally
            {
                data.Release();
            }
        }

        private StopReason MapReject(short serverVersion, string serverName, Status status, string? text)
        {
            var detail = text ?? ConverterHandshake.DescribeStatus(status);
            if (status == Status.VersionMismatch ||
                status == Status.NameMismatch ||
                serverVersion != ConverterHandshake.Version ||
                !ConverterHandshake.NameEquals(serverName, ConverterHandshake.Name))
            {
                return new ProtocolMismatch(Name, detail);
            }

            return new HandshakeRejected(Name, detail);
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
