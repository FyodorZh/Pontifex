using System;

namespace Serializer.BinarySerializer
{
    public struct ManagedWriter<TAllocator> : IManagedWriter
        where TAllocator : struct, IAllocator
    {
        private TAllocator mAllocator;

        /// <summary>
        /// Внутренний буфер сериализации
        /// </summary>
        private byte[] mBuffer;

        private int mCapacity;

        /// <summary>
        /// Текущая позиция сериализации
        /// </summary>
        private int mBufferPos;
        public int ByteSize { get { return mBufferPos; } }

        public static ManagedWriter<TAllocator> Construct(TAllocator allocator)
        {
            ManagedWriter<TAllocator> writer = new ManagedWriter<TAllocator>();
            writer.Init(allocator, 128);
            return writer;
        }

        private void Init(TAllocator allocator, int capacity)
        {
            mAllocator = allocator;
            mCapacity = capacity;
            mBuffer = mAllocator.Allocate(mCapacity);
            mBufferPos = 0;
        }

        public void Clear()
        {
            mBufferPos = 0;
        }

        private void Extend(int dSize)
        {
            int needCapacity = mBufferPos + dSize;
            if (needCapacity >= mCapacity)
            {
                while (needCapacity >= mCapacity)
                {
                    mCapacity *= 2;
                }
                mBuffer = mAllocator.Reallocate(mBuffer, mCapacity);
            }
        }

        public void AddBoolean(bool v)
        {
            this.AddByte((byte)(v ? 1 : 0));
        }

        public void AddByte(byte v)
        {
            Extend(sizeof(byte));
            mBuffer[mBufferPos++] = v;
        }

        public void AddChar(char v)
        {
            Extend(sizeof(char));
            mBufferPos = EndianIndependentSerializer.CharWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddDouble(double v)
        {
            Extend(sizeof(double));
            mBufferPos = EndianIndependentSerializer.DoubleWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddInt16(short v)
        {
            Extend(sizeof(short));
            mBufferPos = EndianIndependentSerializer.ShortWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddInt32(int v)
        {
            Extend(sizeof(int));
            mBufferPos = EndianIndependentSerializer.IntWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddInt64(long v)
        {
            Extend(sizeof(long));
            mBufferPos = EndianIndependentSerializer.LongWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddSByte(sbyte v)
        {
            Extend(sizeof(sbyte));
            mBuffer[mBufferPos++] = (byte)v;
        }

        public void AddSingle(float v)
        {
            Extend(sizeof(float));
            mBufferPos = EndianIndependentSerializer.FloatWriter.Write(mBuffer, mBufferPos, v);
        }

        public void AddUInt16(ushort v)
        {
            Extend(sizeof(ushort));
            mBufferPos = EndianIndependentSerializer.ShortWriter.Write(mBuffer, mBufferPos, (short)v);
        }

        public void AddUInt32(uint v)
        {
            Extend(sizeof(uint));
            mBufferPos = EndianIndependentSerializer.IntWriter.Write(mBuffer, mBufferPos, (int)v);
        }

        public void AddUInt64(ulong v)
        {
            Extend(sizeof(ulong));
            mBufferPos = EndianIndependentSerializer.LongWriter.Write(mBuffer, mBufferPos, (long)v);
        }

        public void AddBytes(byte[] bytes)
        {
            Extend(bytes.Length);
            Buffer.BlockCopy(bytes, 0, mBuffer, mBufferPos, bytes.Length);
            mBufferPos += bytes.Length;
        }

        public void AddBytes(byte[] bytes, int from, int count)
        {
            Extend(count);
            Buffer.BlockCopy(bytes, from, mBuffer, mBufferPos, count);
            mBufferPos += count;
        }

        public void WriteTo(byte[] dst, int offset)
        {
            Buffer.BlockCopy(mBuffer, 0, dst, offset, mBufferPos);
        }

        public byte[] ShowByteDataUnsafe(out int outSize)
        {
            outSize = mBufferPos;
            return mBuffer;
        }

        public bool CanPackPrimitives
        {
            get { return false; }
        }
    }
}