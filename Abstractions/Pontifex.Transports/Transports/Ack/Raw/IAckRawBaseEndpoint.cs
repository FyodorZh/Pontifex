using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Represents one side of a raw ACK-based connection.
    /// Provides send/disconnect capabilities and metadata about the remote peer.
    /// Implemented by the transport system; consumed by business logic.
    /// </summary>
    public interface IAckRawBaseEndpoint : IBaseEndpoint
    {
        /// <summary>
        /// Gets the remote endpoint address, or null if not connected.
        /// </summary>
        IEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Gets whether the endpoint is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Sends a message to the remote peer.
        /// If a failure occurs asynchronously after the method returns success,
        /// the transport will be destroyed and OnDisconnected will be raised.
        /// </summary>
        /// <param name="bufferToSend">The data to send.</param>
        /// <returns>SendResult.Ok on success; other values indicate a synchronous failure.</returns>
        SendResult Send(UnionDataList bufferToSend);

        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Disconnect(StopReason reason);
    }
}