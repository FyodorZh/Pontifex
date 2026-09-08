using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports.Raw.Support;

/// <summary>
/// Buffer construction and ownership helpers for RawEndpoint tests.
///
/// Buffers are real <see cref="UnionDataList"/> instances acquired from the shared
/// collectable pool (repo idiom, see UnionDataListTests). A freshly acquired buffer
/// has one owner (refcount 1). Tests therefore observe the endpoint's release
/// behaviour with two simple probes:
/// <list type="bullet">
/// <item>no extra reference held -> after the endpoint releases the buffer,
/// <c>IsAlive == false</c> (catches leaks / under-release);</item>
/// <item>an extra reference held (<see cref="Create"/> with <c>keepRef</c>) -> the buffer
/// stays alive while the endpoint must still own it (catches premature release).</item>
/// </list>
/// Note: a double Release of a live buffer is not reliably observable, because
/// MultiRefResource misuse reporting is Debug.Assert-gated and is compiled out of the
/// Release build of the Actuarius package. Over-release is therefore best covered by the
/// held-reference probes plus the sequence/content assertions in the suites.
/// </summary>
public static class RawTestBuffers
{
    /// <summary>Creates a message carrying a single leading int marker.</summary>
    /// <param name="memory">Pool source; defaults to the shared rental.</param>
    /// <param name="marker">Distinguishing int payload (first element).</param>
    /// <param name="keepRef">When true, adds one extra owner reference the caller must later Release.</param>
    public static UnionDataList Create(IMemoryRental? memory = null, int marker = 0, bool keepRef = false)
    {
        var m = memory ?? MemoryRental.Shared;
        var buffer = m.CollectablePool.Acquire<UnionDataList>();
        buffer.PutLast(new UnionData(marker));
        if (keepRef)
        {
            buffer.AddRef();
        }

        return buffer;
    }

    /// <summary>Creates an empty (payload-free) message.</summary>
    public static UnionDataList CreateEmpty(IMemoryRental? memory = null, bool keepRef = false)
    {
        var m = memory ?? MemoryRental.Shared;
        var buffer = m.CollectablePool.Acquire<UnionDataList>();
        if (keepRef)
        {
            buffer.AddRef();
        }

        return buffer;
    }

    /// <summary>Reads the leading int marker, or -1 if the buffer has none.</summary>
    public static int ReadMarker(UnionDataList buffer)
    {
        if (buffer.IsAlive && buffer.Elements.Count > 0 && buffer.Elements[0].Type == UnionDataType.Int)
        {
            return buffer.Elements[0].Alias.IntValue;
        }

        return -1;
    }
}
