using NewProtocol;
using Serializer.BinarySerializer;

namespace TransportAnalyzer.TestLogic
{
    class BytesBuffer : IDataStruct
    {
        public byte[] Data;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Data);
            return true;
        }
    }

    class AckRawProtocol : Protocol
    {
        public readonly S2CMessageDecl<BytesBuffer> OnAck = new S2CMessageDecl<BytesBuffer>();

        public readonly RRDecl<BytesBuffer, BytesBuffer> Request = new RRDecl<BytesBuffer, BytesBuffer>();

    }
}
