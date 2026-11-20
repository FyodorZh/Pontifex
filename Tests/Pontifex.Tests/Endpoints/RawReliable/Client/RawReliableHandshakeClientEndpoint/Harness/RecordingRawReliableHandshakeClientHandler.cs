using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable client handler. Inherits the received/stopped/disconnected recording from
/// <see cref="RecordingRawReliableClientHandler"/> and records the logical connection surface. It re-implements
/// <see cref="IRawReliableHandler.OnDisconnected"/> explicitly so the test can observe <c>IsConnected</c>
/// during the callback; the inherited member is not virtual.
/// </summary>
internal sealed class RecordingRawReliableHandshakeClientHandler : RecordingRawReliableClientHandler, IRawReliableClientHandler
{
    public RecordingRawReliableHandshakeClientHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    /// <summary>
    /// Gets or sets the probe evaluated during <see cref="IRawReliableHandler.OnDisconnected"/>; typically reads
    /// the endpoint's <c>IsConnected</c>.
    /// </summary>
    public Func<bool>? IsConnectedProbe { get; set; }

    /// <summary>
    /// Gets the value observed through <see cref="IsConnectedProbe"/> during the disconnect callback, or
    /// <see langword="null"/> when the probe was not set.
    /// </summary>
    public bool? IsConnectedDuringDisconnected { get; private set; }

    public bool ThrowOnConnected { get; set; }

    public Exception? ConnectedException { get; set; }

    public override void OnConnected(IRawReliableEndpoint endPoint)
    {
        base.OnConnected(endPoint);

        if (ThrowOnConnected)
        {
            throw ConnectedException ?? new InvalidOperationException("OnConnected test failure");
        }
    }

    void IRawReliableHandler.OnDisconnected(StopReason reason)
    {
        IsConnectedDuringDisconnected = IsConnectedProbe?.Invoke();
        base.OnDisconnected(reason);
    }
}
