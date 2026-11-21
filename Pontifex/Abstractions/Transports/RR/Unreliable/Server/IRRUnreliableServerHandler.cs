using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Application callbacks for one RR unreliable server.
    /// </summary>
    public interface IRRUnreliableServerHandler : IHandler
    {
        /// <summary>
        /// Called once when the server starts.
        /// </summary>
        void Started();

        /// <summary>
        /// Called once when the server stops.
        /// </summary>
        void Stopped();

        /// <summary>
        /// Called for every request received from a client.
        /// </summary>
        /// <param name="client">The route of the client that sent the request.</param>
        /// <param name="message">The received message. The handler owns the reference.</param>
        void OnRequest(IEndPoint client, UnionDataList message);
    }
}
