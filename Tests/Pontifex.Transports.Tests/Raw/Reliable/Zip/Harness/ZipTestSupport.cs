using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Zip.Harness
{
    /// <summary>
    /// A minimal <see cref="IEndPoint"/> carrying only the identity the wrapper proxies forward.
    /// </summary>
    internal sealed class FakeZipEndPoint : IEndPoint
    {
        public FakeZipEndPoint(string address)
        {
            Address = address;
        }

        public string Address { get; }

        public bool Equals(IEndPoint? other)
        {
            return other is FakeZipEndPoint endpoint && endpoint.Address == Address;
        }

        public override bool Equals(object? obj)
        {
            return obj is FakeZipEndPoint endpoint && endpoint.Address == Address;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }

        public override string ToString()
        {
            return Address;
        }
    }

    /// <summary>
    /// Helpers for building and describing flat payloads in tests.
    /// </summary>
    internal static class ZipPayload
    {
        public static UnionDataList Create(params object[] values)
        {
            var list = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            foreach (var value in values)
            {
                switch (value)
                {
                    case string text:
                        list.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(text))));
                        break;
                    case int number:
                        list.PutLast(new UnionData(number));
                        break;
                    case short number:
                        list.PutLast(new UnionData(number));
                        break;
                    case byte number:
                        list.PutLast(new UnionData(number));
                        break;
                    case byte[] bytes:
                        list.PutLast(new UnionData(new MultiRefByteArray(bytes)));
                        break;
                    default:
                        throw new ArgumentException($"Unsupported payload value {value}", nameof(values));
                }
            }

            return list;
        }

        public static string[] Describe(UnionDataList data)
        {
            var result = new string[data.Elements.Count];
            for (int i = 0; i < data.Elements.Count; i++)
            {
                var element = data.Elements[i];
                switch (element.Type)
                {
                    case UnionDataType.Array:
                        result[i] = "s:" + Decode(element);
                        break;
                    case UnionDataType.Int:
                        result[i] = "i:" + element.Alias.IntValue;
                        break;
                    case UnionDataType.Short:
                        result[i] = "h:" + element.Alias.ShortValue;
                        break;
                    case UnionDataType.Byte:
                        result[i] = "b:" + element.Alias.ByteValue;
                        break;
                    default:
                        result[i] = element.Type.ToString();
                        break;
                }
            }

            return result;
        }

        public static int ByteCount(UnionData element)
        {
            var bytes = element.Bytes;
            return bytes?.Count ?? 0;
        }

        private static string Decode(UnionData element)
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
