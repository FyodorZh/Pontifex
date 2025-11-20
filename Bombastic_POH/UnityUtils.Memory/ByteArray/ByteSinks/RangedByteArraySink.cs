using System;
using System.ComponentModel;
using Actuarius.Memory;

namespace Shared.ByteSinks
{
    public class RangedByteArraySink : IByteSink
    {
        [ThreadStatic] private static RangedByteArraySink mInstance;

        public static IByteSink ThreadInstance(int srcSkipBytes, byte[] dst, int dstOffset, int dstCount)
        {
            var instance = mInstance;
            if (instance == null)
            {
                mInstance = new RangedByteArraySink();
                instance = mInstance;
            }

            instance.Init(srcSkipBytes, dst, dstOffset, dstCount);

            return instance;
        }

        private byte[] mBytes;
        private int mDstOffset;
        private int mDstEndOffset;

        private int mPos;

        private RangedByteArraySink()
        {
        }

        public RangedByteArraySink(int srcSkipBytes, byte[] dst, int dstOffset, int dstCount)
        {
            Init(srcSkipBytes, dst, dstOffset, dstCount);
        }

        private void Init(int srcSkipBytes, byte[] dst, int dstOffset, int dstCount)
        {
            mDstOffset = dstOffset;
            mDstEndOffset = dstOffset + dstCount;
            mBytes = dst;

            mPos = dstOffset - srcSkipBytes; // возможно < 0
        }

        public bool Put(byte val)
        {
            if (mPos < mDstOffset)
            {
                mPos += 1;
            }
            else if (mPos < mDstEndOffset)
            {
                mBytes[mPos++] = val;
            }
            return true;
        }

        public bool PutMany<TBytes>(TBytes bytes) where TBytes : IReadOnlyBytes
        {
            if (!bytes.IsValid)
            {
                return false;
            }

            int srcCount = bytes.Count;
            int srcOffset = 0;

            if (mPos < mDstOffset)
            {
                int delta = Math.Min(mDstOffset - mPos, srcCount);

                mPos += delta;
                srcCount -= delta;
                srcOffset += delta;
            }

            if (srcCount > 0)
            {
                if (mPos < mDstEndOffset)
                {
                    srcCount = Math.Min(srcCount, mDstEndOffset - mPos);
                    bytes.CopyTo(mBytes, mPos, srcOffset, srcCount);
                    mPos += srcCount;
                }
            }
            return true;
        }
    }
}