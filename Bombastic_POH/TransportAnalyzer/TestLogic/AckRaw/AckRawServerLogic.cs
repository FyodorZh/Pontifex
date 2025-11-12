using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Shared;
using Transport;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using Shared.Buffer;

namespace TransportAnalyzer.TestLogic
{
    class AckRawServerLogic : IRawServerAcknowledger<IAckRawServerHandler>
    {
        public interface IClientHandler
        {
            string Name { get; }
            void Disconnect(StopReason reason);
        }

        private class Handler : AckRawCommonLogic, IAckRawServerHandler, IClientHandler
        {
            private volatile IAckRawClientEndpoint mEndpoint;

            private long mReceiveId = 0;

            private readonly AckRawServerLogic mOwner;

            private string mText = "<connecting>";

            public Handler(AckRawServerLogic owner)
            {
                mOwner = owner;
            }

            byte[] IAckRawServerHandler.GetAckResponse()
            {
                return AckResponse;
            }

            public void OnConnected(IAckRawClientEndpoint endPoint)
            {
                mEndpoint = endPoint;
                mText = endPoint.RemoteEndPoint.ToString();
                mOwner.Add(this);
            }

            public void OnDisconnected(StopReason reason)
            {
                mEndpoint = null;
                mText += reason.ToString();
                mOwner.Remove(this);
            }

            public void OnReceived(IMemoryBufferHolder receivedBuffer)
            {
                using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
                {
                    ByteArraySegment data;
                    if (!bufferAccessor.Buffer.PopFirst().AsArray(out data))
                    {
                        Log.e("Invalid message");
                        mEndpoint.Disconnect(new Transport.StopReasons.UserFail("Invalid message"));
                    }

                    byte[] toSend = new byte[data.Count];

                    int len = data.Count;
                    for (int i = 0; i < len; ++i)
                    {
                        toSend[i] = data[len - i - 1];
                    }

                    long id = Interlocked.Increment(ref mReceiveId);
                    if (!CheckBuffer(id, new ByteArraySegment(toSend)))
                    {
                        Log.e("Message check failed #" + id);
                        mEndpoint.Disconnect(new Transport.StopReasons.UserFail("Message check failed #" + id));
                    }

                    var endpoint = mEndpoint;
                    if (endpoint != null)
                    {
                        endpoint.Send(ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(toSend));
                    }
                }
            }

            string IClientHandler.Name
            {
                get
                {
                    return mText;
                }
            }

            void IClientHandler.Disconnect(StopReason reason)
            {
                var endpoint = mEndpoint;
                if (endpoint != null)
                {
                    endpoint.Disconnect(reason);
                }
            }

            public override string ToString()
            {
                return mText;
            }
        }

        private readonly ConcurrentDictionary<IClientHandler, IClientHandler> mClients = new ConcurrentDictionary<IClientHandler, IClientHandler>();

        public event Action<IClientHandler> ClientAdded;
        public event Action<IClientHandler> ClientRemoved;

        private void Add(IClientHandler handler)
        {
            mClients.TryAdd(handler, handler);
            var evt = ClientAdded;
            evt?.Invoke(handler);
        }

        private void Remove(IClientHandler handler)
        {
            mClients.TryRemove(handler, out _);
            var evt = ClientRemoved;
            evt?.Invoke(handler);
        }

        public ILogger Log { get; set; }

        public IAckRawServerHandler TryAck(ByteArraySegment ackData, ILogger logger)
        {
            if (Transport.AckUtils.CheckPrefix(ackData, "stress").IsValid)
            {
                Handler h = new Handler(this);
                h.Log = Log;
                return h;
            }
            return null;
        }

        ICollection<IClientHandler> Clients
        {
            get
            {
                return mClients.Values;
            }
        }
    }
}
