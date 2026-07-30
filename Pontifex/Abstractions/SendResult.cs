namespace Pontifex
{
    /// <summary>
    /// Represents the result of a data transmission attempt.
    /// </summary>
    public enum SendResult : byte
    {
        /// <summary>
        /// The transport accepted the message for local processing. It does not
        /// guarantee carrier submission, peer receipt, or delivery.
        /// </summary>
        Ok,

        /// <summary>
        /// The actual size of the data being sent exceeds the maximum allowed value.
        /// </summary>
        MessageTooBig,

        /// <summary>
        /// The data being sent is invalid: null or malformed.
        /// </summary>
        InvalidMessage,

        /// <summary>
        /// The destination address is invalid.
        /// </summary>
        InvalidAddress,

        /// <summary>
        /// A connection-oriented transport is not connected. Connectionless
        /// transport contracts may reserve this value and use a different result.
        /// </summary>
        NotConnected,

        /// <summary>
        /// The send buffer is full.
        /// </summary>
        BufferOverflow,

        /// <summary>
        /// An unclassified synchronous error. A transport contract may also use
        /// this result for a transport-specific unavailable state.
        /// </summary>
        Error
    }
}
