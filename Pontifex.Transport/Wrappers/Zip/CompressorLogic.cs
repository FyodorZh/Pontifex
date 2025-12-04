using Actuarius.Memory;
using Ionic.Zlib;
using Pontifex.Utils;
using Transport.Transports.ProtocolWrapper.AckRaw;

namespace Transport.Protocols.Zip
{
    public abstract class CompressorLogic : AckRawWrapperLogic, IReleasableResource
    {
        private ZLibCompressor? mCompressor;
        private ZLibDecompressor? mDecompressor;

        public CompressorLogic(int compressionLvl)
        {
            if (compressionLvl < 0)
            {
                compressionLvl = 0;
            }
            if (compressionLvl > 9)
            {
                compressionLvl = 9;
            }

            mCompressor = ZLibCompressor.Acquire((CompressionLevel)compressionLvl);
            mDecompressor = ZLibDecompressor.Acquire((CompressionLevel)compressionLvl);
        }

        protected bool Compress(UnionDataList data)
        {
            var compressor = mCompressor;
            if (compressor != null)
            {
                try
                {
                    return compressor.Pack(data, Memory.CollectablePool,  Memory.ByteArraysPool);
                }
                catch
                {
                    mCompressor = null;
                    throw;
                }
            }

            return false;
        }

        protected bool Decompress(UnionDataList data)
        {
            var decompressor = mDecompressor;
            if (decompressor != null)
            {
                try
                {
                    return decompressor.Unpack(data, Memory.ByteArraysPool);
                }
                catch
                {
                    mDecompressor = null;
                    throw;
                }
            }

            return false;
        }

        public void Release()
        {
            var compressor = System.Threading.Interlocked.Exchange(ref mCompressor, null);
            if (compressor != null)
            {
                ZLibCompressor.Release(compressor);
            }

            var decompressor = System.Threading.Interlocked.Exchange(ref mDecompressor, null);
            if (decompressor != null)
            {
                ZLibDecompressor.Release(decompressor);
            }
        }
    }
}
