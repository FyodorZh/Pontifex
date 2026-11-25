using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Minimal hand-written reliable client endpoint for the failed-start paths of
/// <c>RawReliableEndpointClientTransport</c>. It records <c>Start</c> and <c>Stop</c>, can reject or throw
/// from <c>Start</c>, and raises its one-shot terminal <c>Stopped</c> notification from <c>Stop</c>.
/// </summary>
internal sealed class FakeRawReliableClientEndpoint : IRawReliableClientEndpointImp
{
    private bool _stoppedRaised;

    public bool StartResult { get; set; } = true;

    public Exception? StartException { get; set; }

    /// <summary>
    /// Gets or sets a probe invoked from <see cref="Start"/> after recording the call and before the result is
    /// produced. It lets a test observe the transport attachment at the moment the endpoint starts.
    /// </summary>
    public Action? StartProbe { get; set; }

    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public StopReason? LastStopReason { get; private set; }

    public bool IsConnected => false;

    public bool IsLowLevelConnected => false;

    public bool IsRunning { get; private set; }

    public IEndPoint? RemoteEndPoint => null;

    public int MessageMaxByteSize => 1024;

    public event Action<StopReason>? Stopped;

    public bool Start()
    {
        StartCalls++;
        StartProbe?.Invoke();

        if (StartException != null)
        {
            throw StartException;
        }

        IsRunning = StartResult;
        return StartResult;
    }

    public void Stop(StopReason reason, bool tryBeGentle)
    {
        StopCalls++;
        LastStopReason = reason;
        IsRunning = false;

        if (_stoppedRaised)
        {
            return;
        }

        _stoppedRaised = true;
        Stopped?.Invoke(reason);
    }

    public bool Disconnect(StopReason reason)
    {
        return false;
    }

    public SendResult Send(UnionDataList bufferToSend)
    {
        return SendResult.NotConnected;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}
