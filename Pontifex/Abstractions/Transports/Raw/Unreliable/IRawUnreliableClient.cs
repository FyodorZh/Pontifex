namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableClient : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the client with the application handler before Start.
        /// </summary>
        /// <param name="handler">The handler that receives the client endpoint and all of its callbacks. Must
        /// not be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> for the first eligible call; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: One-time and pre-start. A second call, a call
        /// after a start attempt, and a call on an invalid transport return <see langword="false"/> without
        /// changing the bound handler or invalidating the transport. A successful call neither raises
        /// <c>OnStarted</c> nor starts carrier activity.</description></item>
        /// <item><description><b>Exceptions</b>: A <see langword="null"/> handler throws
        /// <see cref="System.ArgumentNullException"/> and leaves the transport unchanged.</description></item>
        /// </list>
        /// </remarks>
        bool Init(IRawUnreliableHandler handler);
    }
}
