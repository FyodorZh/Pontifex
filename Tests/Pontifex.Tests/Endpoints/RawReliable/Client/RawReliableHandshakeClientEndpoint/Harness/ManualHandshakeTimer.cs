using System;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Manual handshake timer used by the endpoint doubles. The test fires it explicitly, so the handshake
/// deadline is deterministic and never depends on real time.
/// </summary>
internal sealed class ManualHandshakeTimer : IDisposable
{
    private readonly Action _onTimeout;

    public ManualHandshakeTimer(Action onTimeout)
    {
        _onTimeout = onTimeout;
    }

    public bool IsDisposed { get; private set; }

    public void Fire()
    {
        if (IsDisposed)
        {
            return;
        }

        _onTimeout();
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
