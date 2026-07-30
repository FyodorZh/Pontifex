namespace Pontifex.NoAck.Raw.Unreliable
{
    public interface INoAckRawUnreliableClientConformanceControl : INoAckRawUnreliableConformanceControl
    {
        /// <summary>
        /// Attempt to make this transport reliable. If success, then no message will be lost, reordered or duplicated.
        /// This method must be called before starting the transport.
        /// </summary>
        /// <returns> False if transport can't be put in reliable mode </returns>
        bool TryMakeReliable();

    }
}