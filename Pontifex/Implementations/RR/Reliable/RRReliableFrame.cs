using System;
using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Kind of a reliable RR frame.
    /// </summary>
    internal enum RRReliableFrameType : byte
    {
        /// <summary>A client request without an ordinal.</summary>
        Request = 1,

        /// <summary>A server response carrying the response ordinal.</summary>
        Response = 2,

        /// <summary>A terminal notification carrying the completion type.</summary>
        Finish = 3,

        /// <summary>A best-effort client cancellation notice.</summary>
        Cancel = 4
    }

    /// <summary>
    /// Decoded header of a reliable RR frame.
    /// </summary>
    internal readonly struct RRReliableFrameHeader
    {
        /// <summary>
        /// Initializes a decoded header.
        /// </summary>
        public RRReliableFrameHeader(RRReliableFrameType frameType, ushort requestId, uint ordinal,
            RRReliableCompletionType completion)
        {
            FrameType = frameType;
            RequestId = requestId;
            Ordinal = ordinal;
            Completion = completion;
        }

        /// <summary>Gets the frame kind.</summary>
        public RRReliableFrameType FrameType { get; }

        /// <summary>Gets the client-local request id.</summary>
        public ushort RequestId { get; }

        /// <summary>Gets the response ordinal, or <c>0</c> for other frame kinds.</summary>
        public uint Ordinal { get; }

        /// <summary>Gets the completion type of a <see cref="RRReliableFrameType.Finish"/> frame.</summary>
        public RRReliableCompletionType Completion { get; }
    }

    /// <summary>
    /// Encodes and decodes the wire header of a reliable RR frame.
    /// </summary>
    /// <remarks>
    /// The header is the type byte followed by the client-local request id. A response appends the
    /// unsigned response ordinal, and a finish appends the completion byte. The payload follows the
    /// header. The methods neither own nor release the frame; the caller owns it on every path.
    /// </remarks>
    internal static class RRReliableFrame
    {
        /// <summary>Serialized size of the frame-type element.</summary>
        public const int TypeBytes = 2;

        /// <summary>Serialized size of the request-id element.</summary>
        public const int RequestIdBytes = 3;

        /// <summary>Serialized size of the response ordinal element.</summary>
        public const int OrdinalBytes = 5;

        /// <summary>Serialized size of the completion element.</summary>
        public const int CompletionBytes = 2;

        /// <summary>Serialized size of a request header.</summary>
        public const int RequestOverheadBytes = TypeBytes + RequestIdBytes;

        /// <summary>Serialized size of a response header.</summary>
        public const int ResponseOverheadBytes = TypeBytes + RequestIdBytes + OrdinalBytes;

        /// <summary>Serialized size of a finish header.</summary>
        public const int FinishOverheadBytes = TypeBytes + RequestIdBytes + CompletionBytes;

        /// <summary>Serialized size of a cancel header.</summary>
        public const int CancelOverheadBytes = TypeBytes + RequestIdBytes;

        /// <summary>Serialized size of the largest header, which a response owns.</summary>
        public const int MaxOverheadBytes = ResponseOverheadBytes;

        /// <summary>
        /// Prepends a request header to <paramref name="message"/>.
        /// </summary>
        public static void EncodeRequest(UnionDataList message, ushort requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.PutFirst(requestId);
            message.PutFirst((byte)RRReliableFrameType.Request);
        }

        /// <summary>
        /// Prepends a response header to <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The response payload.</param>
        /// <param name="requestId">The client-local request id.</param>
        /// <param name="ordinal">The response ordinal. Must be at least one.</param>
        public static void EncodeResponse(UnionDataList message, ushort requestId, uint ordinal)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (ordinal == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal), "Response ordinals start at one.");
            }

            // UnionDataList has no uint overload on its extensions, so the ordinal travels as the
            // bit-identical int value and is decoded back through unchecked conversion.
            message.PutFirst(new UnionData(unchecked((int)ordinal)));
            message.PutFirst(requestId);
            message.PutFirst((byte)RRReliableFrameType.Response);
        }

        /// <summary>
        /// Prepends a finish header to <paramref name="message"/>.
        /// </summary>
        public static void EncodeFinish(UnionDataList message, ushort requestId,
            RRReliableCompletionType completion)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.PutFirst((byte)completion);
            message.PutFirst(requestId);
            message.PutFirst((byte)RRReliableFrameType.Finish);
        }

        /// <summary>
        /// Prepends a cancel header to <paramref name="message"/>.
        /// </summary>
        public static void EncodeCancel(UnionDataList message, ushort requestId)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.PutFirst(requestId);
            message.PutFirst((byte)RRReliableFrameType.Cancel);
        }

        /// <summary>
        /// Removes and decodes the header of <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The received frame. The caller owns it.</param>
        /// <param name="header">The decoded header when the method returns
        /// <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when the frame starts with a complete, well-formed
        /// header; otherwise <see langword="false"/>. A malformed frame may have already lost its
        /// leading elements; the caller releases the remainder.</returns>
        public static bool TryDecode(UnionDataList message, out RRReliableFrameHeader header)
        {
            if (message == null!)
            {
                throw new ArgumentNullException(nameof(message));
            }

            header = default;

            if (!message.TryPopFirst(out byte rawType))
            {
                return false;
            }

            RRReliableFrameType frameType;

            switch (rawType)
            {
                case (byte)RRReliableFrameType.Request:
                    frameType = RRReliableFrameType.Request;
                    break;
                case (byte)RRReliableFrameType.Response:
                    frameType = RRReliableFrameType.Response;
                    break;
                case (byte)RRReliableFrameType.Finish:
                    frameType = RRReliableFrameType.Finish;
                    break;
                case (byte)RRReliableFrameType.Cancel:
                    frameType = RRReliableFrameType.Cancel;
                    break;
                default:
                    return false;
            }

            if (!message.TryPopFirst(out ushort requestId))
            {
                return false;
            }

            switch (frameType)
            {
                case RRReliableFrameType.Request:
                case RRReliableFrameType.Cancel:
                    header = new RRReliableFrameHeader(frameType, requestId, 0,
                        RRReliableCompletionType.Normal);
                    return true;

                case RRReliableFrameType.Response:
                {
                    if (!message.TryPopFirst(out int rawOrdinal))
                    {
                        return false;
                    }

                    uint ordinal = unchecked((uint)rawOrdinal);

                    if (ordinal == 0)
                    {
                        return false;
                    }

                    header = new RRReliableFrameHeader(frameType, requestId, ordinal,
                        RRReliableCompletionType.Normal);
                    return true;
                }

                case RRReliableFrameType.Finish:
                {
                    if (!message.TryPopFirst(out byte rawCompletion))
                    {
                        return false;
                    }

                    RRReliableCompletionType completion;

                    switch (rawCompletion)
                    {
                        case (byte)RRReliableCompletionType.Normal:
                            completion = RRReliableCompletionType.Normal;
                            break;
                        case (byte)RRReliableCompletionType.Canceled:
                            completion = RRReliableCompletionType.Canceled;
                            break;
                        case (byte)RRReliableCompletionType.Rejected:
                            completion = RRReliableCompletionType.Rejected;
                            break;
                        case (byte)RRReliableCompletionType.Failed:
                            completion = RRReliableCompletionType.Failed;
                            break;
                        default:
                            return false;
                    }

                    header = new RRReliableFrameHeader(frameType, requestId, 0, completion);
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}
