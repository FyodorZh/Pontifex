using System;
using System.Text;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// The transport handshake shared by every <c>RawReliable</c> carrier. The dataset is a flat
    /// <see cref="UnionDataList"/>: <c>version (short) + name (string) + messageMaxByteSize (int)</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The handshake is internal to the transport layer. Callers own the
    /// built lists and release them exactly once. Names are stored as UTF-8 byte arrays and compared by
    /// content.</description></item>
    /// </list>
    /// </remarks>
    internal static class RawReliableHandshake
    {
        /// <summary>
        /// The transport handshake protocol version.
        /// </summary>
        public const short Version = 1;

        /// <summary>
        /// The default inclusive maximum size of a handshake frame.
        /// </summary>
        public const int DefaultMaxByteSize = 128;

        /// <summary>
        /// The terminal status of a transport handshake attempt.
        /// </summary>
        internal enum HandshakeStatus : byte
        {
            /// <summary>
            /// The handshake succeeded and the session is admitted.
            /// </summary>
            Accepted = 0,

            /// <summary>
            /// The session was rejected.
            /// </summary>
            Rejected,

            /// <summary>
            /// The peer's protocol version does not match.
            /// </summary>
            VersionMismatch,

            /// <summary>
            /// The peer's handshake name does not match.
            /// </summary>
            NameMismatch,

            /// <summary>
            /// The peer cannot receive a handshake response of its advertised maximum size.
            /// </summary>
            CapacityExceeded,

            /// <summary>
            /// The handshake did not complete in time.
            /// </summary>
            Timeout
        }

        /// <summary>
        /// Builds a handshake request.
        /// </summary>
        /// <param name="name">The handshake name. Must be non-null.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size advertised by the client; must
        /// be at least 1.</param>
        /// <returns>The request list without a frame type. Ownership transfers to the caller.</returns>
        public static UnionDataList BuildRequest(string name, int messageMaxByteSize)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (messageMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxByteSize));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            data.PutLast(new UnionData(Version));
            data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
            data.PutLast(new UnionData(messageMaxByteSize));
            return data;
        }

        /// <summary>
        /// Reads the structure of a handshake request without validating its advertised maximum size.
        /// </summary>
        /// <param name="data">The request body without its frame type.</param>
        /// <param name="version">Receives the protocol version.</param>
        /// <param name="name">Receives the handshake name.</param>
        /// <param name="messageMaxByteSize">Receives the advertised maximum message size.</param>
        /// <returns><see langword="true"/> when the structure and element types are valid.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The server uses this to distinguish a malformed request from a
        /// valid request with an unusable capacity, which must be rejected with
        /// <see cref="HandshakeStatus.CapacityExceeded"/>.</description></item>
        /// </list>
        /// </remarks>
        internal static bool TryReadRequest(UnionDataList data, out short version, out string name,
            out int messageMaxByteSize)
        {
            version = 0;
            name = string.Empty;
            messageMaxByteSize = 0;

            if (data == null || data.Elements.Count != 3)
            {
                return false;
            }

            if (data.Elements[0].Type != UnionDataType.Short ||
                data.Elements[1].Type != UnionDataType.Array ||
                data.Elements[2].Type != UnionDataType.Int)
            {
                return false;
            }

            version = data.Elements[0].Alias.ShortValue;
            name = DecodeName(data.Elements[1]);
            messageMaxByteSize = data.Elements[2].Alias.IntValue;
            return true;
        }

        /// <summary>
        /// Parses a handshake request.
        /// </summary>
        /// <param name="data">The request body without its frame type.</param>
        /// <param name="name">Receives the handshake name.</param>
        /// <param name="max">Receives the advertised maximum message size.</param>
        /// <returns><see langword="true"/> when the request has exactly three correctly typed elements and a
        /// positive advertised maximum.</returns>
        public static bool TryParseRequest(UnionDataList data, out string name, out int max)
        {
            if (!TryReadRequest(data, out _, out name, out max))
            {
                return false;
            }

            return max >= 1;
        }

        /// <summary>
        /// Builds a handshake response from the server.
        /// </summary>
        /// <param name="version">The server protocol version.</param>
        /// <param name="name">The server handshake name. Must be non-null.</param>
        /// <param name="agreedMax">The negotiated inclusive maximum message size; must be at least 1.</param>
        /// <returns>The response list without a frame type. Ownership transfers to the caller.</returns>
        public static UnionDataList BuildResponse(short version, string name, int agreedMax)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (agreedMax < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(agreedMax));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            data.PutLast(new UnionData(version));
            data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
            data.PutLast(new UnionData(agreedMax));
            return data;
        }

        /// <summary>
        /// Parses a handshake response.
        /// </summary>
        /// <param name="data">The response body without its frame type.</param>
        /// <param name="version">Receives the server protocol version.</param>
        /// <param name="name">Receives the server handshake name.</param>
        /// <param name="max">Receives the agreed maximum message size.</param>
        /// <returns><see langword="true"/> when the response has exactly three correctly typed elements.</returns>
        public static bool TryParseResponse(UnionDataList data, out short version, out string name, out int max)
        {
            version = 0;
            name = string.Empty;
            max = 0;

            if (data == null || data.Elements.Count != 3)
            {
                return false;
            }

            if (data.Elements[0].Type != UnionDataType.Short ||
                data.Elements[1].Type != UnionDataType.Array ||
                data.Elements[2].Type != UnionDataType.Int)
            {
                return false;
            }

            version = data.Elements[0].Alias.ShortValue;
            name = DecodeName(data.Elements[1]);
            max = data.Elements[2].Alias.IntValue;
            return true;
        }

        /// <summary>
        /// Builds a handshake rejection.
        /// </summary>
        /// <param name="serverVersion">The server protocol version.</param>
        /// <param name="serverName">The server handshake name. Must be non-null.</param>
        /// <param name="status">The rejection status.</param>
        /// <param name="text">An optional diagnostic text, or <see langword="null"/>.</param>
        /// <returns>The rejection list without a frame type. Ownership transfers to the caller.</returns>
        public static UnionDataList BuildReject(short serverVersion, string serverName,
            HandshakeStatus status, string? text)
        {
            if (serverName == null)
            {
                throw new ArgumentNullException(nameof(serverName));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            data.PutLast(new UnionData(serverVersion));
            data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(serverName))));
            data.PutLast(new UnionData((byte)status));
            if (!string.IsNullOrEmpty(text))
            {
                data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(text!))));
            }

            return data;
        }

        /// <summary>
        /// Parses a handshake rejection.
        /// </summary>
        /// <param name="data">The rejection body without its frame type.</param>
        /// <param name="serverVersion">Receives the server protocol version.</param>
        /// <param name="serverName">Receives the server handshake name.</param>
        /// <param name="status">Receives the rejection status.</param>
        /// <param name="text">Receives the optional diagnostic text, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the rejection is structurally valid.</returns>
        public static bool TryParseReject(UnionDataList data, out short serverVersion, out string serverName,
            out HandshakeStatus status, out string? text)
        {
            serverVersion = 0;
            serverName = string.Empty;
            status = HandshakeStatus.Rejected;
            text = null;

            if (data == null || data.Elements.Count < 3 || data.Elements.Count > 4)
            {
                return false;
            }

            if (data.Elements[0].Type != UnionDataType.Short ||
                data.Elements[1].Type != UnionDataType.Array ||
                data.Elements[2].Type != UnionDataType.Byte)
            {
                return false;
            }

            if (data.Elements.Count == 4 && data.Elements[3].Type != UnionDataType.Array)
            {
                return false;
            }

            serverVersion = data.Elements[0].Alias.ShortValue;
            serverName = DecodeName(data.Elements[1]);
            status = (HandshakeStatus)data.Elements[2].Alias.ByteValue;
            if (data.Elements.Count == 4)
            {
                text = DecodeName(data.Elements[3]);
            }

            return true;
        }

        /// <summary>
        /// Compares two handshake names by their UTF-8 content.
        /// </summary>
        public static bool NameEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets a short diagnostic phrase for a rejection status.
        /// </summary>
        public static string DescribeStatus(HandshakeStatus status)
        {
            switch (status)
            {
                case HandshakeStatus.VersionMismatch:
                    return "version mismatch";
                case HandshakeStatus.NameMismatch:
                    return "name mismatch";
                case HandshakeStatus.CapacityExceeded:
                    return "capacity exceeded";
                case HandshakeStatus.Timeout:
                    return "timeout";
                case HandshakeStatus.Rejected:
                    return "rejected";
                default:
                    return "bad request";
            }
        }

        /// <summary>
        /// Formats the diagnostic text carried by a reject frame.
        /// </summary>
        public static string FormatRejectText(string serverName, short serverVersion, HandshakeStatus status)
        {
            return $"server {serverName}/v{serverVersion}: {DescribeStatus(status)}";
        }

        private static string DecodeName(UnionData element)
        {
            var bytes = element.Bytes;
            if (bytes == null || bytes.Count == 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(bytes.ReadOnlyArray, bytes.Offset, bytes.Count);
        }
    }

    /// <summary>
    /// The result of a transport handshake attempt on the server side.
    /// </summary>
    internal readonly struct HandshakeOutcome
    {
        /// <summary>
        /// Gets the terminal handshake status.
        /// </summary>
        public RawReliableHandshake.HandshakeStatus Status { get; }

        /// <summary>
        /// Gets the admitted session handler, or <see langword="null"/> when the handshake was rejected.
        /// </summary>
        public IRawReliableServerHandler? Handler { get; }

        /// <summary>
        /// Gets the response to send, or <see langword="null"/> when the handshake was rejected.
        /// </summary>
        public UnionDataList? Response { get; }

        /// <summary>
        /// Gets the negotiated inclusive maximum message size, or zero when the handshake was rejected.
        /// </summary>
        public int AgreedMaxByteSize { get; }

        /// <summary>
        /// Gets whether the handshake admitted a session.
        /// </summary>
        public bool IsAccepted =>
            Status == RawReliableHandshake.HandshakeStatus.Accepted && Handler != null;

        /// <summary>
        /// Initializes a rejected outcome.
        /// </summary>
        public HandshakeOutcome(RawReliableHandshake.HandshakeStatus status)
        {
            Status = status;
            Handler = null;
            Response = null;
            AgreedMaxByteSize = 0;
        }

        /// <summary>
        /// Initializes an accepted outcome.
        /// </summary>
        public HandshakeOutcome(RawReliableHandshake.HandshakeStatus status,
            IRawReliableServerHandler handler, UnionDataList response, int agreedMaxByteSize)
        {
            Status = status;
            Handler = handler;
            Response = response;
            AgreedMaxByteSize = agreedMaxByteSize;
        }
    }
}
