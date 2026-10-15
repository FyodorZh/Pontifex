using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable handler. Inherits the received/stopped recording and buffer cloning from
/// <see cref="RecordingRawHandler"/> and adds the logical-disconnect notification.
/// </summary>
internal sealed class RecordingRawReliableHandler : RecordingRawHandler, IRawReliableHandler
{
    public RecordingRawReliableHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    public StopReason? DisconnectedReason { get; private set; }

    public int DisconnectedCount { get; private set; }

    public void OnDisconnected(StopReason reason)
    {
        DisconnectedReason = reason;
        DisconnectedCount++;
        RecordEvent("disconnected");
    }
}
