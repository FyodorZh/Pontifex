using Actuarius.Memory;

namespace Transport.Protocols.Zip
{
    public static class ZipInfo
    {
        public const string TransportName = "zip";
        public static readonly MultiRefByteArray TransportNameBytes = new (System.Text.Encoding.UTF8.GetBytes(TransportName));
    }
}
