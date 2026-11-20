using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable client handler. Inherits the received/stopped/disconnected recording and buffer
/// cloning from <see cref="RecordingRawReliableHandler"/> and records the logical connection surface. It is
/// the base for the handshake client recording handler.
/// </summary>
internal class RecordingRawReliableClientHandler : RecordingRawReliableHandler, IRawReliableClientHandler
{
    public RecordingRawReliableClientHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    public int ConnectedCount { get; private set; }

    public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

    public virtual void OnConnected(IRawReliableEndpoint endPoint)
    {
        ConnectedCount++;
        ConnectedEndPoint = endPoint;
        RecordEvent("connected");
    }
}
