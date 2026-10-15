using Actuarius.Memory;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording reliable server handler. Inherits the received/stopped/disconnected recording and buffer
/// cloning from <see cref="RecordingRawReliableHandler"/> and implements the server-side handler interface.
/// </summary>
internal sealed class RecordingRawReliableServerHandler : RecordingRawReliableHandler, IRawReliableServerHandler
{
    public RecordingRawReliableServerHandler(IMemoryRental memory)
        : base(memory)
    {
    }
}
