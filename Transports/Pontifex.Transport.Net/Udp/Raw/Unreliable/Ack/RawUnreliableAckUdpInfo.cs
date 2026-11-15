namespace Pontifex.Raw.Unreliable.Ack.Udp
{
    internal static class RawUnreliableAckUdpInfo
    {
        public const string TransportName = "udp-raw-unreliable-ack";
        public const int MessageMaxByteSize = 490;
        public const int QueueCapacity = 100;

        /// <summary>
        /// The byte cap of the shared server send budget and of each endpoint's local outbound budget.
        /// </summary>
        public const long SendBudgetBytes = (long)MessageMaxByteSize * QueueCapacity;

        /// <summary>
        /// The message cap of the shared server send budget and of each endpoint's pre-start inbox.
        /// </summary>
        public const int SendBudgetMessages = QueueCapacity;
    }
}
