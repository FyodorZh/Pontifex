using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableEndpoint : IRawEndpoint
    {
        /// <summary>
        /// Gets whether the endpoint is currently valid and may accept sends.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The value tracks the endpoint's running state. It becomes
        /// <see langword="false"/> as soon as a stop is requested, even though work already accepted by the
        /// endpoint may still drain. A never-started endpoint is not valid; it becomes valid when its start
        /// completes, and it never becomes valid again.</description></item>
        /// </list>
        /// </remarks>
        bool IsValid { get; }

        /// <summary>
        /// Requests a one-shot stop of this endpoint.
        /// </summary>
        /// <param name="reason">The reason delivered to <c>OnStopped</c> and to the endpoint's terminal
        /// subscribers. <see langword="null"/> is replaced with a transport-generated
        /// <see cref="Pontifex.StopReasons.Unknown"/>.</param>
        /// <returns><see langword="true"/> for the call that takes the one-shot latch; <see langword="false"/>
        /// for every later call, including a call on an endpoint that is already invalid.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including from inside one of
        /// this endpoint's callbacks.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: There is no running-state precondition, so
        /// stopping a never-started or still-starting endpoint is supported; the resulting transition releases
        /// that endpoint's inbox and, for a server session, removes its route.</description></item>
        /// <item><description><b>Other notes</b>: The latch and <see cref="IsValid"/> turn over immediately,
        /// before the stop transition completes. The call is a request, not a completion signal: it may execute
        /// the transition on the caller's thread and can therefore block until an already-armed transport-level
        /// gate is released.</description></item>
        /// </list>
        /// </remarks>
        bool Stop(StopReason? reason = null);
    }
}
