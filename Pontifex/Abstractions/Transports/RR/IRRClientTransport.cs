using System;

namespace Pontifex.RR
{
    /// <summary>
    /// Client half shared by the RR families: a fixed server route and a request TTL.
    /// </summary>
    public interface IRRClientTransport : IRRTransport
    {
        /// <summary>
        /// Gets the fixed route of the server this client sends to.
        /// </summary>
        IEndPoint ServerEndPoint { get; }

        /// <summary>
        /// Gets the maximum time a request may remain pending, counted from the moment the request was
        /// accepted (the request call returned <see cref="SendResult.Ok"/>). The time spent inside the
        /// call is not counted.
        /// </summary>
        TimeSpan RequestTtl { get; }
    }
}
