using System;
using System.Threading;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Recording reliable server handler factory for the server transport tests. It records the
/// number of <see cref="TryCreate"/> calls and the last remote endpoint and returns a configured
/// handler, a behavior result, or nothing. It never serializes the calls itself, so the transport's
/// own factory lock remains under test.
/// </summary>
internal sealed class RecordingRawReliableAdmissionServerHandlerFactory
    : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
{
    private readonly Func<IEndPoint, IRawReliableServerHandler?>? _behavior;

    private int _tryCreateCalls;

    private IEndPoint? _lastRemote;

    public RecordingRawReliableAdmissionServerHandlerFactory(
        Func<IEndPoint, IRawReliableServerHandler?>? behavior = null)
    {
        _behavior = behavior;
    }

    /// <summary>
    /// Gets or sets the handler returned by <see cref="TryCreate"/> when no behavior is configured.
    /// </summary>
    public IRawReliableServerHandler? Handler { get; set; }

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

    public IRawReliableServerHandler? TryCreate(IEndPoint remote)
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
