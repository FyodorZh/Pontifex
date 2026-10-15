using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable client handler. Inherits the received/stopped/disconnected recording and buffer
/// cloning from <see cref="RecordingRawReliableHandler"/> and implements the client-side handler interface.
/// It is the base for the acknowledgement client recording handler.
/// </summary>
internal class RecordingRawReliableClientHandler : RecordingRawReliableHandler, IRawReliableClientHandler
{
    public RecordingRawReliableClientHandler(IMemoryRental memory)
        : base(memory)
    {
    }
}
