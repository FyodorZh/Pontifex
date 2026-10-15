using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording acknowledgement client handler. Inherits the received/stopped/disconnected recording from
/// <see cref="RecordingRawReliableClientHandler"/> and records the acknowledgement handshake surface:
/// <see cref="FillAckData"/> (optionally throwing) and <see cref="OnConnected"/>, whose acknowledgement
/// response payload is copied out before the buffer is released.
/// </summary>
internal sealed class RecordingRawReliableAckClientHandler : RecordingRawReliableClientHandler, IRawReliableAckClientHandler
{
    public RecordingRawReliableAckClientHandler(IMemoryRental memory, bool throwOnFillAckData = false)
        : base(memory)
    {
        ThrowOnFillAckData = throwOnFillAckData;
    }

    public bool ThrowOnFillAckData { get; }

    public int FillAckDataCalls { get; private set; }

    public int ConnectedCount { get; private set; }

    public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

    /// <summary>
    /// Gets the payload bytes of the last acknowledgement response delivered through
    /// <see cref="OnConnected"/>, or <see langword="null"/> when none was delivered.
    /// </summary>
    public byte[]? AckResponsePayload { get; private set; }

    public void FillAckData(UnionDataList ackData)
    {
        FillAckDataCalls++;

        if (ThrowOnFillAckData)
        {
            throw new InvalidOperationException("FillAckData test failure");
        }

        ackData.PutLast(new UnionData(0xAB));
    }

    public void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
    {
        ConnectedCount++;
        ConnectedEndPoint = endPoint;

        AckResponsePayload = ackResponse.Elements.Count > 0
            ? new[] { ackResponse.Elements[0].Alias.ByteValue }
            : Array.Empty<byte>();

        ackResponse.Release();
    }
}
