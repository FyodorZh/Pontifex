using Actuarius.Memory;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    internal static class RawReliableAckDirectInfo
    {
        public const string TransportName = "direct";
        public const int MessageMaxByteSize = 1024 * 1024;
        public const int QueueCapacity = 1000;

        public static readonly IMultiRefReadOnlyByteArray AckOKResponse =
            new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("Direct-Ack-OK"));

        public static readonly IMultiRefReadOnlyByteArray AckRejectResponse =
            new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("Direct-Ack-Reject"));
    }
}
