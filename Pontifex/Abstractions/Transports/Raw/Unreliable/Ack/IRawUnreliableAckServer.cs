using System;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Ack
{
    public interface IRawUnreliableAckServer : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the server with a source-route handler factory before Start.
        /// The factory receives the inbound source IEndPoint and the triggering
        /// UnionDataList message, and returns a handler, or null to decline that
        /// message. The triggering message remains owned by the transport: it is
        /// released on decline and delivered via handler.OnReceived after a
        /// successful handler.OnStarted. One-time operation. Returns false when
        /// not eligible. Throws ArgumentNullException on null.
        /// </summary>
        bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory);
    }
}
