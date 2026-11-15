using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Implementation contract for an unreliable raw endpoint. Extends the unreliable endpoint surface with
    /// the inbound delivery entry point used by the owning transport's carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread.
    /// Inbound delivery is serialized with the start flush and the stop transition, and the endpoint owns a
    /// capped pre-start inbox.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The lifecycle is one-shot. Messages delivered
    /// before the start completes are buffered; messages delivered after the endpoint is invalid are
    /// released.</description></item>
    /// <item><description><b>Other notes</b>: The transport observes <see cref="IRawEndpointImp.Stopped"/> to
    /// detach the endpoint. A handler's stop notification is forwarded only when its start notification
    /// completed successfully.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawUnreliableEndpointImp : IRawEndpointImp, IRawUnreliableEndpoint
    {
        /// <summary>
        /// Delivers one received message to this endpoint.
        /// </summary>
        /// <param name="message">The received message. Ownership transfers to the endpoint on every
        /// path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any number of threads. Ordering with
        /// respect to the start flush and the stop transition is guaranteed: a message delivered before the
        /// flush is enqueued behind every already buffered message, including a server trigger.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message must not be
        /// <see langword="null"/>. It is released exactly once when the endpoint is invalid or when a capped
        /// inbox drops it.</description></item>
        /// <item><description><b>Other notes</b>: While the endpoint has not flushed its inbox the message is
        /// buffered, subject to the message and byte caps; excess is dropped silently, without logging. Once
        /// the inbox is open the message is scheduled directly through the endpoint's inbound
        /// path.</description></item>
        /// </list>
        /// </remarks>
        void Deliver(UnionDataList message);
    }
}
