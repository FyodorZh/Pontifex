using System;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Ack
{
    public interface IRawUnreliableAckServer : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the server with a source-route handler factory before Start.
        /// </summary>
        /// <param name="handlerFactory">The factory for a new source route. It receives the inbound source
        /// <see cref="IEndPoint"/> and the triggering message, and returns the handler for that route, or
        /// <see langword="null"/> to decline the triggering message. It must not release the triggering
        /// message: ownership stays with the transport, which releases it on decline and delivers it through
        /// <c>handler.OnReceived</c> after a successful <c>handler.OnStarted</c>. Must not be
        /// <see langword="null"/>.</param>
        /// <returns><see langword="true"/> for the first eligible call; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: One-time and pre-start. A second call, a call
        /// after a start attempt, and a call on an invalid transport return <see langword="false"/> without
        /// changing the first factory or invalidating the transport.</description></item>
        /// <item><description><b>Factory semantics</b>: Invoked once per new source route, serialized with every
        /// other invocation. A <see langword="null"/> result declines only the triggering message; the decline
        /// is not cached, so a later message from the same source is eligible to invoke the factory
        /// again.</description></item>
        /// <item><description><b>Exceptions</b>: A <see langword="null"/> factory throws
        /// <see cref="ArgumentNullException"/> and leaves the transport unchanged.</description></item>
        /// </list>
        /// </remarks>
        bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory);
    }
}
