using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public class SystemMessage : IDataStruct
    {
        public long sender;
        public SystemMessageType type;
        public IDataStruct data;

        public SystemMessage() { }

        public SystemMessage(long sender, SystemMessageType type, IDataStruct data)
        {
            this.sender = sender;
            this.type = type;
            this.data = data;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref sender);
            var t = (byte)type;
            dst.Add(ref t);
            type = (SystemMessageType) t;
            dst.Add(ref data);
            return true;
        }
    }
}
