using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording <see cref="IRawHandler"/> for the raw endpoint tests. Delivered buffers are owned by
/// the handler, so it clones each one for inspection and releases the original, then releases the
/// clones on <see cref="Dispose"/>.
/// </summary>
internal class RecordingRawHandler : IRawHandler, IDisposable
{
    private readonly IMemoryRental _memory;
    private readonly List<UnionDataList> _received = new();
    private readonly List<string> _events = new();

    public RecordingRawHandler(IMemoryRental memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// Gets the clones of the buffers delivered through <see cref="OnReceived"/>.
    /// </summary>
    public IReadOnlyList<UnionDataList> Received => _received;

    /// <summary>
    /// Gets the callback names in invocation order (e.g. <c>received</c>, <c>stopped</c>), for
    /// assertions about callback ordering.
    /// </summary>
    public IReadOnlyList<string> Events => _events;

    public int ReceivedCount => _received.Count;

    public StopReason? StoppedReason { get; private set; }

    public int StoppedCount { get; private set; }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        _received.Add(receivedBuffer.Clone(_memory.CollectablePool));
        receivedBuffer.Release();
        RecordEvent("received");
    }

    public void OnStopped(StopReason reason)
    {
        StoppedReason = reason;
        StoppedCount++;
        RecordEvent("stopped");
    }

    protected void RecordEvent(string name)
    {
        _events.Add(name);
    }

    public void Dispose()
    {
        foreach (UnionDataList buffer in _received)
        {
            buffer.Release();
        }

        _received.Clear();
    }
}
