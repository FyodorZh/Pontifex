namespace Pontifex
{
    /// <summary>
    /// Represents the result of a data transmission attempt.
    /// </summary>
    public enum SendResult : byte
    {
        /// <summary>
        /// Data transmission was successful, but the delivery status is unknown.
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
        /// The connection is not established or has been disconnected.
        /// </summary>
        NotConnected,

        /// <summary>
        /// The send buffer is full.
        /// </summary>
        BufferOverflow,

        /// <summary>
        /// Any unclassified error.
        /// </summary>
        Error
    }
}
