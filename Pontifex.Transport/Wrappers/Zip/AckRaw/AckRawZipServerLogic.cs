using Pontifex.Utils;
using Transport.Transports.ProtocolWrapper.AckRaw;

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

        public override void OnConnected()
        {
            // DO NOTHING
        }

        public override void OnDisconnected()
        {
            Release();
        }

        public override bool ProcessReceivedData(UnionDataList receivedData)
        {
            return Decompress(receivedData);
        }

        public override bool ProcessSentData(UnionDataList sentData)
        {
            return Compress(sentData);
        }
    }
}
