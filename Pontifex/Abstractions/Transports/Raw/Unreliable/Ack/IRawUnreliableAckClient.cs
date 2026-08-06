using Pontifex.Raw.Unreliable;

namespace Pontifex.Raw.Unreliable.Ack
{
    public interface IRawUnreliableAckClient : IRawUnreliableTransport
    {
        /// <summary>
        /// Initializes the client with the user-provided handler before Start.
        /// One-time operation. Returns false when not eligible (already initialized,
        /// started, stopping, or invalid). Throws ArgumentNullException on null.
        /// </summary>
        bool Init(IRawUnreliableHandler handler);
    }
}
