namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// The discriminator of a reliable-wrapper negotiation envelope.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The status is always the third element of the flat envelope, so a
    /// custom layer element (an <c>int</c> identifier, for example) can never be mistaken for a status.
    /// </description></item>
    /// </list>
    /// </remarks>
    internal enum NegotiationStatus : byte
    {
        /// <summary>
        /// The client's negotiation request.
        /// </summary>
        Request = 0,

        /// <summary>
        /// The server accepted the negotiation request.
        /// </summary>
        Accepted,

        /// <summary>
        /// The server rejected the negotiation request.
        /// </summary>
        Rejected
    }
}
