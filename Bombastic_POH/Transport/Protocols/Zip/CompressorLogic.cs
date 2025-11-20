using Actuarius.Memory;
using Ionic.Zlib;
using Shared;
using Shared.Buffer;
using Shared.Pooling;

namespace Transport.Protocols.Zip
{
    public class CompressorLogic : IReleasableResource
    {
        private ZLibCompressor mCompressor;
        private ZLibDecompressor mDecompressor;

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

        protected bool Compress(IMemoryBuffer data)
        {
            if (mCompressor != null)
            {
                try
                {
                    return mCompressor.Pack(data);
                }
                catch
                {
                    mCompressor = null;
                    throw;
                }
            }

            return false;
        }

        protected bool Decompress(IMemoryBuffer data)
        {
            if (mDecompressor != null)
            {
                try
                {
                    return mDecompressor.Unpack(data, data);
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
