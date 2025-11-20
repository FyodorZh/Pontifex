using Actuarius.Memory;
using Shared;
using Shared.Buffer;
using Shared.Pooling;

namespace Transport.Protocols.Reliable.Delivery
{
    internal interface IUserMessage : IMessage
    {
        DeliveryId Id { get; }
        DeliveryInfo DeliveryInfo { get; }
        short ResponseProcessTime { get; }
        IMultiRefByteArray Data { get; }
    }

    internal abstract class UserMessage<TSelf> : MultiRefCollectable<TSelf>, IUserMessage
        where TSelf : UserMessage<TSelf>
    {
        private DeliveryId mId;
        private IMultiRefByteArray mData;

        private short mResponseProcessTime;

        protected sealed override void OnRestored()
        {
            // DO NOTHING
        }

        protected sealed override void OnCollected()
        {
            DeInit();
        }

        public virtual void WriteTo(IMemoryBuffer dst)
        {
            dst.PushUInt16(mId.Id, false);
            dst.PushAbstractArray(mData.Acquire(), false);
        }

        public virtual bool ReadFrom(IMemoryBuffer dst)
        {
            ushort id;
            if (!dst.PopFirst().AsUInt16(out id))
            {
                return false;
            }
            mId = new DeliveryId(id);

            if (mData != null)
            {
                mData.Release();
            }
            if (!dst.PopFirst().AsAbstractArray(out mData))
            {
                return false;
            }

            return true;
        }

        public DeliveryId Id
        {
            get { return mId; }
        }

        public abstract DeliveryInfo DeliveryInfo { get; }

        public IMultiRefByteArray Data
        {
            get { return mData; }
        }

        public short ResponseProcessTime
        {
            get { return mResponseProcessTime; }
            set { mResponseProcessTime = value; }
        }

        protected void Init(DeliveryId id, IMultiRefByteArray data)
        {
            mId = id;
            mData = data;
        }

        public void DeInit()
        {
            if (mData != null)
            {
                mData.Release();
                mData = null;
            }
        }
    }
}
