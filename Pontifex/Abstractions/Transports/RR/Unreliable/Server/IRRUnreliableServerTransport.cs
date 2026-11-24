namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server half of the RR unreliable contract: receives requests from clients and sends matched
    /// response messages back to their source.
    /// </summary>
    public interface IRRUnreliableServerTransport : IRRServerTransport
    {
        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the request callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise <see langword="false"/>.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>.</exception>
        bool Init(IRRUnreliableServerHandler handler);
    }
}
