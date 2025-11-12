using System.Text;

namespace Transport.Protocols.Reliable
{
    public static class ReliableInfo
    {
        public const string TransportName = "reliable";

        public static readonly int LogicServerQuantLength = 10;
        public static readonly int LogicClientQuantLength = 50;
        public static readonly int TransportMessageQueueCapacity = 5000;

        public static readonly byte[] AckPrefix = Encoding.UTF8.GetBytes("AckRawReliable");
        public static readonly byte[] AckOKResponse = Encoding.UTF8.GetBytes("AckRawReliable-OK");
    }
}
