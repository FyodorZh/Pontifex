using System.Collections.Concurrent;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;
using Pontifex;
using Pontifex.Raw.Reliable.Ack;

namespace TransportAnalyzer.TestLogic
{
    class RawReliableAckServerLogic : IRawServerAcknowledger<IRawReliableAckServerHandler>
    {
        private readonly ConcurrentDictionary<IClientHandler, IClientHandler> mClients = new ConcurrentDictionary<IClientHandler, IClientHandler>();

        public event Action<IClientHandler>? ClientAdded;
        public event Action<IClientHandler>? ClientRemoved;

        private readonly ILogger _logger;
        private readonly IMemoryRental _memory;

        public RawReliableAckServerLogic(ILogger logger, IMemoryRental memory)
        {
            _logger = logger;
            _memory = memory;
        }

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

        public IRawReliableAckServerHandler? TryAck(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? ack) && RawReliableAckCommonLogic.AckRequest.EqualByContent(ack) && ackData.Elements.Count == 0)
            {
                ack.Release();
                return new Handler(this, _memory, _logger);
            }
            return null;
        }

        ICollection<IClientHandler> Clients => mClients.Values;
        
        
        public interface IClientHandler
        {
            string Name { get; }
            void Disconnect(StopReason reason);
        }

        private class Handler : RawReliableAckCommonLogic, IRawReliableAckServerHandler, IClientHandler
        {
            private volatile IRawReliableAckServerSideEndpoint? mEndpoint;

            private long mReceiveId = 0;

            private readonly RawReliableAckServerLogic mOwner;

            private string mText = "<connecting>";

            public Handler(RawReliableAckServerLogic owner, IMemoryRental memoryRental, ILogger logger)
                :base(memoryRental, logger)
            {
                mOwner = owner;
            }

            void IRawAckServerHandler.FillAckResponse(UnionDataList ackResponse)
            {
                ackResponse.PutFirst(new UnionData(AckResponse));
            }

            public void OnConnected(IRawReliableAckServerSideEndpoint endPoint)
            {
                mEndpoint = endPoint;
                mText = endPoint.RemoteEndPoint?.ToString() ?? "null";
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
