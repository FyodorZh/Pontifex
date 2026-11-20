using System;
using System.Text;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Codec for the reliable-wrapper negotiation envelope. The dataset is a flat
    /// <see cref="UnionDataList"/>:
    /// <c>version (short) + name (string) + status (byte) + ...custom layer elements</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: every builder returns a list owned by the caller. Every parser
    /// clones the custom tail into a fresh list owned by the caller and leaves the source list untouched, so
    /// the source buffer can be released independently.</description></item>
    /// <item><description><b>Validation</b>: the status discriminator is validated before the custom tail is
    /// read, so a custom element can never be mistaken for a status.</description></item>
    /// </list>
    /// </remarks>
    internal static class RawReliableNegotiation
    {
        /// <summary>
        /// The default negotiation protocol version.
        /// </summary>
        public const short DefaultVersion = 1;

        /// <summary>
        /// The default inclusive maximum size of a negotiation envelope.
        /// </summary>
        public const int DefaultMaxByteSize = 128;

        /// <summary>
        /// The default negotiation deadline.
        /// </summary>
        public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default time a rejected session stays open after the reject envelope is sent.
        /// </summary>
        public static readonly TimeSpan DefaultRejectLinger = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Builds a client negotiation request.
        /// </summary>
        /// <param name="version">The negotiated protocol version.</param>
        /// <param name="name">The layer negotiation name. Must be non-null.</param>
        /// <param name="customTail">The custom layer elements, or <see langword="null"/> for none. The tail is
        /// cloned; the caller retains and releases it.</param>
        /// <returns>The request envelope owned by the caller.</returns>
        public static UnionDataList BuildRequest(short version, string name, UnionDataList? customTail)
        {
            return BuildEnvelope(version, name, NegotiationStatus.Request, customTail);
        }

        /// <summary>
        /// Builds a server negotiation response.
        /// </summary>
        /// <param name="version">The negotiated protocol version.</param>
        /// <param name="name">The layer negotiation name. Must be non-null.</param>
        /// <param name="customTail">The custom layer elements, or <see langword="null"/> for none. The tail is
        /// cloned; the caller retains and releases it.</param>
        /// <returns>The response envelope owned by the caller.</returns>
        public static UnionDataList BuildResponse(short version, string name, UnionDataList? customTail)
        {
            return BuildEnvelope(version, name, NegotiationStatus.Accepted, customTail);
        }

        /// <summary>
        /// Builds a server negotiation rejection.
        /// </summary>
        /// <param name="version">The server protocol version.</param>
        /// <param name="name">The server layer negotiation name. Must be non-null.</param>
        /// <param name="text">An optional diagnostic text, or <see langword="null"/>.</param>
        /// <returns>The rejection envelope owned by the caller.</returns>
        public static UnionDataList BuildReject(short version, string name, string? text)
        {
            UnionDataList? tail = null;
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    tail = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                    tail.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(text!))));
                }

                return BuildEnvelope(version, name, NegotiationStatus.Rejected, tail);
            }
            finally
            {
                tail?.Release();
            }
        }

        /// <summary>
        /// Reads the envelope header and clones the custom tail.
        /// </summary>
        /// <param name="data">The borrowed envelope.</param>
        /// <param name="version">Receives the protocol version.</param>
        /// <param name="name">Receives the layer negotiation name.</param>
        /// <param name="status">Receives the validated status discriminator.</param>
        /// <param name="customTail">Receives a fresh list with the custom elements, owned by the caller. Every
        /// returned tail, including an empty one, must be released exactly once.</param>
        /// <returns><see langword="true"/> when the envelope has at least three correctly typed elements and a
        /// known status discriminator.</returns>
        public static bool TryReadEnvelope(UnionDataList data, out short version, out string name,
            out NegotiationStatus status, out UnionDataList customTail)
        {
            version = 0;
            name = string.Empty;
            status = NegotiationStatus.Rejected;
            customTail = null!;

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

            byte statusValue = data.Elements[2].Alias.ByteValue;
            if (!IsKnownStatus(statusValue))
            {
                return false;
            }

            version = data.Elements[0].Alias.ShortValue;
            name = DecodeString(data.Elements[1]);
            status = (NegotiationStatus)statusValue;

            var tail = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            try
            {
                for (int i = 3; i < data.Elements.Count; i++)
                {
                    tail.PutLast(data.Elements[i].Clone());
                }
            }
            catch
            {
                tail.Release();
                throw;
            }

            customTail = tail;
            return true;
        }

        /// <summary>
        /// Reads the first optional element of a rejection tail as diagnostic text.
        /// </summary>
        /// <param name="customTail">The borrowed rejection tail.</param>
        /// <returns>The decoded text, or <see langword="null"/> when the tail has no string element.</returns>
        public static string? ReadRejectText(UnionDataList customTail)
        {
            if (customTail == null || customTail.Elements.Count == 0)
            {
                return null;
            }

            var element = customTail.Elements[0];
            if (element.Type != UnionDataType.Array)
            {
                return null;
            }

            string text = DecodeString(element);
            return text.Length == 0 ? null : text;
        }

        /// <summary>
        /// Compares two negotiation names by their UTF-8 content.
        /// </summary>
        public static bool NameEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static UnionDataList BuildEnvelope(short version, string name, NegotiationStatus status,
            UnionDataList? customTail)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            try
            {
                data.PutLast(new UnionData(version));
                data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
                data.PutLast(new UnionData((byte)status));

                if (customTail != null)
                {
                    for (int i = 0; i < customTail.Elements.Count; i++)
                    {
                        data.PutLast(customTail.Elements[i].Clone());
                    }
                }

                return data;
            }
            catch
            {
                data.Release();
                throw;
            }
        }

        private static bool IsKnownStatus(byte value)
        {
            switch (value)
            {
                case (byte)NegotiationStatus.Request:
                case (byte)NegotiationStatus.Accepted:
                case (byte)NegotiationStatus.Rejected:
                    return true;
                default:
                    return false;
            }
        }

        private static string DecodeString(UnionData element)
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
