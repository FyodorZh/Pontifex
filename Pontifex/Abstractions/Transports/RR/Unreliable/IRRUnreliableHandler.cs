namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Common lifecycle callbacks of an RR unreliable server handler.
    /// </summary>
    public interface IRRUnreliableHandler : IHandler
    {
        /// <summary>
        /// Called once when the transport and its carrier are ready. It is invoked before
        /// <see cref="ITransport.Start"/> returns <see langword="true"/> and before any request
        /// callback.
        /// </summary>
        void Started();

        /// <summary>
        /// Called once when the transport stops, before the transport's <c>onStopped</c> callback.
        /// </summary>
        void Stopped();
    }
}
