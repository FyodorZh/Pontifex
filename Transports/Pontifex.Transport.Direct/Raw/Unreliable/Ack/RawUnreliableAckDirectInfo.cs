namespace Pontifex.Raw.Unreliable.Ack.Direct
{
    internal static class RawUnreliableAckDirectInfo
    {
        public const string TransportName = "direct-raw-unreliable-ack";
        public const int MessageMaxByteSize = 1024 * 1024;
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
