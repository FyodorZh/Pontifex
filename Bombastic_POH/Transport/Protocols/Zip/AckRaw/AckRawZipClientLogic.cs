using Transport.Abstractions;
using Transport.Transports.ProtocolWrapper.AckRaw;
using Shared.Buffer;

namespace Transport.Protocols.Zip.AckRaw
{
    class AckRawZipClientLogic : CompressorLogic, IAckRawWrapperClientLogic
    {
        public IControlProvider Controls
        {
            get { return null; }
        }

        public AckRawZipClientLogic(int compressionLvl)
            : base(compressionLvl)
        {
        }

        public byte[] UpdateAckData(byte[] originalAck)
        {
            return AckUtils.AppendPrefix(originalAck, "zip");
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