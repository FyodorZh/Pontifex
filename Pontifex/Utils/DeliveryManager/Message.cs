using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal readonly struct Message
    {
        public const ushort VoidId = 0;

        public ushort PacketId { get; }
        public IMultiRefByteArray Data { get; }

        public Message(ushort packetId, IMultiRefByteArray data)
        {
            PacketId = packetId;
            Data = data;
        }

        public bool IsDeliveryInfo => PacketId == VoidId;
    }
}
