namespace Pontifex.Raw.Unreliable.Ack
{
    /// <summary>
    /// Server-side transport for an ACK-based raw connection.
    /// Initializes the server with an acknowledger that validates and creates sessions
    /// for incoming clients.
    /// Implemented by the transport system.
    /// </summary>
    public interface IRawAckServer : ITransport
    {
        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}