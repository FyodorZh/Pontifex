using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Recording <see cref="IRawReliableClientHandler"/> for the client transport tests. It records the
/// connection lifecycle callbacks and inbound buffers, can be configured to throw from the stop
/// callback, and optionally appends callback names to a shared event log.
/// </summary>
/// <remarks>
/// <see cref="OnReceived"/> does not release the delivered buffer; ownership stays with the handler
/// so the test can inspect and release it. Buffers still alive at <see cref="Dispose"/> time are
/// released there as a safety net.
/// </remarks>
internal sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler, IDisposable
{
    private readonly List<UnionDataList> _received = new();
    private readonly List<string>? _events;

    public RecordingRawReliableClientHandler(List<string>? events = null)
    {
        _events = events;
    }

    /// <summary>
    /// Gets or sets the probe used to observe the transport's connectivity from inside the stop
    /// callback.
    /// </summary>
    public Func<bool>? IsConnectedProbe { get; set; }

    public Exception? StoppedException { get; set; }

    public int DisconnectedCount { get; private set; }

    public StopReason? StoppedReason { get; private set; }

    public int StoppedCount { get; private set; }

    public bool? IsConnectedDuringStopped { get; private set; }

    public Exception? ThrownStoppedException { get; private set; }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        _received.Add(receivedBuffer);
        _events?.Add("handlerReceived");
    }

    public void OnDisconnected(StopReason reason)
    {
        DisconnectedCount++;
        _events?.Add("handlerDisconnected");
    }

    public void OnStopped(StopReason reason)
    {
        StoppedReason = reason;
        StoppedCount++;
        _events?.Add("handlerStopped");
        IsConnectedDuringStopped = IsConnectedProbe?.Invoke();

        if (StoppedException != null)
        {
            ThrownStoppedException = StoppedException;
            throw StoppedException;
        }
    }

    public void Dispose()
    {
        foreach (UnionDataList buffer in _received)
        {
            if (buffer.IsAlive)
            {
                buffer.Release();
            }
        }

        _received.Clear();
    }
}
