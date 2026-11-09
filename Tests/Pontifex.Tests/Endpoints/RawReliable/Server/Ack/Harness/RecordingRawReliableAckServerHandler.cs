using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording acknowledgement server handler. Inherits the received/stopped/disconnected recording from
/// <see cref="RecordingRawReliableServerHandler"/> and records the acknowledgement handshake surface:
/// <see cref="FillAckResponse"/> (optionally throwing) and <see cref="OnConnected"/>.
/// </summary>
internal sealed class RecordingRawReliableAckServerHandler : RecordingRawReliableServerHandler, IRawReliableAckServerHandler
{
    private readonly bool _throwOnFillAckResponse;

    public RecordingRawReliableAckServerHandler(IMemoryRental memory, bool throwOnFillAckResponse = false)
        : base(memory)
    {
        _throwOnFillAckResponse = throwOnFillAckResponse;
    }

    public int FillAckResponseCalls { get; private set; }

    public int ConnectedCount { get; private set; }

    public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

    public void FillAckResponse(UnionDataList ackData)
    {
        FillAckResponseCalls++;

        if (_throwOnFillAckResponse)
        {
            throw new InvalidOperationException("FillAckResponse test failure");
        }

        ackData.PutLast(new UnionData(0xCD));
    }

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
        ConnectedCount++;
        ConnectedEndPoint = endPoint;
    }
}
