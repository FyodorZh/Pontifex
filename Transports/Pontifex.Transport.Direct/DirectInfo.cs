using Actuarius.Memory;

namespace Pontifex
{
    public static class DirectInfo
    {
        public const string TransportName = "direct";
        public const int DefaultMessageMaxSize = 1024 * 1024;
        public const int BufferCapacity = 500; // todo remove
        public const long DefaultBufferCapacity = 1024 * 1024 * 100;

        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("Direct-Ack-OK"));
    }
}