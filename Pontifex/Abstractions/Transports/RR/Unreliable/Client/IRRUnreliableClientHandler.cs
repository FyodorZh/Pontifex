using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Application callbacks for one RR unreliable client.
    /// </summary>
    public interface IRRUnreliableClientHandler : IHandler
    {
        /// <summary>
        /// Called once the client endpoint is ready. The endpoint is used to send messages to the server.
        /// </summary>
        /// <param name="endpoint">The client-side handle bound to the configured server.</param>
        void Started(IRRUnreliableServerEndpoint endpoint);

        /// <summary>
        /// Called for every message sent by the server to this client.
        /// </summary>
        /// <param name="message">The received message. The handler owns the reference.</param>
        void Received(UnionDataList message);

        /// <summary>
        /// Called once when the client stops.
        /// </summary>
        void Stopped();
    }
}
