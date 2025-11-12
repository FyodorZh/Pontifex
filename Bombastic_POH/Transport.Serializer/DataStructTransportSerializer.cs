using Serializer.BinarySerializer;
using Shared;

namespace Transport.Serializer
{
    public class DataStructTransportSerializer : ITransportSerializer<IDataStruct>
    {
        public byte[] Serialize(IDataStruct data)
        {
            //            Log.d("[DataStructTransportSerializer] Serialize: {0}", data.GetType().FullName);

            var result = Shared.Protocol.ProtocolSerializer.toArray(ref data);
            //                Log.e("[DataStructTransportSerializer] Can't serialize data");
            //            }

            return result;
        }

        public IDataStruct Deserialize(byte[] data)
        {
            return Shared.Protocol.ProtocolSerializer.fromArray<IDataStruct>(data);
        }

        public IDataStruct Deserialize(ByteArraySegment data)
        {
            return Shared.Protocol.ProtocolSerializer.fromArray<IDataStruct>(data);
        }
    }
}