namespace Pontifex.Raw.Unreliable.NoAck
{
    public interface IRawUnreliableNoAckClientConformanceControl : IRawUnreliableNoAckConformanceControl
    {
        /// <summary>
        /// Attempts to make this client-server link reliable in both directions.
        /// When it returns true, every message accepted with SendResult.Ok while
        /// both endpoints are running is delivered exactly once in FIFO operation
        /// order for that direction. This method must be called before starting the
        /// client; calling it after startup begins is unsupported.
        /// </summary>
        /// <returns>False if the implementation cannot provide reliable debug mode.</returns>
        bool TryMakeReliable();
    }
}
