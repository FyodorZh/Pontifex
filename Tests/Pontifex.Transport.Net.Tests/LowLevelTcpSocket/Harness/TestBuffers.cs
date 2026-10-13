using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>Factory helpers for building <see cref="MultiRefByteArray"/> buffers in tests.</summary>
    internal static class TestBuffers
    {
        public static MultiRefByteArray Empty() => new MultiRefByteArray(Array.Empty<byte>());

        public static MultiRefByteArray Of(params byte[] bytes) => new MultiRefByteArray(bytes);

        public static MultiRefByteArray Filled(int count, byte value = 0)
        {
            var bytes = new byte[count];
            if (value != 0)
            {
                Array.Fill(bytes, value);
            }

            return new MultiRefByteArray(bytes);
        }

        public static MultiRefByteArray WithOffset(byte[] array, int offset, int count)
            => new MultiRefByteArray(array, offset, count);
    }

    /// <summary>
    /// A <see cref="MultiRefByteArray"/> that records ownership events so tests can assert balanced
    /// release. It distinguishes three different facts:
    /// <list type="bullet">
    /// <item><description><see cref="FinalReleaseCount"/> — how many times the reference count reached
    /// zero (i.e. how many times <see cref="MultiRefResource.OnReleased"/> ran);</description></item>
    /// <item><description><see cref="ReleaseCallCount"/> — how many <c>Release</c> calls were made,
    /// including the failed extra call of a double release;</description></item>
    /// <item><description><see cref="UnderflowCount"/>/<see cref="HasMisuse"/> — a release of an
    /// already-released object, reported by the installed Actuarius.Memory reference-count
    /// diagnostics (the counter does not clamp at zero).</description></item>
    /// </list>
    /// <see cref="CurrentRefCount"/> exposes the final counter value so misuse that drives it negative
    /// is visible instead of being masked by "one final release".
    /// </summary>
    internal sealed class TrackingByteArray : MultiRefByteArray
    {
        private int _finalReleaseCount;
        private int _releaseCallCount;
        private int _underflowCount;
        private int _observedRefCount = 1;

        public TrackingByteArray(byte[] array, int offset = 0, int count = -1)
            : base(array, offset, count)
        {
            // The installed RefCounter does not clamp at zero; it reports a release of an already
            // released object through the counter's Error event, surfaced by the monitor as
            // ResourceUsageErrorType.ReleaseOfReleasedObject.
            MultiRefResourceMonitor monitor = ConstructMonitor(
                collectHistory: false, trackLeaks: false, traceEnabled: false);
            monitor.Error += OnMonitorError;
            RefCount.Updated += OnRefCountUpdated;
        }

        /// <summary>Number of times the reference count reached zero.</summary>
        public int FinalReleaseCount => Volatile.Read(ref _finalReleaseCount);

        /// <summary>Legacy name for <see cref="FinalReleaseCount"/>.</summary>
        public int ReleaseCount => FinalReleaseCount;

        public bool WasReleased => FinalReleaseCount > 0;

        /// <summary>Number of <c>Release</c> calls observed, including failed (underflowing) calls.</summary>
        public int ReleaseCallCount => Volatile.Read(ref _releaseCallCount);

        /// <summary>Number of releases of an already-released object observed.</summary>
        public int UnderflowCount => Volatile.Read(ref _underflowCount);

        /// <summary>Current reference count; a double release drives this negative.</summary>
        public int CurrentRefCount => RefCount.RefCount;

        /// <summary>True when a release of an already-released object was observed.</summary>
        public bool HasMisuse => Volatile.Read(ref _underflowCount) > 0;

        /// <summary>
        /// Runs when the reference count reaches zero, before the base final-release work. Lets a
        /// test model a blocked or reentrant pool return without a derived buffer type.
        /// <see langword="null"/> in production.
        /// </summary>
        public Action? OnFinalRelease { get; set; }

        protected override void OnReleased()
        {
            OnFinalRelease?.Invoke();
            Interlocked.Increment(ref _finalReleaseCount);
            base.OnReleased();
        }

        private void OnRefCountUpdated(int newCount)
        {
            int previous = Interlocked.Exchange(ref _observedRefCount, newCount);
            if (newCount < previous)
            {
                Interlocked.Increment(ref _releaseCallCount);
            }
        }

        private void OnMonitorError(string ownerName, ResourceUsageErrorType errorType)
        {
            if (errorType == ResourceUsageErrorType.ReleaseOfReleasedObject)
            {
                Interlocked.Increment(ref _underflowCount);
                Interlocked.Increment(ref _releaseCallCount);
            }
        }
    }

    /// <summary>
    /// A <see cref="MultiRefByteArray"/> whose first release throws, modelling a failing pool sink /
    /// buffer release. Later releases (if any) complete normally.
    /// </summary>
    internal sealed class ThrowingReleaseByteArray : MultiRefByteArray
    {
        private readonly Exception _exception;
        private int _releaseAttempts;
        private bool _thrown;

        public ThrowingReleaseByteArray(byte[] array, int offset = 0, int count = -1, Exception? exception = null)
            : base(array, offset, count)
        {
            _exception = exception ?? new InvalidOperationException("Simulated buffer release failure");
        }

        public int ReleaseAttempts => Volatile.Read(ref _releaseAttempts);

        protected override void OnReleased()
        {
            Interlocked.Increment(ref _releaseAttempts);
            if (!_thrown)
            {
                _thrown = true;
                throw _exception;
            }

            base.OnReleased();
        }
    }
}
