using System;
using Pontifex.Raw.Unreliable;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public interface IRawUnreliableNoAckServer : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the server with a source-route handler factory before Start.
        /// The factory receives the inbound source IEndPoint and returns a handler,
        /// or null to decline that message. One-time operation. Returns false when
        /// not eligible. Throws ArgumentNullException on null.
        /// </summary>
        bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory);
    }
}
