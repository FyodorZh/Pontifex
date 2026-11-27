using Pontifex.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests;

/// <summary>
/// Wraps a server handler factory and records when every created session handler has stopped.
/// Transport stop callbacks may run before session callbacks finish, so teardown waits on both.
/// </summary>
public sealed class TrackingServerHandlerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
{
    private readonly IRawReliableServerHandlerFactory<IRawReliableServerHandler> _inner;
    private readonly object _gate = new();
    private TaskCompletionSource _allStopped = RawReliableTestSupport.CreateCompletionSource();
    private int _creating;
    private int _created;
    private int _stopped;

    public TrackingServerHandlerFactory(IRawReliableServerHandlerFactory<IRawReliableServerHandler> inner)
    {
        _inner = inner;
        _allStopped.TrySetResult();
    }

    public IRawReliableServerHandler? TryCreate(IEndPoint remote)
    {
        lock (_gate)
        {
            _creating++;
            if (_allStopped.Task.IsCompleted)
            {
                _allStopped = RawReliableTestSupport.CreateCompletionSource();
            }
        }

        try
        {
            var handler = _inner.TryCreate(remote);
            lock (_gate)
            {
                if (handler != null)
                {
                    _created++;
                }

                _creating--;
                CompleteIfReadyLocked();
            }

            return handler == null ? null : new TrackingHandler(handler, this);
        }
        catch
        {
            lock (_gate)
            {
                _creating--;
                CompleteIfReadyLocked();
            }

            throw;
        }
    }

    public async Task WaitForAllStoppedAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (true)
        {
            Task completion;
            lock (_gate)
            {
                completion = _allStopped.Task;
            }

            await completion.WaitAsync(cancellation.Token);
            lock (_gate)
            {
                if (_creating == 0 && _stopped >= _created)
                {
                    return;
                }
            }
        }
    }

    private void HandlerStopped()
    {
        lock (_gate)
        {
            _stopped++;
            CompleteIfReadyLocked();
        }
    }

    private void CompleteIfReadyLocked()
    {
        if (_creating == 0 && _stopped >= _created)
        {
            _allStopped.TrySetResult();
        }
    }

    private sealed class TrackingHandler(
        IRawReliableServerHandler inner,
        TrackingServerHandlerFactory owner) : IRawReliableServerHandler
    {
        private int _stopped;

        public void OnConnected(IRawReliableEndpoint endPoint) => inner.OnConnected(endPoint);

        public void OnReceived(UnionDataList receivedBuffer) => inner.OnReceived(receivedBuffer);

        public void OnDisconnected(StopReason reason) => inner.OnDisconnected(reason);

        public void OnStopped(StopReason reason)
        {
            try
            {
                inner.OnStopped(reason);
            }
            finally
            {
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    owner.HandlerStopped();
                }
            }
        }
    }
}
