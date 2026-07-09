namespace Pontifex.NoAck.Raw_old
{
    public interface INoAckRawClient : ITransport
    {
        /// <summary>
        /// Maximum allowed size of a single message for sending (and receiving)
        /// </summary>
        int MessageMaxByteSize { get; }

        bool Init(INoAckRawClientSideHandler handler);
    }
}