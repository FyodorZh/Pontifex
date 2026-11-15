using System;

namespace Pontifex.Raw
{
    /// <summary>
    /// Raw endpoint contract that adds the one-shot start and stop lifecycle to the raw endpoint surface:
    /// thread-safe outbound admission and serialization, inbound dispatch to the handler, and a single
    /// terminal stopped notification.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread.
    /// State transitions and handler dispatch are serialized; outbound messages are transmitted in admission
    /// order.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The lifecycle is one-shot. A stop request is
    /// latched, and after the endpoint has stopped it cannot be started again.</description></item>
    /// <item><description><b>Other notes</b>: A failure that cannot be reported synchronously stops the
    /// endpoint and raises the terminal stopped notification on every path, so an owner can always release the
    /// endpoint.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawEndpointImp : IRawEndpoint
    {
        /// <summary>
        /// Gets a value indicating whether the endpoint is running and accepting new work, meaning it has
        /// started and no stop has been requested.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Becomes false as soon as a stop is requested, even while work
        /// already accepted may still be draining. Implementations expose transport-specific validity getters
        /// in terms of this property.</description></item>
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
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the transition is
        /// serialized.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: May be called at most once, before any stop.
        /// A redundant call fails the endpoint hard.</description></item>
        /// <item><description><b>Other notes</b>: Observe <see cref="IsRunning"/> to learn when the endpoint is
        /// actually running.</description></item>
        /// </list>
        /// </remarks>
        bool Start();

        /// <summary>
        /// Requests a stop of the endpoint and latches the given reason. Only the first call performs the
        /// transition; later calls do nothing.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint forwarded to the implementation indicating whether a graceful stop
        /// should be attempted; the endpoint does not interpret it beyond passing it on.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including concurrently with
        /// other stop calls.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The endpoint stops accepting new work
        /// immediately, while work already accepted may still drain. Stopping an endpoint that was never
        /// started moves it directly to stopped; stopping one that is still starting defers the transition
        /// until the start attempt completes. A stopped endpoint cannot be started again.</description></item>
        /// <item><description><b>Other notes</b>: The reason becomes the endpoint's stop reason. If the
        /// endpoint had started, the reason is supplied to the handler.</description></item>
        /// </list>
        /// </remarks>
        void Stop(StopReason reason, bool tryBeGentle);

        /// <summary>
        /// Raised exactly once when the endpoint reaches the terminal stopped stage.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Raised serialized with state transitions; observers must
        /// not block. Subscriber exceptions are caught and logged and cannot disrupt the terminal
        /// transition.</description></item>
        /// <item><description><b>Other notes</b>: The handler's stop notification is delivered only when the
        /// endpoint had started; this event is raised on every path that reaches the terminal stopped stage,
        /// including endpoints that never started, so a transport can always detach. It is transport-level
        /// observability and does not replace the handler lifecycle callbacks.</description></item>
        /// </list>
        /// </remarks>
        event Action<StopReason>? Stopped;
    }
}
