using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording acknowledgement server handler. Inherits the received/stopped/disconnected recording from
/// <see cref="RecordingRawReliableServerHandler"/> and records the acknowledgement handshake surface:
/// <see cref="OnConnected"/>, with an optional probe and an optional failure thrown from the start of
/// the callback.
/// </summary>
internal sealed class RecordingRawReliableAckServerHandler : RecordingRawReliableServerHandler, IRawReliableAckServerHandler
{
    public RecordingRawReliableAckServerHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    /// <summary>
    /// Gets or sets a probe invoked at the very start of <see cref="OnConnected"/>, before any state is
    /// recorded and before the optional failure is thrown.
    /// </summary>
    public Action? OnConnectedProbe { get; set; }

    public bool ThrowOnConnected { get; set; }

    public int ConnectedCount { get; private set; }

    public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
        OnConnectedProbe?.Invoke();

        if (ThrowOnConnected)
        {
            throw new InvalidOperationException("OnConnected test failure");
        }

        ConnectedCount++;
        ConnectedEndPoint = endPoint;
    }
}
