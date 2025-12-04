using Pontifex.Utils;
using Transport.Abstractions;
using Transport.Transports.ProtocolWrapper.AckRaw;

namespace Transport.Protocols.Zip.AckRaw
{
    class AckRawZipClientLogic : CompressorLogic, IAckRawWrapperClientLogic
    {
        public IControlProvider? Controls => null;

        public AckRawZipClientLogic(int compressionLvl)
            : base(compressionLvl)
        {
        }

        public void UpdateAckData(UnionDataList ackData)
        {
            ackData.PutFirst("zip");
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