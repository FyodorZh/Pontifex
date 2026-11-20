using System.Threading;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Session handler gate for the reliable server transport. It forwards every callback to the handler
    /// created by the handler factory and guarantees that the handler observes at most one
    /// <see cref="OnStopped"/> notification, whichever terminal path reports it first.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Forwarding is not synchronized, matching the handler
    /// contract that callbacks for one session never overlap. Only the stop latch is atomic.</description></item>
    /// <item><description><b>Other notes</b>: The gate is the instance handed to the carrier and to the
    /// endpoint, so the endpoint's own terminal notification and the transport's admission-failure
    /// notification share one latch. There is no separate notification entry point: the transport calls
    /// <see cref="OnStopped"/> through the interface and the gate deduplicates.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class StopOnceServerHandler : IRawReliableServerHandler
    {
        private int _stopped;

        /// <summary>
        /// Initializes a new gate around the handler created by the factory.
        /// </summary>
        /// <param name="inner">The handler created by the factory. Must be non-null.</param>
        public StopOnceServerHandler(IRawReliableServerHandler inner)
        {
            Inner = inner;
        }

        /// <summary>
        /// Gets the wrapped factory handler.
        /// </summary>
        public IRawReliableServerHandler Inner { get; }

        /// <inheritdoc/>
        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Inner.OnConnected(endPoint);
        }

        /// <inheritdoc/>
        public void OnReceived(UnionDataList receivedBuffer)
        {
            Inner.OnReceived(receivedBuffer);
        }

        /// <inheritdoc/>
        public void OnDisconnected(StopReason reason)
        {
            Inner.OnDisconnected(reason);
        }

        /// <inheritdoc/>
        public void OnStopped(StopReason reason)
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0)
            {
                Inner.OnStopped(reason);
            }
        }
    }
}
