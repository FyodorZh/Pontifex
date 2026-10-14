using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Helpers for building <see cref="UnionDataList"/> buffers with controlled sizes.
/// The data size of a list is the varint-encoded element count plus the size of every element,
/// so tests should compare against <see cref="UnionDataList.GetDataSize"/> rather than hard-code.
/// </summary>
internal static class TestBuffers
{
    public static UnionDataList Acquire(IMemoryRental memory)
    {
        return memory.CollectablePool.Acquire<UnionDataList>();
    }

    /// <summary>
    /// Creates a list containing a single byte element (a small, known-size buffer).
    /// </summary>
    public static UnionDataList WithByte(IMemoryRental memory, byte value)
    {
        UnionDataList list = Acquire(memory);
        list.PutLast(new UnionData(value));
        return list;
    }

    /// <summary>
    /// Creates a list containing a single byte-array element of the given length.
    /// </summary>
    public static UnionDataList WithPayload(IMemoryRental memory, int byteCount)
    {
        UnionDataList list = Acquire(memory);
        IMultiRefByteArray bytes = memory.ByteArraysPool.Acquire(byteCount);
        list.PutLast(new UnionData((IMultiRefReadOnlyByteArray)bytes));
        return list;
    }
}
