namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) no-acknowledgement raw server transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableNoAckServer : IRawReliableTransport
    {
        /// <summary>
        /// Initializes the server transport with the given handler factory.
        /// The factory decides whether an incoming connection is admitted and creates
        /// a per-client session handler for every admitted connection.
        /// </summary>
        /// <param name="factory">The factory that validates and creates client sessions.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory);
    }
}
