using Shared;

namespace Transport.Protocols.Reliable.Delivery
{
    internal class UserSingleMessage : UserMessage<UserSingleMessage>, IInitable<DeliveryId, IMultiRefByteArray>
    {
        public override DeliveryInfo DeliveryInfo
        {
            get { return new DeliveryInfo(Id, 0); }
        }

        public new bool Init(DeliveryId id, IMultiRefByteArray data)
        {
            base.Init(id, data);
            return true;
        }
    }
}
