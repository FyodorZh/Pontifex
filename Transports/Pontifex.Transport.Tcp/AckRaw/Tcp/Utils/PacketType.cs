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
}
