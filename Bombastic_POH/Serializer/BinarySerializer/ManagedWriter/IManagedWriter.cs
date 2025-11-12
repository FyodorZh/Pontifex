namespace Serializer.BinarySerializer
{
    public interface IBinaryWriter : IBinaryRWCapabilities
    {
        void AddByte(byte v);
        void AddSByte(sbyte v);
        void AddBoolean(bool v);
        void AddChar(char v);
        void AddInt16(short v);
        void AddUInt16(ushort v);
        void AddInt32(int v);
        void AddUInt32(uint v);
        void AddInt64(long v);
        void AddUInt64(ulong v);
        void AddSingle(float v);
        void AddDouble(double v);

        void AddBytes(byte[] bytes);
        void AddBytes(byte[] bytes, int from, int count);
    }

    public interface IManagedWriter : IBinaryWriter
    {
        int ByteSize { get; }

        void Clear();
        void WriteTo(byte[] dst, int offset);
    }
}