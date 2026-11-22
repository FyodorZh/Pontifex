using System;

namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableServer : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the server with a source-route handler factory before Start.
        /// </summary>
        /// <param name="handlerFactory">The factory for a new source route. It receives the inbound source
        /// <see cref="IEndPoint"/> and returns the handler for that route, or <see langword="null"/> to decline
        /// the triggering message. It never receives the triggering message. Must not be
        /// <see langword="null"/>.</param>
        /// <returns><see langword="true"/> for the first eligible call; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: One-time and pre-start. A second call, a call
        /// after a start attempt, and a call on an invalid transport return <see langword="false"/> without
        /// changing the first factory or invalidating the transport.</description></item>
        /// <item><description><b>Factory semantics</b>: Invoked once per new source route, serialized with every
        /// other invocation. A <see langword="null"/> result declines the triggering message, which the transport
        /// releases; the decline is not cached, so a later message from the same source is eligible to invoke the
        /// factory again.</description></item>
        /// <item><description><b>Exceptions</b>: A <see langword="null"/> factory throws
        /// <see cref="ArgumentNullException"/> and leaves the transport unchanged.</description></item>
        /// </list>
        /// </remarks>
        bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory);
    }
}
