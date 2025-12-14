using System.Collections.Concurrent;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;
using Pontifex;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;

namespace TransportAnalyzer.TestLogic
{
    class AckRawServerLogic : IRawServerAcknowledger<IAckRawServerHandler>
    {
        private readonly ConcurrentDictionary<IClientHandler, IClientHandler> mClients = new ConcurrentDictionary<IClientHandler, IClientHandler>();

        public event Action<IClientHandler>? ClientAdded;
        public event Action<IClientHandler>? ClientRemoved;

        private ILogger _logger = StaticLogger.Instance;
        private IMemoryRental _memory = MemoryRental.Shared;

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

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            _logger = logger;
            _memory = memory;
        }

        public IAckRawServerHandler? TryAck(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? ack) && AckRawCommonLogic.AckRequest.EqualByContent(ack) && ackData.Elements.Count == 0)
            {
                ack.Release();
                return new Handler(this);
            }
            return null;
        }

        ICollection<IClientHandler> Clients => mClients.Values;
        
        
        public interface IClientHandler
        {
            string Name { get; }
            void Disconnect(StopReason reason);
        }

        private class Handler : AckRawCommonLogic, IAckRawServerHandler, IClientHandler
        {
            private volatile IAckRawClientEndpoint? mEndpoint;

            private long mReceiveId = 0;

            private readonly AckRawServerLogic mOwner;

            private string mText = "<connecting>";

            public Handler(AckRawServerLogic owner)
            {
                mOwner = owner;
            }

            void IAckRawServerHandler.GetAckResponse(UnionDataList ackResponse)
            {
                ackResponse.PutFirst(new UnionData(AckResponse));
            }

            public void OnConnected(IAckRawClientEndpoint endPoint)
            {
                mEndpoint = endPoint;
                mText = endPoint.RemoteEndPoint.ToString() ?? "null";
                mOwner.Add(this);
            }

            public void OnDisconnected(StopReason reason)
            {
                mEndpoint = null;
                mText += reason.ToString();
                mOwner.Remove(this);
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                try
                {
                    if (!receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
                    {
                        Log.e("Invalid message");
                        mEndpoint?.Disconnect(new Pontifex.StopReasons.UserFail("Invalid message"));
                        return;
                    }
                    using var dataDisposer = data.AsDisposable();


                    
                    var toSend = Memory.CollectablePool.Acquire<UnionDataList>();
                    using var toSendDisposable = toSend.AsDisposable();

                    int len = data.Count;
                    var buffer = Memory.ByteArraysPool.Acquire(len);
                    toSend.PutFirst(new UnionData(buffer));
                    
                    for (int i = 0; i < len; ++i)
                    {
                        buffer[i] = data[len - i - 1];
                    }

                    long id = Interlocked.Increment(ref mReceiveId);
                    if (!CheckBuffer(id, buffer))
                    {
                        Log.e("Message check (s) failed #" + id);
                        mEndpoint?.Disconnect(new Pontifex.StopReasons.UserFail("Message check (s) failed #" + id));
                        return;
                    }

                    var endpoint = mEndpoint;
                    if (endpoint != null)
                    {
                        endpoint.Send(toSend.Acquire());
                    }
                }
                finally
                {
                    receivedBuffer.Release();
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
    }
}
