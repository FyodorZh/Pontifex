using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal readonly struct Message
    {
        public const ushort VoidId = 0;

        public ushort PacketId { get; }
        public UnionDataList Data { get; }

        public Message(ushort packetId, UnionDataList data)
        {
            PacketId = packetId;
            Data = data;
        }

        public bool IsDeliveryInfo => PacketId == VoidId;
    }
}
