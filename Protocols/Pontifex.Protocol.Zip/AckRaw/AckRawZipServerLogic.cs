using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Zip
{
    class AckRawZipServerLogic : CompressorLogic, IAckRawWrapperServerLogic
    {
        public AckRawZipServerLogic(ILogger logger, IMemoryRental memoryRental, int compressionLvl)
            : base(logger, memoryRental, compressionLvl)
        {
        }

        public bool ProcessAckData(UnionDataList ackData)
        {
            bool res = ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? data) && data.EqualByContent(ZipInfo.TransportNameBytes);
            data?.Release();
            return res;
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
