namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableHandler : IRawHandler
    {
        /// <summary>
        /// Called exactly once for this handler's endpoint, after that endpoint's start completes and before
        /// any <see cref="IRawHandler.OnReceived"/> for it.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Timing</b>: For a client the notification follows a successful client transport
        /// start. For a server it follows the acceptance of a source route, and the triggering message is
        /// delivered only after this callback returns successfully.</description></item>
        /// <item><description><b>Other notes</b>: The notification is not raised when the endpoint is stopped
        /// before its start completes. If it throws, the implementation catches and logs the exception and stops
        /// the endpoint; that handler does not receive <see cref="IRawHandler.OnStopped"/> for the failed
        /// endpoint.</description></item>
        /// <item><description><b>Thread safety</b>: Callbacks for one endpoint are serialized and non-reentrant;
        /// callbacks for different server endpoints may run concurrently.</description></item>
        /// </list>
        /// </remarks>
        void OnStarted(IRawUnreliableEndpoint endpoint);
    }
}
