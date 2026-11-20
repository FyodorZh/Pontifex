using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Protocols
{
    /// <summary>
    /// The protocol-specific half of a reliable wrapper: data transformation plus the wrapper's negotiation
    /// dataset.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Negotiation hooks</b>: the hooks receive the custom tail that follows
    /// <c>version</c>, <c>name</c>, and <c>status</c> in the flat negotiation envelope. The session owns every
    /// buffer it passes to a hook and releases it once after the hook returns; hooks must neither retain nor
    /// release it. The fill hook is append-only.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableWrapperLogic
    {
        /// <summary>
        /// Called once after the wrapper negotiation is admitted.
        /// </summary>
        void OnConnected();

        /// <summary>
        /// Called once when an admitted wrapper session is disconnected.
        /// </summary>
        void OnDisconnected();

        /// <summary>
        /// Transforms one inbound application payload in place. Returns <see langword="false"/> when the payload
        /// is rejected, which terminates the session.
        /// </summary>
        bool ProcessReceivedData(UnionDataList receivedData);

        /// <summary>
        /// Transforms one outbound application payload in place. Returns <see langword="false"/> when the
        /// payload is rejected, which fails only that send.
        /// </summary>
        bool ProcessSentData(UnionDataList sentData);

        /// <summary>
        /// Adds the wrapper's controls to <paramref name="dst"/>.
        /// </summary>
        void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);

        /// <summary>
        /// Appends the client's custom negotiation data. Append-only: the hook must not clear or release the
        /// buffer.
        /// </summary>
        void FillNegotiationData(UnionDataList customData);

        /// <summary>
        /// Observes the server's custom negotiation data. The session releases the buffer after the hook
        /// returns.
        /// </summary>
        void OnNegotiationAccepted(UnionDataList customData);

        /// <summary>
        /// Builds the server's custom negotiation response tail. The session owns and releases the returned
        /// list; the hook must not retain it.
        /// </summary>
        UnionDataList CreateNegotiationResponse();

        /// <summary>
        /// Validates the client's custom negotiation request tail. Returning <see langword="false"/> rejects
        /// the connection.
        /// </summary>
        bool CheckNegotiationData(UnionDataList customData);
    }
}
