using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Protocol.Harness
{
    /// <summary>
    /// A deterministic, manual <see cref="INegotiationClock"/>: no wall-clock time and no real timers.
    /// </summary>
    internal sealed class ManualNegotiationClock : INegotiationClock
    {
        private sealed class Entry : IDisposable
        {
            public TimeSpan Delay;
            public DateTime Due;
            public Action Callback = null!;
            public bool Cancelled;

            public void Dispose()
            {
                Cancelled = true;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public DateTime Now { get; private set; } = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public IReadOnlyList<TimeSpan> PendingDelays =>
            _entries.Where(e => !e.Cancelled).Select(e => e.Delay).ToList();

        public int PendingCount => _entries.Count(e => !e.Cancelled);

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var entry = new Entry
            {
                Delay = delay,
                Due = Now + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay),
                Callback = callback
            };
            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// Advances the clock and fires every due callback in due-time order.
        /// </summary>
        public void Advance(TimeSpan by)
        {
            Now += by;
            while (true)
            {
                var due = _entries
                    .Where(e => !e.Cancelled && e.Due <= Now)
                    .OrderBy(e => e.Due)
                    .FirstOrDefault();

                if (due == null)
                {
                    return;
                }

                _entries.Remove(due);
                due.Cancelled = true;
                due.Callback();
            }
        }

        /// <summary>
        /// Fires the earliest pending callback without requiring the test to compute its delay.
        /// </summary>
        public void FireNext()
        {
            var due = _entries
                .Where(e => !e.Cancelled)
                .OrderBy(e => e.Due)
                .FirstOrDefault();

            if (due == null)
            {
                return;
            }

            if (due.Due > Now)
            {
                Now = due.Due;
            }

            Advance(TimeSpan.Zero);
        }
    }

    /// <summary>
    /// A minimal <see cref="IEndPoint"/> carrying only the identity the wrapper proxies forward.
    /// </summary>
    internal sealed class FakeEndPoint : IEndPoint
    {
        public FakeEndPoint(string address)
        {
            Address = address;
        }

        public string Address { get; }

        public bool Equals(IEndPoint? other)
        {
            return other is FakeEndPoint endpoint && endpoint.Address == Address;
        }

        public override bool Equals(object? obj)
        {
            return obj is FakeEndPoint endpoint && endpoint.Address == Address;
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
    /// Helpers for building and describing flat negotiation payloads in tests.
    /// </summary>
    internal static class Payload
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

        public static UnionDataList CreateString(string text)
        {
            return Create(text);
        }

        public static List<string> Describe(UnionDataList data)
        {
            var result = new List<string>(data.Elements.Count);
            for (int i = 0; i < data.Elements.Count; i++)
            {
                var element = data.Elements[i];
                switch (element.Type)
                {
                    case UnionDataType.Array:
                        result.Add("s:" + Decode(element));
                        break;
                    case UnionDataType.Int:
                        result.Add("i:" + element.Alias.IntValue);
                        break;
                    case UnionDataType.Short:
                        result.Add("h:" + element.Alias.ShortValue);
                        break;
                    case UnionDataType.Byte:
                        result.Add("b:" + element.Alias.ByteValue);
                        break;
                    default:
                        result.Add(element.Type.ToString());
                        break;
                }
            }

            return result;
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
