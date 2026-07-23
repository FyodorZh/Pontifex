using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pontifex.Utils.CheckPointGate
{
    public sealed class CheckPoint : ICheckPointCtl
    {
        private readonly object _gate = new();
        private volatile bool _disposed;
        private volatile bool _isArmed;
        private int _hitCount;
        private int _epoch;
        private TaskCompletionSource<CheckPointWaitResult>? _armTcs;
        private TaskCompletionSource<byte>? _blockedHitTcs;

        public bool IsArmed => _isArmed;

        public int HitCount => _hitCount;

        public void Hit()
        {
            if (!_isArmed || _disposed)
                return;

            lock (_gate)
            {
                if (!_isArmed || _disposed)
                    return;

                if (_hitCount > 0)
                {
                    _hitCount--;
                    return;
                }

                _armTcs?.TrySetResult(CheckPointWaitResult.Reached);
                _armTcs = null;

                var capturedEpoch = _epoch;
                do
                {
                    Monitor.Wait(_gate);
                } while (!_disposed && _isArmed && _hitCount == 0 && _epoch == capturedEpoch);
            }
        }

        public Task HitAsync()
        {
            if (!_isArmed || _disposed)
                return Task.CompletedTask;

            lock (_gate)
            {
                if (!_isArmed || _disposed)
                    return Task.CompletedTask;

                if (_hitCount > 0)
                {
                    _hitCount--;
                    return Task.CompletedTask;
                }

                _armTcs?.TrySetResult(CheckPointWaitResult.Reached);
                _armTcs = null;

                _blockedHitTcs ??= new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _blockedHitTcs.Task;
            }
        }

        public Task<CheckPointWaitResult> Arm(int requiredHits = 1)
        {
            if (requiredHits <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredHits), "requiredHits must be greater than zero.");
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                ResetUnderLock();

                _isArmed = true;
                _hitCount = requiredHits - 1;

                var tcs = new TaskCompletionSource<CheckPointWaitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _armTcs = tcs;
                return tcs.Task;
            }
        }

        public void Reset()
        {
            if (_disposed)
                return;

            lock (_gate)
            {
                if (_disposed)
                    return;

                ResetUnderLock();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                ResetUnderLock();
            }
        }

        private void ResetUnderLock()
        {
            _isArmed = false;
            _hitCount = 0;
            _epoch++;

            _armTcs?.TrySetResult(CheckPointWaitResult.Released);
            _armTcs = null;

            _blockedHitTcs?.TrySetResult(0);
            _blockedHitTcs = null;

            Monitor.PulseAll(_gate);
        }
    }
}
