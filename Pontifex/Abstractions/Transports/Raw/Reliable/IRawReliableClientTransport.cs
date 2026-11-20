namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableClientTransport<in TClientHandler> : IRawReliableTransport
        where TClientHandler : class, IRawReliableClientHandler
    {
        /// <summary>
        /// Initializes the client transport with the user-provided handler.
        /// </summary>
        /// <param name="handler">The handler that processes transport events.</param>
        /// <returns>True if initialization was successful.</returns>
        bool Init(TClientHandler handler);
    }
}