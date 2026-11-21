using System;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Server half of the RR reliable contract: receives requests from clients and lets application
    /// logic answer each one with zero or more responses and an explicit completion.
    /// </summary>
    public interface IRRReliableServerTransport : IRRReliableTransport
    {
        /// <summary>
        /// Gets the maximum time a response stream may remain open after
        /// <see cref="IRRReliableServerHandler.OnRequested"/> accepts the request.
        /// </summary>
        TimeSpan ResponseTtl { get; }

        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the request callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise
        /// <see langword="false"/>.</returns>
        bool Init(IRRReliableServerHandler handler);
    }
}
