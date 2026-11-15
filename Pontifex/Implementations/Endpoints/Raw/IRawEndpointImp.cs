using System;

namespace Pontifex.Raw
{
    /// <summary>
    /// Implementation contract for a raw endpoint that is driven through the <see cref="RawEndpoint"/>-style
    /// lifecycle: a one-shot start, an idempotent stop request, and a single terminal stopped notification.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members must be safe to call concurrently from any
    /// thread.</description></item>
    /// <item><description><b>Other notes</b>: This interface exists so transports can hold and drive an
    /// endpoint without depending on a concrete implementation. Implementations are still expected to derive
    /// from the shipped endpoint base classes, which supply the actual state machine.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawEndpointImp : IRawEndpoint
    {
        /// <summary>
        /// Gets a value indicating whether the endpoint is running and accepting new work.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Must become false as soon as a stop is requested, even while
        /// accepted work is still draining.</description></item>
        /// </list>
        /// </remarks>
        bool IsRunning { get; }

        /// <summary>
        /// Requests a start of the endpoint.
        /// </summary>
        /// <returns><see langword="true"/> when the start request was accepted for processing; otherwise
        /// <see langword="false"/>. A successful return means the request was queued, not that the endpoint has
        /// started.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: May be called at most once, before any
        /// stop.</description></item>
        /// </list>
        /// </remarks>
        bool Start();

        /// <summary>
        /// Requests a stop of the endpoint.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint indicating whether a graceful stop should be
        /// attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: A repeated call is ignored; only the first reason is
        /// retained. The endpoint stops accepting new work immediately while work already accepted may still
        /// drain.</description></item>
        /// </list>
        /// </remarks>
        void Stop(StopReason reason, bool tryBeGentle);

        /// <summary>
        /// Raised exactly once when the endpoint reaches the terminal stopped stage.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Raised on the endpoint's serial action queue; observers
        /// must not block.</description></item>
        /// <item><description><b>Other notes</b>: Also raised for endpoints that were never started, so a
        /// transport can always detach. Transport bookkeeping relies on exactly-once delivery.</description></item>
        /// </list>
        /// </remarks>
        event Action<StopReason>? Stopped;
    }
}
