using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Ionic.Zlib;
using Pontifex.Abstractions;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Zip
{
    public abstract class CompressorLogic : AckRawWrapperLogic, IReleasableResource
    {
        private ZLibCompressor? _compressor;
        private ZLibDecompressor? _decompressor;

        public CompressorLogic(ILogger logger, IMemoryRental memoryRental, int compressionLvl)
            :base(logger, memoryRental)
        {
            if (compressionLvl < 0)
            {
                compressionLvl = 0;
            }
            if (compressionLvl > 9)
            {
                compressionLvl = 9;
            }

            _compressor = ZLibCompressor.Acquire((CompressionLevel)compressionLvl);
            _decompressor = ZLibDecompressor.Acquire((CompressionLevel)compressionLvl);
        }

        protected bool Compress(UnionDataList data)
        {
            var compressor = _compressor;
            if (compressor != null)
            {
                try
                {
                    return compressor.Pack(data, Memory.ByteArraysPool);
                }
                catch
                {
                    _compressor = null;
                    throw;
                }
            }
            return false;
        }

        protected bool Decompress(UnionDataList data)
        {
            var decompressor = _decompressor;
            if (decompressor != null)
            {
                try
                {
                    return decompressor.Unpack(data, Memory.ByteArraysPool);
                }
                catch
                {
                    _decompressor = null;
                    throw;
                }
            }
            return false;
        }

        public void Release()
        {
            var compressor = System.Threading.Interlocked.Exchange(ref _compressor, null);
            if (compressor != null)
            {
                ZLibCompressor.Release(compressor);
            }

            var decompressor = System.Threading.Interlocked.Exchange(ref _decompressor, null);
            if (decompressor != null)
            {
                ZLibDecompressor.Release(decompressor);
            }
        }
        
        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
    }
}
