using System.IO;
using Actuarius.Memory;
using Ionic.Zlib;
using Pontifex.Utils;

namespace Pontifex.Protocols.Zip
{
    public class ZLibCompressor
    {
        private static readonly IConcurrentPool<ZLibCompressor>[] _compressors = new IConcurrentPool<ZLibCompressor>[10];

        static ZLibCompressor()
        {
            for (CompressionLevel cLvl  = CompressionLevel.Level0; cLvl <= CompressionLevel.Level9; cLvl += 1)
            {
                var lvl = cLvl;
                _compressors[(int)cLvl] = new LargeObjectBufferedPool<ZLibCompressor>(() => new ZLibCompressor(lvl));
            }
        }

        public static ZLibCompressor Acquire()
        {
            return Acquire(CompressionLevel.Default);
        }
        public static ZLibCompressor Acquire(CompressionLevel compressionLvl)
        {
            var compressor = _compressors[(int) compressionLvl].Acquire();
            compressor.Reset();
            return compressor;
        }

        public static void Release(ZLibCompressor compressor)
        {
            _compressors[(int)compressor._compressionLevel].Release(compressor);
        }

        private readonly MemoryStream _packedStream;
        private readonly DeflateStream _compressor;
        private readonly CompressionLevel _compressionLevel;

        private ZLibCompressor(CompressionLevel compressionLvl)
        {
            _compressionLevel = compressionLvl;
            _packedStream = new MemoryStream();
            _compressor = new DeflateStream(_packedStream, CompressionMode.Compress, compressionLvl, true);
            Reset();
        }

        public bool Pack(UnionDataList data, ICollectablePool collectablePool, IConcurrentPool<IMultiRefByteArray, int> bytesPool)
        {
            using var bufferHodler = data.Serialize(collectablePool, bytesPool).AsDisposable();
            var buffer = bufferHodler.Value;

            // Упаковываем
            _packedStream.SetLength(0);
            _compressor.Write(buffer.Array, buffer.Offset, buffer.Count);
            _compressor.Flush();

            // Аллоцируем буфер для записи
            int compressedSize = (int)_packedStream.Position;
            using var compressedBufferHolder = bytesPool.Acquire(compressedSize).AsDisposable();
            var compressedBuffer = compressedBufferHolder.Value;            

            // Записываем упакованные данные буфер
            _packedStream.Position = 0;
            if (_packedStream.Read(compressedBuffer.Array, compressedBuffer.Offset, compressedSize) != compressedSize)
            {
                return false;
            }

            // Заменяем исходные данные на сжатый буфер
            data.Clear();
            data.PutFirst(new UnionData(compressedBuffer.Acquire()));

            return true;
        }

        public void Reset()
        {
            _compressor.FlushMode = FlushType.Full;
            _compressor.WriteByte(123); // any byte
            _compressor.Flush();
            _compressor.FlushMode = FlushType.Sync;
            _packedStream.SetLength(0);
        }
    }

    public class ZLibDecompressor
    {
        private static readonly IConcurrentPool<ZLibDecompressor>[] mDecompressors = new IConcurrentPool<ZLibDecompressor>[10];

        static ZLibDecompressor()
        {
            for (CompressionLevel cLvl  = CompressionLevel.Level0; cLvl <= CompressionLevel.Level9; cLvl += 1)
            {
                var lvl = cLvl;
                mDecompressors[(int)cLvl] = new LargeObjectBufferedPool<ZLibDecompressor>(() => new ZLibDecompressor(lvl));
            }
        }

        public static ZLibDecompressor Acquire()
        {
            return Acquire(CompressionLevel.Default);
        }
        public static ZLibDecompressor Acquire(CompressionLevel compressionLvl)
        {
            var compressor =  mDecompressors[(int) compressionLvl].Acquire();
            return compressor;
        }

        public static void Release(ZLibDecompressor decompressor)
        {
            mDecompressors[(int)decompressor._compressionLevel].Release(decompressor);
        }


        private readonly MemoryStream _unpackedStream;
        private readonly DeflateStream _decompressor;
        private readonly CompressionLevel _compressionLevel;
        
        private readonly ByteSourceFromStream _byteSource = new ByteSourceFromStream();

        private ZLibDecompressor(CompressionLevel compressionLvl)
        {
            _compressionLevel = compressionLvl;
            _unpackedStream = new MemoryStream();
            _decompressor = new DeflateStream(_unpackedStream, CompressionMode.Decompress);
        }

        public bool Unpack(UnionDataList data, IConcurrentPool<IMultiRefByteArray, int> bytesPool)
        {
            if (!data.TryPopFirst(out IMultiRefReadOnlyByteArray? compressedBytes) || data.Elements.Count != 0)
            {
                compressedBytes?.Release();
                return false;
            }
            using var compressedBytesDisposer = compressedBytes.AsDisposable();
            
            if (!compressedBytes.IsValid)
            {
                return false;
            }
            
            _unpackedStream.SetLength(0);
            _decompressor.Write(compressedBytes.ReadOnlyArray, compressedBytes.Offset, compressedBytes.Count);
            _decompressor.Flush();
            
            _unpackedStream.Position = 0;
            _byteSource.Reset(_unpackedStream);
            if (!data.Deserialize(_byteSource, bytesPool))
            {
                return false;
            }

            return true;
        }
    }
}