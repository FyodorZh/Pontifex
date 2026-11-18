using System;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Recording acknowledger for the Ack server transport tests. It records the remote endpoint, the
/// acknowledgement buffer, the buffer's reference count around the configured behavior, and the returned
/// response. The behavior owns the additional reference and should release it; the recorder releases it as
/// a safety net when the behavior does not.
/// </summary>
internal sealed class RecordingRawReliableAckServerAcknowledger
    : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
{
    private readonly Func<IEndPoint, UnionDataList, (IRawReliableAckServerHandler handler, UnionDataList ackResponse)?> _behavior;

    public RecordingRawReliableAckServerAcknowledger(
        Func<IEndPoint, UnionDataList, (IRawReliableAckServerHandler handler, UnionDataList ackResponse)?> behavior)
    {
        _behavior = behavior;
    }

    public int TryAckCalls { get; private set; }

    public IEndPoint? LastRemote { get; private set; }

    public UnionDataList? LastAckData { get; private set; }

    public UnionDataList? LastAckResponse { get; private set; }

    public int RefCountBefore { get; private set; }

    public int RefCountAfterBehavior { get; private set; }

    public int RefCountAfterRelease { get; private set; }

    public bool ReleasedByBehavior { get; private set; }

    public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryAck(
        IEndPoint remote, UnionDataList ackData)
    {
        TryAckCalls++;
        LastRemote = remote;
        LastAckData = ackData;
        RefCountBefore = ackData.RefCount.RefCount;

        try
        {
            var result = _behavior(remote, ackData);
            LastAckResponse = result?.ackResponse;
            return result;
        }
        finally
        {
            RefCountAfterBehavior = ackData.RefCount.RefCount;
            ReleasedByBehavior = RefCountAfterBehavior < RefCountBefore;

            if (!ReleasedByBehavior)
            {
                ackData.Release();
            }

            RefCountAfterRelease = ackData.RefCount.RefCount;
        }
    }
}
