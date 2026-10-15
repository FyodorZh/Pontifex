using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable server handler. Inherits the received/stopped/disconnected recording and buffer
/// cloning from <see cref="RecordingRawReliableHandler"/> and implements the server-side handler interface.
/// It is the base for the acknowledgement server recording handler.
/// </summary>
internal class RecordingRawReliableServerHandler : RecordingRawReliableHandler, IRawReliableServerHandler
{
    public RecordingRawReliableServerHandler(IMemoryRental memory)
        : base(memory)
    {
    }
}
