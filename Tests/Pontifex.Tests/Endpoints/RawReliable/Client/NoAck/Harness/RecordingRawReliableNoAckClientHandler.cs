using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.NoAck;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording no-acknowledgement client handler. Inherits the received/stopped/disconnected recording from
/// <see cref="RecordingRawReliableClientHandler"/> and records the logical connection surface. It re-implements
/// <see cref="IRawReliableHandler.OnDisconnected"/> explicitly so the test can observe <c>IsConnected</c>
/// during the callback; the inherited member is not virtual.
/// </summary>
internal sealed class RecordingRawReliableNoAckClientHandler : RecordingRawReliableClientHandler, IRawReliableNoAckClientHandler
{
    public RecordingRawReliableNoAckClientHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    public int ConnectedCount { get; private set; }

    public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

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

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
        ConnectedCount++;
        ConnectedEndPoint = endPoint;
        RecordEvent("connected");

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
