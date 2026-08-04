namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableClientHandler : IRawReliableHandler
    {
        /// <summary>
        /// Called when the client-server connection is finally destroyed.
        /// If OnConnected() was previously triggered, the call sequence will be:
        /// OnDisconnected() followed by OnStopped().
        /// If OnConnected() was never triggered, only OnStopped() is called.
        /// </summary>
        void OnStopped(StopReason reason);
    }
}