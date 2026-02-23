using Actuarius.Memory;

namespace Pontifex.Protocols.Zip
{
    public static class ZipInfo
    {
        public const string TransportName = "zip";
        public static readonly IMultiRefReadOnlyByteArray TransportNameBytes = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes(TransportName));
    }
}
