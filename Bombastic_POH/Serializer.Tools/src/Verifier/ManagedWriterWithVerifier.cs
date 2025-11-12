namespace Serializer.BinarySerializer
{
    public interface IManagedWriterVerifier
    {
        int DataLength { get; }

        bool Check(byte v, int pos);
        bool Check(char v, int pos);
        bool Check(short v, int pos);
        bool Check(int v, int pos);
        bool Check(long v, int pos);
        bool Check(float v, int pos);
        bool Check(double v, int pos);
        bool Check(byte[] v, int pos);
        bool Check(byte[] v, int from, int count, int pos);
    }

    public struct ManagedWriterWithVerifier<TManagedWriter> : IManagedWriter
        where TManagedWriter : struct, IManagedWriter
    {
        private TManagedWriter mWriter;
        private readonly IManagedWriterVerifier mVerifier;

        public ManagedWriterWithVerifier(TManagedWriter writer, IManagedWriterVerifier verifier)
        {
            mWriter = writer;
            mVerifier = verifier;
        }

        public int ByteSize
        {
            get { return mWriter.ByteSize; }
        }

        public void AddByte(byte v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddByte(v);
        }

        public void AddSByte(sbyte v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddSByte(v);
        }

        public void AddBoolean(bool v)
        {
            mWriter.AddBoolean(v);
        }

        public void AddChar(char v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddChar(v);
        }

        public void AddInt16(short v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddInt16(v);
        }

        public void AddUInt16(ushort v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddUInt16(v);
        }

        public void AddInt32(int v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddInt32(v);
        }

        public void AddUInt32(uint v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddUInt32(v);
        }

        public void AddInt64(long v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddInt64(v);
        }

        public void AddUInt64(ulong v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddUInt64(v);
        }

        public void AddSingle(float v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddSingle(v);
        }

        public void AddDouble(double v)
        {
            mVerifier.Check(v, mWriter.ByteSize);
            mWriter.AddDouble(v);
        }

        public void AddBytes(byte[] bytes)
        {
            mVerifier.Check(bytes, mWriter.ByteSize);
            mWriter.AddBytes(bytes);
        }

        public void AddBytes(byte[] bytes, int @from, int count)
        {
            mVerifier.Check(bytes, from, count, mWriter.ByteSize);
            mWriter.AddBytes(bytes, @from, count);
        }

        public void WriteTo(byte[] dst, int offset)
        {
            mWriter.WriteTo(dst, offset);
        }

        public void Clear()
        {
            mWriter.Clear();
        }

        public bool CanPackPrimitives
        {
            get { return mWriter.CanPackPrimitives; }
        }
    }
}