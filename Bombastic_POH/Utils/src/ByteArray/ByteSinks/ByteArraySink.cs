using System;

namespace Shared.ByteSinks
{
    public class ByteArraySink : IByteSink
    {
        [ThreadStatic] private static ByteArraySink mInstance;

        private byte[] mBytes;
        private int mPos;

        public static IByteSink ThreadInstance(byte[] bytes, int offset = 0)
        {
            var instance = mInstance;
            if (instance == null)
            {
                mInstance = new ByteArraySink();
                instance = mInstance;
            }

            instance.mBytes = bytes;
            instance.mPos = offset;

            return instance;
        }

        private ByteArraySink()
        {
        }

        public ByteArraySink(byte[] bytes, int offset)
        {
            mBytes = bytes;
            mPos = offset;
        }

        public void Push(byte val)
        {
            mBytes[mPos++] = val;
        }

        public void Push<TBytes>(TBytes bytes) where TBytes : IByteArray
        {
            int count = bytes.Count;
            bytes.CopyTo(mBytes, mPos, 0, count);
            mPos += count;
        }
    }
}