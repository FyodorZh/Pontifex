namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    internal static class RawUnreliableAckDirectInfo
    {
        public const string TransportName = "direct-raw-unreliable-ack";
        public const int MessageMaxByteSize = 1024 * 1024;
        public const int QueueCapacity = 100;
    }
}
