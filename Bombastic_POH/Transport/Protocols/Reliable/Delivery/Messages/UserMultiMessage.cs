using Shared;
using Shared.Buffer;

namespace Transport.Protocols.Reliable.Delivery
{
    internal class UserMultiMessage : UserMessage<UserMultiMessage>, IInitable<DeliveryId, IMultiRefByteArray, byte, byte>
    {
        private byte mPartId;
        private byte mPartsNumber;


        public bool Init(DeliveryId id, IMultiRefByteArray data, byte partId, byte partsNumber)
        {
            base.Init(id, data);
            mPartId = partId;
            mPartsNumber = partsNumber;
            return true;
        }

        public override void WriteTo(IMemoryBuffer dst)
        {
            dst.PushByte(mPartId, false);
            dst.PushByte(mPartsNumber, false);
            base.WriteTo(dst);
        }

        public override bool ReadFrom(IMemoryBuffer dst)
        {
            if (!dst.PopFirst().AsByte(out mPartId) ||
                !dst.PopFirst().AsByte(out mPartsNumber))
            {
                return false;
            }

            return base.ReadFrom(dst);
        }

        public override DeliveryInfo DeliveryInfo
        {
            get { return new DeliveryInfo(Id, mPartId); }
        }

        public byte PartId
        {
            get { return mPartId; }
        }

        public byte PartsNumber
        {
            get { return mPartsNumber; }
        }
    }
}
