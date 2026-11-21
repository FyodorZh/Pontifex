namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// How a reliable request reached its terminal state.
    /// </summary>
    public enum RRReliableCompletionType
    {
        /// <summary>
        /// The server completed the request normally.
        /// </summary>
        Normal,

        /// <summary>
        /// The client cancelled the request.
        /// </summary>
        Canceled,

        /// <summary>
        /// The server rejected the request.
        /// </summary>
        Rejected,

        /// <summary>
        /// The request did not complete normally for any other reason, for example a transport
        /// failure or a stop.
        /// </summary>
        Failed
    }
}
