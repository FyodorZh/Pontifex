using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Transports.TransportWrapper.AckRaw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Zip.AckRaw
{
    class AckRawZipClientLogic : CompressorLogic, IAckRawWrapperClientLogic
    {
        public IControlProvider? Controls => null;

        public AckRawZipClientLogic(ILogger logger, IMemoryRental memoryRental, int compressionLvl)
            : base(logger, memoryRental, compressionLvl)
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