using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw.Unreliable.Tests;

public sealed class StopRecorder
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _count;
    private StopReason? _lastReason;

    public Action<StopReason> Callback { get; }

    public StopRecorder()
    {
        Callback = reason =>
        {
            lock (this)
            {
                _count++;
                _lastReason = reason;
            }

            _tcs.TrySetResult();
        };
    }

    public int Count
    {
        get { lock (this) return _count; }
    }

    public StopReason? LastReason
    {
        get { lock (this) return _lastReason; }
    }

    public Task WaitAsync() => _tcs.Task;

    public void Reset()
    {
        // Reserved for future reuse scenarios.
    }
}

public sealed class CheckpointLease : IDisposable
{
    private readonly ICheckPointCtl _ctl;
    private readonly Task<CheckPointWaitResult> _reached;
    private bool _disposed;

    public CheckpointLease(ICheckPoint gate)
    {
        _ctl = (ICheckPointCtl)gate;
        _reached = _ctl.Arm(1);
    }

    public Task<CheckPointWaitResult> Reached => _reached;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _ctl.Reset();
        }
    }
}
