namespace Pontifex.Raw.Unreliable.Ack
{
    public interface IRawAckBaseEndpoint : IBaseEndpoint
    {
        /// <summary>
        /// Gets the remote endpoint address, or null if not connected.
        /// </summary>
        IEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Gets whether the endpoint is currently connected.
        /// May return true before OnConnected() is invoked (the endpoint reference
        /// is not available to the caller before OnConnected()).
        /// Guaranteed to return false during and after OnDisconnected() — the
        /// IsConnected transition to false is synchronized with the OnDisconnected() call.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Disconnect(StopReason reason);
    }
}