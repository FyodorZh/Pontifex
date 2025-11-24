using Pontifex.Utils;
using Shared;
using Transport.Transports.ProtocolWrapper.AckRaw;
using Shared.Buffer;

namespace Transport.Protocols.Zip.AckRaw
{
    class AckRawZipServerLogic : CompressorLogic, IAckRawWrapperServerLogic
    {
        public AckRawZipServerLogic(int compressionLvl)
            : base(compressionLvl)
        {
        }

        public bool ProcessAckData(UnionDataList ackData)
        {
            return ackData.Check("zip");
        }

        public void OnConnected()
        {
            // DO NOTHING
        }

        public void OnDisconnected()
        {
            Release();
        }

        public bool ProcessReceivedData(IMemoryBuffer receivedData)
        {
            return Decompress(receivedData);
        }

        public bool ProcessSentData(IMemoryBuffer sentData)
        {
            return Compress(sentData);
        }
    }
}
