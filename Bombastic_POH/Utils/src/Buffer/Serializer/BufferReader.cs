using Serializer.BinarySerializer;

namespace Shared.Buffer
{
    public struct BufferReader : IManagedReader
    {
        private IMemoryBuffer mBuffer;

        public void SetBuffer(IMemoryBuffer buffer)
        {
            mBuffer = buffer;
        }

        /// <summary>
        /// Memory size
        /// </summary>
        public int BufferSize
        {
            get { return mBuffer.Size; }
        }

        bool IManagedReader.IsEndOfBuffer
        {
            get
            {
                return mBuffer.Count == 0;
            }
        }

        public void SkipElements(int count)
        {
            for (int i = 0; i < count; i++)
            {
                BufferElement element = mBuffer.PopFirst();
                element.Clear();
            }
        }

        bool IBinaryReader.ReadBoolean()
        {
            bool res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsBoolean(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        byte IBinaryReader.ReadByte()
        {
            byte res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsByte(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        void IBinaryReader.ReadBytes(byte[] outData)
        {
            ByteArraySegment res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsArray(out res))
            {
                if (!res.CopyTo(outData, 0))
                {
                    throw new System.InvalidOperationException("Failed to read array element");
                }

                return;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        char IBinaryReader.ReadChar()
        {
            ushort res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsUInt16(out res))
            {
                return (char)res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        double IBinaryReader.ReadDouble()
        {
            double res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsDouble(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        short IBinaryReader.ReadInt16()
        {
            ushort res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsUInt16(out res))
            {
                return unchecked((short)res);
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        int IBinaryReader.ReadInt32()
        {
            int res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsInt32(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        long IBinaryReader.ReadInt64()
        {
            long res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsInt64(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        sbyte IBinaryReader.ReadSByte()
        {
            byte res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsByte(out res))
            {
                return unchecked((sbyte)res);
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        float IBinaryReader.ReadSingle()
        {
            float res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsSingle(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        ushort IBinaryReader.ReadUInt16()
        {
            ushort res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsUInt16(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        uint IBinaryReader.ReadUInt32()
        {
            int res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsInt32(out res))
            {
                return unchecked((uint)res);
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        ulong IBinaryReader.ReadUInt64()
        {
            long res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsInt64(out res))
            {
                return unchecked((ulong)res);
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        public IMemoryBufferHolder ReadBuffer()
        {
            IMemoryBufferHolder res;
            BufferElement element = mBuffer.PopFirst();
            if (element.AsBuffer(out res))
            {
                return res;
            }
            throw new System.InvalidOperationException(string.Format("Wrong element '{0}' type", element));
        }

        public bool CanPackPrimitives
        {
            get { return true; }
        }
    }
}
