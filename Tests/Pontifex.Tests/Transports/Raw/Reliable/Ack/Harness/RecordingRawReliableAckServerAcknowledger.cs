using System;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Recording acknowledger for the Ack server transport tests. It records the remote endpoint, the
/// acknowledgement buffer, and the buffer's reference count around the configured behavior. The
/// behavior owns the additional reference and should release it; the recorder releases it as a safety
/// net when the behavior does not.
/// </summary>
internal sealed class RecordingRawReliableAckServerAcknowledger
    : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
{
    private readonly Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?> _behavior;

    public RecordingRawReliableAckServerAcknowledger(
        Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?> behavior)
    {
        _behavior = behavior;
    }

    public int TryAckCalls { get; private set; }

    public IEndPoint? LastRemote { get; private set; }

    public UnionDataList? LastAckData { get; private set; }

    public int RefCountBefore { get; private set; }

    public int RefCountAfterBehavior { get; private set; }

    public int RefCountAfterRelease { get; private set; }

    public bool ReleasedByBehavior { get; private set; }

    public IRawReliableAckServerHandler? TryAck(IEndPoint remote, UnionDataList ackData)
    {
        TryAckCalls++;
        LastRemote = remote;
        LastAckData = ackData;
        RefCountBefore = ackData.RefCount.RefCount;

        try
        {
            return _behavior(remote, ackData);
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
