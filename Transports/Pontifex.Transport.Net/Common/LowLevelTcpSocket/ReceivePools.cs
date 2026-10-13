using Actuarius.Memory;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// Opt-in receive-pool composition for <see cref="LowLevelTcpSocket"/>. The transport accepts
    /// any caller-injected <see cref="IConcurrentPool{TResource, TParam0}"/> as its
    /// <c>byteArraysPool</c> constructor argument; this factory is the documented entry point for
    /// the dependency's allocation-free pools (P02 rental-wrapper portion). No transport signature
    /// changes and no custom owner/refcount type: the wrapper recycling is provided by
    /// Actuarius.Memory.
    /// </summary>
    /// <remarks>
    /// <para><b>Recycled-wrapper identity hazard.</b> Without recycling, every acquisition allocates
    /// a fresh wrapper (≈96 B/op). The shared/bounded pools recycle wrapper instances, so a stale
    /// reference released twice (or released after the wrapper was re-acquired) corrupts the current
    /// owner's reference count and can return an in-use array. The transport's exactly-once release
    /// (stages 7, 13, 14) is therefore load-bearing for these pools.</para>
    /// <para><b>Count semantics.</b> <see cref="IMultiRefByteArray.Count"/> is the requested capacity;
    /// the raw backing array length is the next power of two of the request and may be larger. The
    /// receiver arms <c>SetBuffer(next.Array, next.Offset, next.Count)</c>, so only the requested
    /// capacity is used as a receive window; the excess raw capacity is harmless slack. Memory
    /// sizing, however, is based on the rounded raw length.</para>
    /// <para><b>Diagnostics caveat.</b> <c>ByteArrayConcurrentPoolGuard</c> wraps a pool for leak /
    /// double-release diagnostics but its own <c>Release</c> is a no-op, so it never exercises wrapper
    /// recycling (the wrapped pool never gets the wrapper back). Recycling/double-release safety must
    /// be verified against the real pool.</para>
    /// </remarks>
    internal static class ReceivePools
    {
        /// <summary>
        /// Allocation-free shared receive pool: recyclable wrappers (cache capacity 1000) over the
        /// process-wide shared raw-array pool. Warm acquire/release allocates no owner metadata.
        /// </summary>
        public static IConcurrentPool<IMultiRefByteArray, int> Shared
            => MemoryRental.Shared.SharedByteArraysPool;

        /// <summary>
        /// Allocation-free receive pool with an explicit wrapper-cache bound. A non-positive bound
        /// disables recycling and degrades to the allocating default behavior.
        /// </summary>
        public static IConcurrentPool<IMultiRefByteArray, int> Bounded(int wrapperCacheCapacity)
            => new ByteArrayConcurrentPool(
                MemoryRental.Shared.GetArrayPool<byte>(),
                wrapperCacheCapacity);

        /// <summary>
        /// The legacy non-recycling pool (kept for comparison and for callers that do not opt in):
        /// every warm acquisition allocates a wrapper.
        /// </summary>
        public static IConcurrentPool<IMultiRefByteArray, int> NonRecycling
            => MemoryRental.Shared.ByteArraysPool;
    }
}
