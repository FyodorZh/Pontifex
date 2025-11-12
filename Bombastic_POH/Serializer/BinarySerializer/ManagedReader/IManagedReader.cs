namespace Serializer.BinarySerializer
{
    public interface IBinaryReader : IBinaryRWCapabilities
    {
        byte ReadByte();
        sbyte ReadSByte();
        bool ReadBoolean();
        char ReadChar();
        short ReadInt16();
        ushort ReadUInt16();
        int ReadInt32();
        uint ReadUInt32();
        long ReadInt64();
        ulong ReadUInt64();
        float ReadSingle();
        double ReadDouble();

        void ReadBytes(byte[] outData);
    }

    public interface IManagedReader : IBinaryReader
    {
        bool IsEndOfBuffer { get; }
    }
}