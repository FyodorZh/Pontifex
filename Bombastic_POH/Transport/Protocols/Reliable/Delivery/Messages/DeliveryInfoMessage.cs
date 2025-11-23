using System.Collections.Generic;
using Actuarius.Memory;
using Shared.Pooling;
using Shared.Buffer;

namespace Transport.Protocols.Reliable.Delivery
{
    internal sealed class DeliveryInfoMessage : MultiRefCollectableResource<DeliveryInfoMessage>, IMessage
    {
        private readonly List<DeliveryInfo> mConfirmedDeliveries = new List<DeliveryInfo>();

        protected override void OnCollected()
        {
            mConfirmedDeliveries.Clear();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public void Append(DeliveryInfo confirmedDelivery)
        {
            mConfirmedDeliveries.Add(confirmedDelivery);
        }

        public List<DeliveryInfo> ConfirmedDeliveries
        {
            get { return mConfirmedDeliveries; }
        }

        public void WriteTo(IMemoryBuffer dst)
        {
            ushort confirmationsNumber = (ushort)mConfirmedDeliveries.Count;

            dst.PushUInt16(confirmationsNumber, false);

            for (int i = 0; i < confirmationsNumber; ++i)
            {
                dst.PushUInt16(mConfirmedDeliveries[i].Id.Id, false);
                dst.PushByte(mConfirmedDeliveries[i].ChunkId, false);
            }
        }

        public bool ReadFrom(IMemoryBuffer dst)
        {
            ushort confirmationsNumber;
            if (!dst.PopFirst().AsUInt16(out confirmationsNumber))
            {
                return false;
            }

            mConfirmedDeliveries.Clear();

            for (int i = 0; i < confirmationsNumber; ++i)
            {
                ushort id;
                byte chunkId;

                if (!dst.PopFirst().AsUInt16(out id) || !dst.PopFirst().AsByte(out chunkId))
                {
                    return false;
                }

                mConfirmedDeliveries.Add(new DeliveryInfo(new DeliveryId(id), chunkId));
            }

            return true;
        }
    }
}
