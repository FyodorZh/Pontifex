namespace Pontifex.Raw.Unreliable.NoAck.Direct
{
    internal static class RawUnreliableNoAckDirectInfo
    {
        public const string TransportName = "direct-raw-unreliable-noack";
        public const int MessageMaxByteSize = 1024 * 1024;
        public const int QueueCapacity = 100;
    }
}
