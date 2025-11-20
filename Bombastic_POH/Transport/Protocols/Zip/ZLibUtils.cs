using System.IO;
using Ionic.Zlib;
using Shared;
using Shared.Buffer;
using Shared.ByteSinks;
using Shared.Pooling;

namespace Transport.Protocols.Zip
{
    public class ZLibCompressor
    {
        private static readonly IConcurrentPool<ZLibCompressor>[] mCompressors = new IConcurrentPool<ZLibCompressor>[10];

        static ZLibCompressor()
        {
            for (CompressionLevel cLvl  = CompressionLevel.Level0; cLvl <= CompressionLevel.Level9; cLvl += 1)
            {
                var lvl = cLvl;
                mCompressors[(int)cLvl] = new LargeObjectBufferedPool<ZLibCompressor>(new LambdaConstructor<ZLibCompressor>(() => new ZLibCompressor(lvl)));
            }
        }

        public static ZLibCompressor Acquire()
        {
            return Acquire(CompressionLevel.Default);
        }
        public static ZLibCompressor Acquire(CompressionLevel compressionLvl)
        {
            var compressor = mCompressors[(int) compressionLvl].Acquire();
            compressor.Reset();
            return compressor;
        }

        public static void Release(ZLibCompressor compressor)
        {
            mCompressors[(int)compressor.mCompressionLevel].Release(compressor);
        }


        private readonly MemoryStream mPackedStream;
        private readonly DeflateStream mCompressor;
        private readonly CompressionLevel mCompressionLevel;

        private byte[] mBuffer = new byte[256];

        private ZLibCompressor(CompressionLevel compressionLvl)
        {
            mCompressionLevel = compressionLvl;
            mPackedStream = new MemoryStream();
            mCompressor = new DeflateStream(mPackedStream, CompressionMode.Compress, compressionLvl, true);
            Reset();
        }

        public bool Pack(IMemoryBuffer data)
        {
            int len = data.Size;
            if (mBuffer.Length < len)
            {
                mBuffer = new byte[len * 4];
            }

            var sink = ByteArraySink.ThreadInstance(mBuffer);
            if (!data.TryWriteTo(sink))
            {
                return false;
            }

            // Упаковываем
            mPackedStream.Position = 0;
            mCompressor.Write(mBuffer, 0, len);
            mCompressor.Flush();

            // Аллоцируем буфер для записи
            int compressedSize = (int)mPackedStream.Position;
            CollectableByteArraySegmentWrapper compressedBuffer = CollectableByteArraySegmentWrapper.Construct(compressedSize);
            ByteArraySegment internalBuffer = compressedBuffer.ShowByteArray();

            // Записываем упакованные данные буфер
            mPackedStream.Position = 0;
            mPackedStream.Read(internalBuffer.ReadOnlyArray, internalBuffer.Offset, compressedBuffer.Count);

            // Заменяем исходные данные на сжатый буфер
            data.Clear();
            data.PushAbstractArray(compressedBuffer);

            return true;
        }

        public void Reset()
        {
            mCompressor.FlushMode = FlushType.Full;
            mCompressor.WriteByte(123); // any byte
            mCompressor.Flush();
            mCompressor.FlushMode = FlushType.Sync;
            mPackedStream.Position = 0;
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
                mDecompressors[(int)cLvl] = new LargeObjectBufferedPool<ZLibDecompressor>(new LambdaConstructor<ZLibDecompressor>(() => new ZLibDecompressor(lvl)));
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
            mDecompressors[(int)decompressor.mCompressionLevel].Release(decompressor);
        }


        private readonly MemoryStream mUnpackedStream;
        private readonly DeflateStream mDecompressor;
        private readonly CompressionLevel mCompressionLevel;

        private ZLibDecompressor(CompressionLevel compressionLvl)
        {
            mCompressionLevel = compressionLvl;
            mUnpackedStream = new MemoryStream();
            mDecompressor = new DeflateStream(mUnpackedStream, CompressionMode.Decompress);
        }

        public bool Unpack(IMemoryBuffer @from, IMemoryBuffer to)
        {
            IMultiRefByteArray compressedBytes;

            var element = @from.PopFirst();
            if (!element.AsAbstractArray(out compressedBytes))
            {
                return false;
            }

            IMultiRefLowLevelByteArray lowLevelCompressedBytes = compressedBytes.ToLowLevelByteArray();
            compressedBytes.Release();

            if (!lowLevelCompressedBytes.IsValid)
            {
                lowLevelCompressedBytes.Release();
                return false;
            }

            mDecompressor.Write(lowLevelCompressedBytes.ReadOnlyArray, lowLevelCompressedBytes.Offset, lowLevelCompressedBytes.Count);
            mDecompressor.Flush();

            lowLevelCompressedBytes.Release();

            IMultiRefLowLevelByteArray unpackedBytes = CollectableByteArraySegmentWrapper.Construct((int)mUnpackedStream.Position);

            mUnpackedStream.Position = 0;
            mUnpackedStream.Read(unpackedBytes.ReadOnlyArray, unpackedBytes.Offset, unpackedBytes.Count);
            mUnpackedStream.Position = 0;

            var res = to.ReadFrom(unpackedBytes);
            unpackedBytes.Release();
            return res;
        }
    }
}