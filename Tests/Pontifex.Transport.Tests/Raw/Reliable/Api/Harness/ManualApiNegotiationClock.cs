using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Api.Harness
{
    /// <summary>
    /// A deterministic, manual <see cref="ITimerFactory"/> for the Api suite: no wall-clock time and no
    /// real timers. Scheduled callbacks fire only from <see cref="Advance"/>, in due-time order.
    /// </summary>
    internal sealed class ManualApiNegotiationClock : ITimerFactory
    {
        private sealed class Entry : IDisposable
        {
            private readonly List<Entry> _owner;

            public Entry(List<Entry> owner)
            {
                _owner = owner;
            }

            public TimeSpan Delay;
            public DateTime Due;
            public Action Callback = null!;

            public void Dispose()
            {
                _owner.Remove(this);
            }
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public DateTime Now { get; private set; } = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public IReadOnlyList<TimeSpan> PendingDelays =>
            _entries.OrderBy(e => e.Due).Select(e => e.Delay).ToList();

        public int PendingCount => _entries.Count;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var entry = new Entry(_entries)
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
                    .Where(e => e.Due <= Now)
                    .OrderBy(e => e.Due)
                    .FirstOrDefault();

                if (due == null)
                {
                    return;
                }

                _entries.Remove(due);
                due.Callback();
            }
        }
    }
}
