using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Client half of the RR unreliable contract: sends requests to a fixed server and receives the
    /// responses that are matched to them by request id.
    /// </summary>
    public interface IRRUnreliableClientTransport : ITransport
    {
        /// <summary>
        /// Gets the fixed route of the server this client sends to.
        /// </summary>
        IEndPoint ServerEndPoint { get; }

        /// <summary>
        /// Inclusive maximum payload size of one message, in bytes. Transport framing is not included.
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the response callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise <see langword="false"/>.</returns>
        bool Init(IRRUnreliableClientHandler handler);

        /// <summary>
        /// Sends <paramref name="request"/> to the server and returns the id that matches its response.
        /// </summary>
        /// <param name="request">The request payload. The transport owns it after the call.</param>
        /// <param name="requestId">The client-local id of the request, or <c>-1</c> when the call fails.</param>
        /// <returns>The result of the send attempt.</returns>
        SendResult Request(UnionDataList request, out long requestId);
    }
}
