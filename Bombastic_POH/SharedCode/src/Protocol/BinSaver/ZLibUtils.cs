using Ionic.Zlib;
using System;
namespace Shared.Protocol
{
    public class ZLibCompressor
    {
        private System.IO.MemoryStream mPackedStream;
        private DeflateStream mCompressor;

        public ZLibCompressor()
        {
            mPackedStream = new System.IO.MemoryStream();
            mCompressor = new DeflateStream(mPackedStream, CompressionMode.Compress, CompressionLevel.BestCompression, true);
            mCompressor.FlushMode = FlushType.Full;
        }

        public byte[] Pack(ByteArray data)
        {
            mPackedStream.Position = 0;
            mCompressor.Write(data.Data, 0, data.Length);
            mCompressor.Flush();

            return mPackedStream.ToArray();
        }

        public ByteArray Pack(ByteArray data, int headerSize)
        {
            if (mPackedStream.Length < headerSize)
            {
                mPackedStream.SetLength(headerSize);
            }
            mPackedStream.Position = headerSize;

            mCompressor.Write(data.Data, 0, data.Length);
            mCompressor.Flush();

            return ByteArray.AssumeControl(mPackedStream.GetBuffer(), (int)mPackedStream.Position, false);
        }
    }

    public class ZLibDecompressor
    {
        private System.IO.MemoryStream mUnpackedStream;
        private DeflateStream mDecompressor;

        public ZLibDecompressor()
        {
            mUnpackedStream = new System.IO.MemoryStream();
            mDecompressor = new DeflateStream(mUnpackedStream, CompressionMode.Decompress);
        }

        public ByteArray Unpack(byte[] data, int offset, int count)
        {
            mDecompressor.Write(data, offset, count);
            mDecompressor.Flush();

            int unpackedLength = (int)mUnpackedStream.Length;

            ByteArray result = ByteArray.AllocateNoLess(unpackedLength);

            mUnpackedStream.Position = 0;
            mUnpackedStream.Read(result.Data, 0, unpackedLength);
            mUnpackedStream.Position = 0;

            return result;
        }
    }
}
