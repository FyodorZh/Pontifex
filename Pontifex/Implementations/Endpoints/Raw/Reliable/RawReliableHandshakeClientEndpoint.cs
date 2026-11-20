using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <inheritdoc cref="IRawReliableClientEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Handler callbacks</b>: a connected endpoint delivers <c>OnConnected</c>, then one
    /// <c>OnDisconnected</c>, then one <c>OnStopped</c>. An endpoint that never reaches <c>OnConnected</c>
    /// delivers exactly one <c>OnStopped</c> and no <c>OnDisconnected</c>. The logical connection is owned by
    /// the reliable transport layer; this endpoint reports it to the handler.</description></item>
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableHandshakeClientEndpoint : RawReliableClientEndpoint, IRawReliableClientEndpointImp
    {
        private readonly IRawReliableClientHandler _handler;

        /// <summary>
        /// Initializes a new reliable client endpoint with the given transport name, logging,
        /// and memory rental, and an optional keep-alive configuration.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives the connection lifecycle notifications and inbound
        /// messages.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <param name="pingOptions">The keep-alive configuration, or <see langword="null"/> to disable
        /// keep-alive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint. The
        /// endpoint starts in the constructed stage.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableHandshakeClientEndpoint(string transportName, IEndPoint remoteEndPoint,
            IRawReliableClientHandler handler, int messageMaxByteSize, long bufferCapacityBytes,
            IMemoryRental memory, ILogger log, RawReliableClientEndpoint.PingOptions? pingOptions)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log, pingOptions)
        {
            _handler = handler;
        }

        /// <summary>
        /// Notifies the handler of the logical connection and starts the inherited keep-alive behavior. When the
        /// handler throws, the endpoint is stopped with an <see cref="ExceptionFail"/> and keep-alive is not
        /// started.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The handler notification runs before the base implementation
        /// so the handler observes a usable endpoint. Dispatching stop notification and disconnect notification
        /// is once-only; a handler exception does not suppress them.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            if (!NotifyConnectedCore(() => _handler.OnConnected(this)))
            {
                return;
            }

            base.OnConnected();
        }

        /// <summary>
        /// Handles a fatal endpoint failure by notifying the handler of the logical disconnection when the
        /// endpoint was connected, then invoking the base implementation.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked from the serial action queue's error
        /// handling.</description></item>
        /// <item><description><b>Other notes</b>: The disconnect notification is once-only and runs before the
        /// terminal stop notification, so IsConnected is false during it. The base implementation delivers the
        /// endpoint's single terminal handler stop notification; the exact situations are documented on
        /// <see cref="RawReliableEndpoint"/>.</description></item>
        /// </list>
        /// </remarks>
        protected override void FailHard(StopReason reason)
        {
            NotifyDisconnectedCore(reason);
            base.FailHard(reason);
        }

        /// <summary>
        /// Notifies the handler of the logical disconnection when the endpoint was connected.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint indicating whether a graceful disconnection should be
        /// attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: This is a partial hook: it does not complete the stop
        /// transition. The concrete transport must override BeginDisconnect, call this base implementation, and
        /// then call FinishDisconnect to complete the stop. The disconnect notification is once-only and
        /// suppressed when the endpoint never reached OnConnected.</description></item>
        /// </list>
        /// </remarks>
        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            NotifyDisconnectedCore(reason);
        }
    }
}
