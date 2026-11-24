using System;
using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Prepends and removes the header of an RR unreliable frame.
    /// </summary>
    /// <remarks>
    /// A request frame carries the client-local request id. A response frame carries the request id,
    /// the zero-based id of the message, and the number of messages in the response.
    /// </remarks>
    internal static class RRUnreliableFrame
    {
        /// <summary>
        /// The largest number of messages one response may contain. The message count travels as a
        /// byte holding the last message id, so <c>256</c> messages map to the byte value <c>255</c>.
        /// </summary>
        public const int MaxMessageCount = 256;

        /// <summary>
        /// Serialized size of the request header: the 16-bit request id is stored as one type byte
        /// and two value bytes.
        /// </summary>
        public const int RequestOverheadBytes = 3;

        /// <summary>
        /// Serialized size of the response header: the 16-bit request id (one type byte and two
        /// value bytes), the message id (one type byte and one value byte), and the last message id
        /// (one type byte and one value byte).
        /// </summary>
        public const int ResponseOverheadBytes = 7;

        /// <summary>
        /// Serialized size of the largest header a frame may carry.
        /// </summary>
        public const int OverheadBytes = ResponseOverheadBytes;

        /// <summary>
        /// Prepends <paramref name="requestId"/> to a request frame.
        /// </summary>
        public static void EncodeRequest(UnionDataList message, ushort requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.PutFirst(new UnionData(requestId));
        }

        /// <summary>
        /// Removes the request-id header from a request frame.
        /// </summary>
        /// <returns><see langword="true"/> when the frame starts with a request id; otherwise
        /// <see langword="false"/>.</returns>
        public static bool TryDecodeRequest(UnionDataList message, out ushort requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return message.TryPopFirst(out requestId);
        }

        /// <summary>
        /// Prepends the response header to <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The response payload.</param>
        /// <param name="requestId">The client-local request id.</param>
        /// <param name="messageId">The zero-based id of this message.</param>
        /// <param name="messageCount">The number of messages in the response, from one to
        /// <see cref="MaxMessageCount"/>.</param>
        public static void EncodeResponse(UnionDataList message, ushort requestId, int messageId,
            int messageCount)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (messageCount < 1 || messageCount > MaxMessageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(messageCount));
            }

            if (messageId < 0 || messageId >= messageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(messageId));
            }

            message.PutFirst(new UnionData((byte)(messageCount - 1)));
            message.PutFirst(new UnionData((byte)messageId));
            message.PutFirst(new UnionData(requestId));
        }

        /// <summary>
        /// Removes the response header from a response frame.
        /// </summary>
        /// <param name="message">The received frame. The caller owns it.</param>
        /// <param name="requestId">The client-local request id.</param>
        /// <param name="messageId">The zero-based id of this message.</param>
        /// <param name="messageCount">The number of messages in the response.</param>
        /// <returns><see langword="true"/> when the frame starts with a complete response header;
        /// otherwise <see langword="false"/>.</returns>
        public static bool TryDecodeResponse(UnionDataList message, out ushort requestId,
            out int messageId, out int messageCount)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            messageId = 0;
            messageCount = 0;

            if (!message.TryPopFirst(out requestId) ||
                !message.TryPopFirst(out byte messageIdValue) ||
                !message.TryPopFirst(out byte lastMessageId))
            {
                return false;
            }

            messageId = messageIdValue;
            messageCount = lastMessageId + 1;
            return true;
        }
    }
}
