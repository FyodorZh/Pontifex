using Actuarius.Memory;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Zip
{
    class RawReliableZipClientLogic : CompressorLogic, IRawReliableWrapperLogic
    {
        public RawReliableZipClientLogic(ILogger logger, IMemoryRental memoryRental, int compressionLvl)
            : base(logger, memoryRental, compressionLvl)
        {
        }

        // The client negotiation request has no custom elements: the layer name "zip" is the dataset.
        // FillNegotiationData is inherited from RawReliableWrapperLogic.

        public override void OnConnected()
        {
            // The compressor state is acquired by the constructor and lives for the whole session.
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
