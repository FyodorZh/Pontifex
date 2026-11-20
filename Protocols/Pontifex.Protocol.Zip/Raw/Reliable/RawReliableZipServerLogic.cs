using Actuarius.Memory;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Zip
{
    class RawReliableZipServerLogic : CompressorLogic, IRawReliableWrapperLogic
    {
        public RawReliableZipServerLogic(ILogger logger, IMemoryRental memoryRental, int compressionLvl)
            : base(logger, memoryRental, compressionLvl)
        {
        }

        // The negotiation dataset is the layer name "zip", which the session already validated.
        // CheckNegotiationData accepts every request and CreateNegotiationResponse returns an empty
        // tail; both are inherited from RawReliableWrapperLogic.

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
