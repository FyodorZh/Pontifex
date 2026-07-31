using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable
{
    /// <summary>
    /// Server-side handler for the ACK raw transport connection.
    /// Controls a single client session. Provides business logic for ACK handshake and connection events.
    /// </summary>
    public interface IAckRawReliableServerHandler : IAckRawServerHandler
    {
        /// <summary>
        /// Logical connect. Informs business logic that the transport is fully configured
        /// and ready for use. Indicates successful client connection to the server
        /// (a session is created on the server side).
        /// </summary>
        /// <param name="endPoint">The endpoint to the remote agent.</param>
        void OnConnected(IAckRawReliableServerSideEndpoint endPoint);
    }
}
