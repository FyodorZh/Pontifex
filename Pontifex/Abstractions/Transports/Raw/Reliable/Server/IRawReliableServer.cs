namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) raw server transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableServer : IRawReliableTransport
    {
        /// <summary>
        /// Initializes the server transport with the given handler factory.
        /// The factory decides whether an incoming connection is admitted and creates
        /// a per-client session handler for every admitted connection.
        /// </summary>
        /// <param name="factory">The factory that validates and creates client sessions.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory);
    }
}
