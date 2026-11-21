using System;
using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Prepends and removes the request-id header of an RR unreliable frame.
    /// </summary>
    internal static class RRUnreliableFrame
    {
        /// <summary>
        /// Serialized size of the header: the long request id is stored as one type byte and eight value
        /// bytes.
        /// </summary>
        public const int OverheadBytes = 9;

        /// <summary>
        /// Prepends <paramref name="requestId"/> to <paramref name="message"/>.
        /// </summary>
        public static void Encode(UnionDataList message, long requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.PutFirst(new UnionData(requestId));
        }

        /// <summary>
        /// Removes the request-id header from <paramref name="message"/>.
        /// </summary>
        /// <returns><see langword="true"/> when the frame starts with a request id; otherwise
        /// <see langword="false"/>.</returns>
        public static bool TryDecode(UnionDataList message, out long requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return message.TryPopFirst(out requestId);
        }
    }
}
