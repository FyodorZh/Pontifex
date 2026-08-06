namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableHandler : IRawHandler
    {
        /// <summary>
        /// Called once when this handler's endpoint has been created and is valid.
        /// For a client, this occurs after the client transport starts. For a server,
        /// this occurs after a source route is accepted.
        /// </summary>
        void OnStarted(IRawUnreliableEndpoint endpoint);

        /// <summary>
        /// Called exactly once after the endpoint becomes invalid, provided
        /// OnStarted completed successfully. No OnReceived will begin after this.
        /// </summary>
        void OnStopped(StopReason reason);
    }
}
