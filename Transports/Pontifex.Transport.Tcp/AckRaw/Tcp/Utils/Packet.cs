namespace Pontifex.Transports.Tcp
{
    public enum PacketType : byte
    {
        Invalid = 0,
        AckRequest = 1,
        AckResponse = 2,
        Regular = 3,
        Disconnect = 4,
        Ping = 10
    }

    public struct Packet
    {
        public readonly PacketType Type;
        public readonly IMemoryBufferHolder Buffer;

        public Packet(PacketType type, IMemoryBufferHolder buffer)
        {
            Type = type;
            Buffer = buffer;
        }
    }
}
