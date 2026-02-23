using Actuarius.Memory;

namespace Pontifex.Transports.Direct
{
    public static class DirectInfo
    {
        public const string TransportName = "direct";
        public const int MessageMaxByteSize = 1024*1204;

        public const int BufferCapacity = 500;

        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("Direct-Ack-OK"));
    }
}