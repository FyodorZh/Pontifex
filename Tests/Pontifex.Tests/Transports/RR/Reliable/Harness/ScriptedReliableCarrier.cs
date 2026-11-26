using System;
using System.Collections.Generic;
using Pontifex.Endpoints;
using Pontifex.RR.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// One sent reliable frame recorded by <see cref="ScriptedReliableCarrier"/>.
/// </summary>
internal sealed class ScriptedReliableFrame
{
    public ScriptedReliableFrame(RRReliableFrameType frameType, ushort requestId, uint ordinal,
        RRReliableCompletionType completion, int[] payload)
    {
        FrameType = frameType;
        RequestId = requestId;
        Ordinal = ordinal;
        Completion = completion;
        Payload = payload;
    }

    public RRReliableFrameType FrameType { get; }

    public ushort RequestId { get; }

    public uint Ordinal { get; }

    public RRReliableCompletionType Completion { get; }

    public int[] Payload { get; }
}

/// <summary>
/// Deterministic scripted carrier shared by the client and server layers.
/// </summary>
/// <remarks>
/// The carrier records and queues every frame instead of delivering it, so a test decides when and
/// how a frame reaches its peer. A test may replace either direction with a delegate to drop a
/// frame, return a non-<c>Ok</c> result, or deliver it synchronously. Frames left in an inbox are
/// released by <see cref="Dispose"/>.
/// </remarks>
internal sealed class ScriptedReliableCarrier : IDisposable
{
    private readonly Queue<UnionDataList> _toServer = new Queue<UnionDataList>();
    private readonly Queue<UnionDataList> _toClient = new Queue<UnionDataList>();

    public ScriptedReliableCarrier()
    {
        ClientEndPoint = new GuidEndPoint(Guid.NewGuid());
        ServerEndPoint = new GuidEndPoint(Guid.NewGuid());
    }

    public IEndPoint ClientEndPoint { get; }

    public IEndPoint ServerEndPoint { get; }

    public List<ScriptedReliableFrame> SentByClient { get; } = new List<ScriptedReliableFrame>();

    public List<ScriptedReliableFrame> SentByServer { get; } = new List<ScriptedReliableFrame>();

    public Func<UnionDataList, SendResult>? ClientSendOverride { get; set; }

    public Func<IEndPoint, UnionDataList, SendResult>? ServerSendOverride { get; set; }

    public int PendingToServer => _toServer.Count;

    public int PendingToClient => _toClient.Count;

    public SendResult SendFromClient(UnionDataList frame)
    {
        if (!Capture(frame, SentByClient, out _))
        {
            frame.Release();
            return SendResult.Error;
        }

        Func<UnionDataList, SendResult>? overrideSend = ClientSendOverride;

        if (overrideSend != null)
        {
            return overrideSend(frame);
        }

        _toServer.Enqueue(frame);
        return SendResult.Ok;
    }

    public SendResult SendFromServer(IEndPoint destination, UnionDataList frame)
    {
        if (!Capture(frame, SentByServer, out _))
        {
            frame.Release();
            return SendResult.Error;
        }

        Func<IEndPoint, UnionDataList, SendResult>? overrideSend = ServerSendOverride;

        if (overrideSend != null)
        {
            return overrideSend(destination, frame);
        }

        _toClient.Enqueue(frame);
        return SendResult.Ok;
    }

    public UnionDataList DequeueToServer()
    {
        return _toServer.Dequeue();
    }

    public UnionDataList DequeueToClient()
    {
        return _toClient.Dequeue();
    }

    public bool TryDequeueToServer(out UnionDataList frame)
    {
        if (_toServer.Count == 0)
        {
            frame = null!;
            return false;
        }

        frame = _toServer.Dequeue();
        return true;
    }

    public bool TryDequeueToClient(out UnionDataList frame)
    {
        if (_toClient.Count == 0)
        {
            frame = null!;
            return false;
        }

        frame = _toClient.Dequeue();
        return true;
    }

    public void Clear()
    {
        SentByClient.Clear();
        SentByServer.Clear();

        while (_toServer.Count > 0)
        {
            _toServer.Dequeue().Release();
        }

        while (_toClient.Count > 0)
        {
            _toClient.Dequeue().Release();
        }
    }

    public void Dispose()
    {
        Clear();
    }

    private static bool Capture(UnionDataList frame, List<ScriptedReliableFrame> log,
        out ScriptedReliableFrame record)
    {
        if (!RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header))
        {
            record = null!;
            return false;
        }

        var payload = new List<int>();

        for (int index = 0; index < frame.Elements.Count; index++)
        {
            UnionData data = frame.Elements[index];
            payload.Add(data.Type == UnionDataType.Int ? data.Alias.IntValue : int.MinValue);
        }

        record = new ScriptedReliableFrame(header.FrameType, header.RequestId, header.Ordinal,
            header.Completion, payload.ToArray());
        log.Add(record);

        switch (header.FrameType)
        {
            case RRReliableFrameType.Request:
                RRReliableFrame.EncodeRequest(frame, header.RequestId);
                break;
            case RRReliableFrameType.Response:
                RRReliableFrame.EncodeResponse(frame, header.RequestId, header.Ordinal);
                break;
            case RRReliableFrameType.Finish:
                RRReliableFrame.EncodeFinish(frame, header.RequestId, header.Completion);
                break;
            case RRReliableFrameType.Cancel:
                RRReliableFrame.EncodeCancel(frame, header.RequestId);
                break;
            default:
                return false;
        }

        return true;
    }
}
