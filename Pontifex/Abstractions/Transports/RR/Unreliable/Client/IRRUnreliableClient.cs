namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Client half of the RR unreliable contract: sends messages to the configured server and receives
    /// the messages that the server sends back.
    /// </summary>
    public interface IRRUnreliableClient : ITransport
    {
        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the client callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise <see langword="false"/>.</returns>
        bool Init(IRRUnreliableClientHandler handler);

        /// <summary>
        /// Inclusive maximum serialized size of one message, in bytes.
        /// </summary>
        int MessageMaxByteSize { get; }
    }
}
