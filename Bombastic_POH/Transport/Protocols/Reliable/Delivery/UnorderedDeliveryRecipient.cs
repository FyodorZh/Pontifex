using System.Collections.Generic;
using Actuarius.Memory;
using Shared;
using IMultiRefByteArray = Actuarius.Memory.IMultiRefByteArray;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.Delivery
{
    internal class UnorderedDeliveryRecipient
    {
        private class MessageConstructor
        {
            private readonly byte mChunksNumber;
            private byte mChunksReady;
            private readonly IMultiRefByteArray[] mData;

            public MessageConstructor(byte chunksNumber)
            {
                mChunksNumber = chunksNumber;
                mData = new IMultiRefByteArray[chunksNumber];
            }

            public bool AddChunk(byte chunkId, IMultiRefByteArray data)
            {
                if (data.IsValid && chunkId < mChunksNumber)
                {
                    if (mData[chunkId] == null)
                    {
                        mChunksReady += 1;
                        mData[chunkId] = data.Acquire();
                    }
                }
                return mChunksReady == mChunksNumber;
            }

            public IMultiRefByteArray Combine()
            {
                IMultiRefByteArray combinedArray = ConcurrentPools.Acquire<CollectableMultiSegmentByteArray>().Init(mData);

                for (int i = 0; i < mData.Length; ++i)
                {
                    mData[i] = null;
                }
                mChunksReady = 0;

                return combinedArray;
            }

            public void Clear()
            {
                for (int i = 0; i < mData.Length; ++i)
                {
                    if (mData[i] != null)
                    {
                        mData[i].Release();
                        mData[i] = null;
                    }
                }
                mChunksReady = 0;
            }
        }

        private readonly Dictionary<DeliveryId, MessageConstructor> mUnfinishedMultimessages = new Dictionary<DeliveryId, MessageConstructor>();

        public IMultiRefByteArray Received(UserMultiMessage message)
        {
            MessageConstructor ctor;
            if (!mUnfinishedMultimessages.TryGetValue(message.Id, out ctor))
            {
                ctor = new MessageConstructor(message.PartsNumber);
                mUnfinishedMultimessages.Add(message.Id, ctor);
            }

            if (ctor.AddChunk(message.PartId, message.Data))
            {
                mUnfinishedMultimessages.Remove(message.Id);
                return ctor.Combine();
            }

            return VoidByteArray.Instance;
        }

        public IMultiRefByteArray Received(UserSingleMessage message)
        {
            return message.Data.Acquire();
        }

        public void Clear()
        {
            foreach (var kv in mUnfinishedMultimessages)
            {
                kv.Value.Clear();
            }
            mUnfinishedMultimessages.Clear();
        }
    }
}