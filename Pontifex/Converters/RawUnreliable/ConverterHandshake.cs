using System;
using System.Text;
using Actuarius.Memory;
using Pontifex.Utils;
using Status = Pontifex.Raw.Reliable.RawReliableHandshake.HandshakeStatus;

namespace Pontifex.Converters
{
    /// <summary>
    /// The converter-tier handshake that runs over the <c>DeliveryManager</c>. The dataset is a flat
    /// <see cref="UnionDataList"/> and is deliberately distinct from the transport-tier handshake codec:
    /// <list type="bullet">
    /// <item><description>Request: <c>[version, "converter", messageMaxByteSize]</c></description></item>
    /// <item><description>Accepted: <c>[version, "converter", HandshakeStatus.Accepted, agreedMax]</c></description></item>
    /// <item><description>Reject: <c>[serverVersion, "converter", HandshakeStatus.Rejected, text?]</c></description></item>
    /// </list>
    /// The third element is always the status, so the client can inspect the discriminator before parsing
    /// either variant.
    /// </summary>
    internal static class ConverterHandshake
    {
        /// <summary>
        /// The converter handshake protocol version.
        /// </summary>
        public const short Version = 1;

        /// <summary>
        /// The handshake name used by the synthetic converter transport.
        /// </summary>
        public const string Name = "converter";

        /// <summary>
        /// Builds a handshake request.
        /// </summary>
        public static UnionDataList BuildRequest(int messageMaxByteSize)
        {
            if (messageMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxByteSize));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            data.PutLast(new UnionData(Version));
            data.PutLast(NameElement());
            data.PutLast(new UnionData(messageMaxByteSize));
            return data;
        }

        /// <summary>
        /// Builds an accepted handshake response.
        /// </summary>
        public static UnionDataList BuildAccepted(int agreedMax)
        {
            if (agreedMax < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(agreedMax));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            data.PutLast(new UnionData(Version));
            data.PutLast(NameElement());
            data.PutLast(new UnionData((byte)Status.Accepted));
            data.PutLast(new UnionData(agreedMax));
            return data;
        }

        /// <summary>
        /// Builds a rejected handshake response. The envelope carries the server's own version and name
        /// so the client can distinguish a protocol mismatch from an ordinary rejection.
        /// </summary>
        public static UnionDataList BuildReject(short serverVersion, string serverName, Status status, string? text)
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
        /// Reads only the status discriminator without parsing either variant.
        /// </summary>
        public static bool TryReadStatus(UnionDataList data, out Status status)
        {
            status = Status.Rejected;
            if (data == null || data.Elements.Count < 3)
            {
                return false;
            }

            if (data.Elements[0].Type != UnionDataType.Short ||
                data.Elements[1].Type != UnionDataType.Array ||
                data.Elements[2].Type != UnionDataType.Byte)
            {
                return false;
            }

            status = (Status)data.Elements[2].Alias.ByteValue;
            return true;
        }

        /// <summary>
        /// Parses a handshake request.
        /// </summary>
        public static bool TryParseRequest(UnionDataList data, out short version, out string name, out int max)
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
        /// Parses an accepted handshake response. The caller must have inspected the status first.
        /// </summary>
        public static bool TryParseAccepted(UnionDataList data, out short version, out string name, out int agreedMax)
        {
            version = 0;
            name = string.Empty;
            agreedMax = 0;

            if (data == null || data.Elements.Count != 4)
            {
                return false;
            }

            if (data.Elements[0].Type != UnionDataType.Short ||
                data.Elements[1].Type != UnionDataType.Array ||
                data.Elements[2].Type != UnionDataType.Byte ||
                data.Elements[3].Type != UnionDataType.Int)
            {
                return false;
            }

            if ((Status)data.Elements[2].Alias.ByteValue != Status.Accepted)
            {
                return false;
            }

            version = data.Elements[0].Alias.ShortValue;
            name = DecodeName(data.Elements[1]);
            agreedMax = data.Elements[3].Alias.IntValue;
            return agreedMax >= 1;
        }

        /// <summary>
        /// Parses a rejected handshake response. The caller must have inspected the status first.
        /// </summary>
        public static bool TryParseReject(UnionDataList data, out short version, out string name,
            out Status status, out string? text)
        {
            version = 0;
            name = string.Empty;
            status = Status.Rejected;
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

            version = data.Elements[0].Alias.ShortValue;
            name = DecodeName(data.Elements[1]);
            status = (Status)data.Elements[2].Alias.ByteValue;
            if (status == Status.Accepted)
            {
                return false;
            }

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
        public static string DescribeStatus(Status status)
        {
            switch (status)
            {
                case Status.VersionMismatch:
                    return "version mismatch";
                case Status.NameMismatch:
                    return "name mismatch";
                case Status.CapacityExceeded:
                    return "capacity exceeded";
                case Status.Timeout:
                    return "timeout";
                case Status.Rejected:
                    return "rejected";
                default:
                    return "bad request";
            }
        }

        private static UnionData NameElement()
        {
            return new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(Name)));
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
}
