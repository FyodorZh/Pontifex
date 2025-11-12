using Shared;
using System.Collections.Generic;
using System.Text;

namespace LogConsumers
{
    public class BytesBuffer
    {
        private readonly ILogFormatter mLogFormatter;
        private const int mMaxMessageCharSize = 1024 * 24;
        private const int mMaxMessageBytesSize = mMaxMessageCharSize * 4; // UTF8 -> UTF32 ???

        private const int mMaxMessagesToSend = 256;

        private readonly IConcurrentQueue<char[]> mCharFreeBuffers = new SynchronizedConcurrentQueue<char[]>();
        private readonly IConcurrentQueue<byte[]>[] mFreeBuffers = new IConcurrentQueue<byte[]>[16];
        private readonly IConcurrentQueue<CharBuffer> mFreeBuffersToWrite = new SynchronizedConcurrentQueue<CharBuffer>();

        private readonly IConcurrentQueue<ByteArraySegment> mBuffersToSend = new SynchronizedConcurrentQueue<ByteArraySegment>(mMaxMessagesToSend);

        private readonly IConcurrentQueue<UTF8Encoding> mEncoders = new SynchronizedConcurrentQueue<UTF8Encoding>();

        public BytesBuffer(ILogFormatter logFormatter)
        {
            mLogFormatter = logFormatter;
            List<IConcurrentQueue<byte[]>> list = new List<IConcurrentQueue<byte[]>>();
            // создаем по одному буферу для каждой степени двойки от 8 до ближайщей верхней степени mMaxMessageBytesSize
            int id = 0;
            while (true)
            {
                list.Add(new SynchronizedConcurrentQueue<byte[]>());
                mCharFreeBuffers.Put(new char[mMaxMessageCharSize]);

                int len = 1 << (id + 8);
                if (len >= mMaxMessageBytesSize)
                {
                    break;
                }
                id += 1;
            }
            mFreeBuffers = list.ToArray();
        }

        public void Clear()
        {
            ByteArraySegment tempSegment;
            while (mBuffersToSend.TryPop(out tempSegment))
            {
            }
        }

        public bool Write(Log.MessageData logMessage)
        {
            CharBuffer buffer;
            if (!mFreeBuffersToWrite.TryPop(out buffer))
            {
                buffer = new CharBuffer(mCharFreeBuffers, mMaxMessageCharSize);
            }

            UTF8Encoding encoder;
            if (!mEncoders.TryPop(out encoder))
            {
                encoder = new UTF8Encoding();
            }

            try
            {
                mLogFormatter.Format(logMessage, buffer);

                foreach (var bufferSegment in buffer)
                {
                    int size = bufferSegment.Count * 2; // sizeof(char) -> sizeof(byte)
                    // индекс подходящего буфера (подходящий буфер - буфер, который вместит необходимое количество байт=size)
                    int pow = System.Math.Max(0, BitMath.HiBit((uint) size) + 1 - 8);

                    if (mFreeBuffers.Length <= pow)
                    {
                        return false;
                    }

                    byte[] bytesBuffer = null;
                    if (!mFreeBuffers[pow].TryPop(out bytesBuffer))
                    {
                        bytesBuffer = new byte[1 << (pow + 8)];
                    }

                    try
                    {
                        //string str = new string(buffer.Buffer, 0, buffer.Length);
                        size = encoder.GetBytes(bufferSegment.Array, bufferSegment.Offset, bufferSegment.Count, bytesBuffer, 0);

                        if (!mBuffersToSend.Put(new ByteArraySegment(bytesBuffer, 0, size)))
                        {
                            mFreeBuffers[pow].Put(bytesBuffer);
                            return false;
                        }
                    }
                    catch
                    {
                        mFreeBuffers[pow].Put(bytesBuffer);
                    }
                }

                return true;
            }
            finally
            {
                buffer.Clear();
                mFreeBuffersToWrite.Put(buffer);
                mEncoders.Put(encoder);
            }
        }

        public struct Enumerator
        {
            private readonly BytesBuffer mOwner;

            private ByteArraySegment mCurrentBuffer;

            public Enumerator(BytesBuffer owner)
            {
                mOwner = owner;
                mCurrentBuffer = new ByteArraySegment();
            }

            public ByteArraySegment Current
            {
                get { return mCurrentBuffer; }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (mCurrentBuffer.IsValid)
                {
                    int len = mCurrentBuffer.Array.Length;
                    int pow = System.Math.Max(0, BitMath.HiBit((uint)len) - 8);

                    mOwner.mFreeBuffers[pow].Put(mCurrentBuffer.Array);
                    mCurrentBuffer = new ByteArraySegment();
                }

                return mOwner.mBuffersToSend.TryPop(out mCurrentBuffer);
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
}
