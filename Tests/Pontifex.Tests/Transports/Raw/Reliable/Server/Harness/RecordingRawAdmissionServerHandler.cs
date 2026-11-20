using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Hand-written plain reliable server handler used by the shared admission tests. It records the
/// terminal and data callbacks without any gate, so a second <see cref="OnStopped"/> would be counted
/// twice; the protocol layers own the stop-once assertions. A delivered buffer is owned by the
/// handler and is released immediately because no test inspects the payload.
/// </summary>
internal sealed class RecordingRawAdmissionServerHandler : IRawReliableServerHandler
{
    public int StoppedCount { get; private set; }

    public StopReason? LastStopReason { get; private set; }

    public int ReceivedCount { get; private set; }

    public int DisconnectedCount { get; private set; }

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
    }

    public void OnDisconnected(StopReason reason)
    {
        DisconnectedCount++;
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        ReceivedCount++;
        receivedBuffer.Release();
    }

    public void OnStopped(StopReason reason)
    {
        StoppedCount++;
        LastStopReason = reason;
    }
}
