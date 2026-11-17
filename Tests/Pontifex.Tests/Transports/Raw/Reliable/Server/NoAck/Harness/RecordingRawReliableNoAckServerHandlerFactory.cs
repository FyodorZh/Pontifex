using System;
using System.Threading;
using Pontifex.Raw.Reliable.NoAck;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Recording no-acknowledgement server handler factory for the server transport tests. It records the
/// number of <see cref="TryCreate"/> calls and the last remote endpoint and returns a configured
/// handler, a behavior result, or nothing. It never serializes the calls itself, so the transport's
/// own factory lock remains under test.
/// </summary>
internal sealed class RecordingRawReliableNoAckServerHandlerFactory
    : IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler>
{
    private readonly Func<IEndPoint, IRawReliableNoAckServerHandler?>? _behavior;

    private int _tryCreateCalls;

    private IEndPoint? _lastRemote;

    public RecordingRawReliableNoAckServerHandlerFactory(
        Func<IEndPoint, IRawReliableNoAckServerHandler?>? behavior = null)
    {
        _behavior = behavior;
    }

    /// <summary>
    /// Gets or sets the handler returned by <see cref="TryCreate"/> when no behavior is configured.
    /// </summary>
    public IRawReliableNoAckServerHandler? Handler { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="TryCreate"/> throws.
    /// </summary>
    public bool ThrowOnTryCreate { get; set; }

    /// <summary>
    /// Gets or sets the exception thrown when <see cref="ThrowOnTryCreate"/> is enabled.
    /// </summary>
    public Exception? TryCreateException { get; set; }

    public int TryCreateCalls => Volatile.Read(ref _tryCreateCalls);

    public IEndPoint? LastRemote => Volatile.Read(ref _lastRemote);

    public IRawReliableNoAckServerHandler? TryCreate(IEndPoint remote)
    {
        Interlocked.Increment(ref _tryCreateCalls);
        Volatile.Write(ref _lastRemote, remote);

        if (ThrowOnTryCreate)
        {
            throw TryCreateException ?? new InvalidOperationException("TryCreate test failure");
        }

        return _behavior != null ? _behavior(remote) : Handler;
    }
}
