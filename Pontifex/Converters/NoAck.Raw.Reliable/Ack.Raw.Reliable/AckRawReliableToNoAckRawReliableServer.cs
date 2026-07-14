using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.NoAck.Raw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Converters
{
    public sealed class AckRawReliableToNoAckRawReliableServer : AnyTransport, INoAckRawReliableServer, IRawServerAcknowledger<IAckRawReliableServerHandler>
    {
        private readonly IAckRawReliableServer _inner;

        private readonly ConcurrentDictionary<IEndPoint, ServerSession> _sessions = new();

        public AckRawReliableToNoAckRawReliableServer(
            IAckRawReliableServer inner,
            ILogger? loggerOverride,
            IMemoryRental? memoryOverride)
            : base(inner.Name, loggerOverride ?? inner.Log, memoryOverride ?? inner.Memory)
        {
            _inner = inner;
        }

        public override TransportType Type => TransportType.NoAckRawReliable;

        public event Action<IEndPoint, UnionDataList>? OnReceived;

        public int MessageMaxByteSize => _inner.MessageMaxByteSize;

        public SendResult Send(IEndPoint destination, UnionDataList message)
        {
            if (!IsStarted)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            if (_sessions.TryGetValue(destination, out var session))
            {
                return session.Send(message);
            }

            message.Release();
            return SendResult.InvalidAddress;
        }

        #region AnyTransport overrides

        protected override bool TryStart()
        {
            if (!_inner.Init(this))
            {
                Log.e("Failed to init inner server");
                return false;
            }

            return _inner.Start(r =>
            {
                if (IsStarted)
                {
                    Fail(r, "Unexpected inner server stop");
                }
            });
        }

        protected override void OnStarted()
        {
        }

        protected override void OnStopped(StopReason reason)
        {
            _sessions.Clear();
            _inner.Stop(reason);
        }

        #endregion

        #region IRawServerAcknowledger

        IAckRawReliableServerHandler? IRawServerAcknowledger<IAckRawReliableServerHandler>.TryAck(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            var session = new ServerSession(this);
            return session;
        }

        #endregion

        private void RegisterSession(ServerSession session, IAckRawReliableServerSideEndpoint endpoint)
        {
            var remoteEp = endpoint.RemoteEndPoint;
            if (remoteEp != null)
            {
                _sessions[remoteEp] = session;
                Log.i("Client connected: {0}", remoteEp);
            }
        }

        private void UnregisterSession(ServerSession session)
        {
            var remoteEp = session.RemoteEndPoint;
            if (remoteEp != null)
            {
                _sessions.TryRemove(remoteEp, out _);
                Log.i("Client disconnected: {0}", remoteEp);
            }
        }

        public override string ToString()
        {
            return $"{Name}<inner-server>";
        }

        #region ServerSession

        private sealed class ServerSession : IAckRawReliableServerHandler
        {
            private readonly AckRawReliableToNoAckRawReliableServer _owner;
            private IAckRawReliableServerSideEndpoint? _endpoint;

            public IEndPoint? RemoteEndPoint => _endpoint?.RemoteEndPoint;

            public ServerSession(AckRawReliableToNoAckRawReliableServer owner)
            {
                _owner = owner;
            }

            void IAckRawReliableServerHandler.OnConnected(IAckRawReliableServerSideEndpoint endPoint)
            {
                _endpoint = endPoint;
                _owner.RegisterSession(this, endPoint);
            }

            void IAckRawBaseHandler.OnDisconnected(StopReason reason)
            {
                _owner.UnregisterSession(this);
            }

            void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
            {
                var handler = _owner.OnReceived;
                var ep = RemoteEndPoint;
                if (handler != null && ep != null)
                {
                    try
                    {
                        handler(ep, receivedBuffer);
                    }
                    catch (Exception ex)
                    {
                        _owner.Log.wtf(ex);
                    }
                }
                else
                {
                    receivedBuffer.Release();
                }
            }

            void IAckRawServerHandler.FillAckResponse(UnionDataList ackData)
            {
            }

            public SendResult Send(UnionDataList message)
            {
                return _endpoint?.Send(message) ?? SendResult.NotConnected;
            }
        }

        #endregion
    }
}
