using Serializer.BinarySerializer;
using Shared.ByteSinks;

namespace Shared.Buffer
{
    public struct BufferWriter : IManagedWriter
    {
        private IMemoryBufferHolder mBufferHolder;
        private IMemoryBuffer mBuffer;

        public void SetBuffer(IMemoryBufferHolder buffer)
        {
            mBuffer = null;
            if (mBufferHolder != null)
            {
                mBufferHolder.Release();
            }

            mBufferHolder = buffer;
            if (buffer != null)
            {
                mBuffer = buffer.ShowBufferUnsafe();
            }
        }

        /// <summary>
        /// Number of elements
        /// </summary>
        public int Count
        {
            get
            {
                return mBuffer.Count;
            }
        }

        public IMemoryBufferHolder ExtractBuffer()
        {
            var buffer = mBufferHolder;
            mBufferHolder = null;
            mBuffer = null;
            return buffer;
        }

        int IManagedWriter.ByteSize
        {
            get
            {
                return mBuffer.Size;
            }
        }

        void IBinaryWriter.AddBoolean(bool v)
        {
            mBuffer.PushBoolean(v, false);
        }

        void IBinaryWriter.AddByte(byte v)
        {
            mBuffer.PushByte(v, false);
        }

        void IBinaryWriter.AddBytes(byte[] bytes)
        {
            mBuffer.PushArray(new ByteArraySegment(bytes), false);
        }

        void IBinaryWriter.AddBytes(byte[] bytes, int from, int count)
        {
            mBuffer.PushArray(new ByteArraySegment(bytes, from, count), false);
        }

        void IBinaryWriter.AddChar(char v)
        {
            mBuffer.PushUInt16((ushort)v, false);
        }

        void IBinaryWriter.AddDouble(double v)
        {
            mBuffer.PushDouble(v, false);
        }

        void IBinaryWriter.AddInt16(short v)
        {
            mBuffer.PushUInt16(unchecked((ushort)v), false);
        }

        void IBinaryWriter.AddInt32(int v)
        {
            mBuffer.PushInt32(v, false);
        }

        void IBinaryWriter.AddInt64(long v)
        {
            mBuffer.PushInt64(v, false);
        }

        void IBinaryWriter.AddSByte(sbyte v)
        {
            mBuffer.PushByte(unchecked((byte)v), false);
        }

        void IBinaryWriter.AddSingle(float v)
        {
            mBuffer.PushSingle(v, false);
        }

        void IBinaryWriter.AddUInt16(ushort v)
        {
            mBuffer.PushUInt16(v, false);
        }

        void IBinaryWriter.AddUInt32(uint v)
        {
            mBuffer.PushInt32(unchecked((int)v), false);
        }

        void IBinaryWriter.AddUInt64(ulong v)
        {
            mBuffer.PushInt64(unchecked((long)v), false);
        }

        void IManagedWriter.Clear()
        {
            mBuffer.Clear();
        }

        void IManagedWriter.WriteTo(byte[] dst, int offset)
        {
            var sink = ByteArraySink.ThreadInstance(dst, offset);
            mBuffer.TryWriteTo(sink);
        }

        public void AddBuffer(IMemoryBufferHolder buffer)
        {
            mBuffer.PushBuffer(buffer, false);
        }

        public bool CanPackPrimitives
        {
            get { return true; }
        }
    }
}
