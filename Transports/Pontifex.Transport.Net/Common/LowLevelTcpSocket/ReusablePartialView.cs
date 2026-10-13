using System;
using System.Threading;
using Actuarius.Memory;
using Actuarius.Memory.Internal;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// A small, revivable partial-view owner over an <see cref="IMultiRefByteArray"/> source,
    /// used for partial receives so a per-receive <c>MultiRefByteArraySpan</c> allocation is not
    /// needed (P02). The installed span type extends a non-revivable reference counter and cannot
    /// be rebound, so this type implements the <see cref="IMultiRefByteArray"/> contract directly
    /// with its own reusable counter (no reflection into the dependency).
    /// </summary>
    /// <remarks>
    /// <para>Ownership: <see cref="Create"/> acquires exactly one reference on the source while the
    /// view is live; the final <see cref="Release"/> releases that reference exactly once, resets
    /// the view, and returns it to a small bounded process-wide pool. A view is never reused while
    /// any consumer reference remains, and a reused instance is bound to a single new range before
    /// it is handed out.</para>
    /// <para>Valid-lifetime semantics match the dependency: reads/writes delegate to the source at
    /// the relative range, <see cref="Offset"/> is absolute in the backing array, the reference view
    /// exposes an <see cref="IRefCounterView"/> with <c>Updated</c>/<c>Error</c> events, and
    /// <see cref="ConstructMonitor"/> creates the regular leak/double-release monitor. A released
    /// reference is invalid and must not be used; using one after the instance was recycled is a
    /// contract violation by the consumer (the transport never does).</para>
    /// </remarks>
    internal sealed class ReusablePartialView : IMultiRefByteArray, IRefCounterView
    {
        /// <summary>Bound on idle pooled view metadata; extras are dropped for the GC.</summary>
        internal const int MaxPooledViews = 64;

        private static readonly object PoolSync = new();
        private static ReusablePartialView? _poolHead;
        private static int _poolCount;

        private IMultiRefByteArray? _source;
        private int _relativeOffset;
        private int _count;
        private int _refCount;
        private ReusablePartialView? _nextPooled;

        public event Action<int>? Updated;

        public event Action<RefCounterErrorEvent, int>? Error;

        private ReusablePartialView()
        {
        }

        /// <summary>
        /// Binds a view to <paramref name="count"/> bytes of <paramref name="source"/> starting at
        /// relative <paramref name="offset"/>, acquiring one source reference. Prefers an idle
        /// pooled instance; a pool miss allocates (allowed for outstanding concurrency beyond the
        /// pooled capacity).
        /// </summary>
        internal static ReusablePartialView Create(IMultiRefByteArray source, int offset, int count)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (offset < 0 || count < 0 || offset > source.Count - count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    "The partial view range must be inside the source.");
            }

            ReusablePartialView view = RentFromPool() ?? new ReusablePartialView();
            view.Bind(source, offset, count);
            return view;
        }

        private void Bind(IMultiRefByteArray source, int offset, int count)
        {
            _refCount = 1;
            _relativeOffset = offset;
            _count = count;
            _source = source;
            source.AddRef();
        }

        public int Count => _count;

        public int Offset => (_source?.Offset ?? 0) + _relativeOffset;

        public bool IsValid => IsAlive;

        public bool IsAlive => Volatile.Read(ref _refCount) > 0;

        public byte[] Array => _source!.Array;

        public byte[] ReadOnlyArray => _source!.ReadOnlyArray;

        public byte this[int index]
        {
            get
            {
                CheckIndex(index);
                return _source![_relativeOffset + index];
            }
            set
            {
                CheckIndex(index);
                _source![_relativeOffset + index] = value;
            }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            CheckRange(srcOffset, count);
            return _source!.CopyTo(dst, dstOffset, _relativeOffset + srcOffset, count);
        }

        public void AddRef()
        {
            while (true)
            {
                int current = Volatile.Read(ref _refCount);
                if (current <= 0)
                {
                    throw new ObjectDisposedException(
                        nameof(ReusablePartialView),
                        "The partial view has been released and cannot acquire a new generation.");
                }

                if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
                {
                    Updated?.Invoke(current + 1);
                    return;
                }
            }
        }

        public void Release()
        {
            while (true)
            {
                int current = Volatile.Read(ref _refCount);
                if (current <= 0)
                {
                    // The installed RefCounter does not clamp at zero; it reports the misuse. Never
                    // drive the counter negative, and never recycle an already-released view here.
                    Error?.Invoke(RefCounterErrorEvent.FailedToDecrement, current);
                    return;
                }

                if (Interlocked.CompareExchange(ref _refCount, current - 1, current) == current)
                {
                    Updated?.Invoke(current - 1);
                    if (current == 1)
                    {
                        OnReleased();
                    }

                    return;
                }
            }
        }

        public IRefCounterView RefCount => this;

        int IRefCounterView.RefCount => Volatile.Read(ref _refCount);

        public IMultiRefResourceMonitor ConstructMonitor(bool collectHistory, bool trackLeaks, bool traceEnabled)
            => new MultiRefResourceMonitor(
                this, nameof(ReusablePartialView), collectHistory, trackLeaks, traceEnabled);

        private void OnReleased()
        {
            IMultiRefByteArray? source = _source;
            _source = null;
            _relativeOffset = 0;
            _count = 0;
            try
            {
                source?.Release();
            }
            finally
            {
                ReturnToPool();
            }
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new IndexOutOfRangeException(
                    $"Index {index} is outside the partial view (count {_count}).");
            }
        }

        private void CheckRange(int srcOffset, int count)
        {
            if (srcOffset < 0 || count < 0 || srcOffset > _count - count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(srcOffset),
                    "The requested range must be inside the partial view.");
            }
        }

        private static ReusablePartialView? RentFromPool()
        {
            lock (PoolSync)
            {
                ReusablePartialView? view = _poolHead;
                if (view == null)
                {
                    return null;
                }

                _poolHead = view._nextPooled;
                view._nextPooled = null;
                _poolCount--;
                return view;
            }
        }

        private void ReturnToPool()
        {
            lock (PoolSync)
            {
                if (_poolCount >= MaxPooledViews)
                {
                    return;
                }

                _nextPooled = _poolHead;
                _poolHead = this;
                _poolCount++;
            }
        }
    }
}
