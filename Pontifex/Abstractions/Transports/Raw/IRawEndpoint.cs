using Pontifex.Utils;

namespace Pontifex.Raw
{
    public interface IRawEndpoint : IBaseEndpoint
    {        
        /// <summary>
        /// Gets the remote endpoint address, or null if not connected.
        /// Safe to read concurrently.
        /// </summary>
        IEndPoint? RemoteEndPoint { get; }
        
        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport.
        /// An inclusive maximum for the application payload; it excludes transport
        /// framing and control metadata. Empty payloads are valid.
        /// Safe to read concurrently.
        /// </summary>
        int MessageMaxByteSize { get; }
        
        /// <summary>
        /// Sends a message to the remote peer.
        /// Thread-safe. Concurrent successful sends on one endpoint are ordered by
        /// the transport's linearization order for those calls. Send does not wait
        /// for network delivery or peer handling; it returns after validation and
        /// outbound admission.
        /// Ownership of <paramref name="bufferToSend"/> transfers to the transport
        /// unconditionally, regardless of the returned SendResult. The caller MUST
        /// NOT read, mutate, retain, release, or retry using that buffer afterward.
        /// </summary>
        /// <remarks>
        /// <para><b>Reliable behaviour:</b>
        /// Synchronous failures (e.g., buffer full, message too big, not connected)
        /// are returned as a non-Ok SendResult and do NOT affect the connection.
        /// If a failure occurs asynchronously after the method returns Ok,
        /// the transport will be destroyed and OnDisconnected will be raised.
        /// </para>
        /// <para><b>Unreliable behaviour:</b>
        /// Success indicates local acceptance only; actual delivery is not verifiable
        /// and loss/reorder/duplication are possible. An unreliable endpoint is valid
        /// for sending while IsValid is true; RawUnreliable never returns NotConnected.
        /// </para>
        /// </remarks>
        /// <param name="bufferToSend">The data to send.</param>
        /// <returns>SendResult.Ok on success; other values indicate a synchronous failure.</returns>
        SendResult Send(UnionDataList bufferToSend);
    }
}