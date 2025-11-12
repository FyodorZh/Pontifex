using System;

namespace Serializer.BinarySerializer
{
    public struct ManagedReader : IManagedReader
    {
        /// <summary>
        /// Внутренний буфер сериализации
        /// </summary>
        private byte[] mBuffer;
        /// <summary>
        /// Текущая позиция сериализации
        /// </summary>
        private int mBufferPos;

        private int mLength;

        public void Reset(byte[] buffer, int startIndex, int length = -1)
        {
            mBuffer = buffer;
            if (mBuffer == null)
            {
                mBufferPos = 0;
                mLength = 0;
                return;
            }
            else
            {
                mLength = length != -1 ? Math.Min(length, mBuffer.Length) : mBuffer.Length;
            }

            Reset(startIndex);
        }

        public void Reset(int startIndex)
        {
            if (startIndex >= mLength)
            {
                startIndex = mLength;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            mBufferPos = startIndex;
        }

        public void Reset()
        {
            Reset(null, 0);
        }

        public bool IsEndOfBuffer
        {
            get
            {
                return BufferSize <= BufferPos;
            }
        }

        public int BufferSize { get { return mLength; } }

        public int BufferPos { get { return (null != mBuffer ? mBufferPos : 0); } }

        public bool ReadBoolean() { return (0 != (mBuffer[mBufferPos++])); }

        public byte ReadByte() { return mBuffer[mBufferPos++]; }

        public char ReadChar()
        {
            return EndianIndependentSerializer.CharReader.Read(mBuffer, ref mBufferPos);
        }

        public double ReadDouble()
        {
            return EndianIndependentSerializer.DoubleReader.Read(mBuffer, ref mBufferPos);
        }

        public short ReadInt16()
        {
            return EndianIndependentSerializer.ShortReader.Read(mBuffer, ref mBufferPos);
        }

        public int ReadInt32()
        {
            return EndianIndependentSerializer.IntReader.Read(mBuffer, ref mBufferPos);
        }

        public long ReadInt64()
        {
            return EndianIndependentSerializer.LongReader.Read(mBuffer, ref mBufferPos);
        }

        public sbyte ReadSByte() { return (sbyte)mBuffer[mBufferPos++]; }

        public float ReadSingle()
        {
            return EndianIndependentSerializer.FloatReader.Read(mBuffer, ref mBufferPos);
        }

        public ushort ReadUInt16()
        {
            return (ushort)EndianIndependentSerializer.ShortReader.Read(mBuffer, ref mBufferPos);
        }

        public uint ReadUInt32()
        {
            return (uint)EndianIndependentSerializer.IntReader.Read(mBuffer, ref mBufferPos);
        }

        public ulong ReadUInt64()
        {
            return (ulong)EndianIndependentSerializer.LongReader.Read(mBuffer, ref mBufferPos);
        }

        public void ReadBytes(byte[] outData)
        {
            Buffer.BlockCopy(mBuffer, mBufferPos, outData, 0, outData.Length);
            mBufferPos += outData.Length;
        }

        public bool CanPackPrimitives
        {
            get { return false; }
        }
    }
}
