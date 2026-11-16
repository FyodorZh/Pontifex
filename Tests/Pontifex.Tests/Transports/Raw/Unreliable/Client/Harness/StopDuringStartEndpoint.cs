using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Client endpoint double that stops itself at the end of its own start. Its serial queue is the production
/// inline queue, so the terminal transition runs synchronously inside <see cref="Pontifex.Raw.RawEndpoint.Start"/>
/// while the client transport still has a start in progress. That window cannot be produced with the
/// manually drained endpoint double shared by the endpoints suite.
/// </summary>
internal sealed class StopDuringStartEndpoint : RawUnreliableEndpoint
{
    private readonly List<StopReason> _stoppedReasons = new();

    public StopDuringStartEndpoint(
        IRawUnreliableHandler handler,
        StopReason stopReason,
        string transportName = "TestRawUnreliableClientTransport",
        IMemoryRental? memory = null,
        ILogger? log = null)
        : base(
            transportName,
            VoidEndPoint.Instance,
            handler,
            1024,
            1024 * 1024,
            16,
            1024 * 1024,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]))
    {
        StopReason = stopReason;
        // Subscribed after the base subscription, so the inbox release always runs before this record.
        Stopped += _stoppedReasons.Add;
    }

    public StopReason StopReason { get; }

    public IReadOnlyList<StopReason> StoppedReasons => _stoppedReasons;

    protected override void BeginStart()
    {
        base.BeginStart();
        // Runs after the flush action of the inherited start, inside the same drain, so the handler
        // observes OnStarted before the endpoint terminal notification. Scheduling instead of calling
        // Stop directly is required: a stop latched before the finish-start action would suppress
        // OnStarted.
        _actionQueue.Schedule(() => ((IRawUnreliableEndpoint)this).Stop(StopReason));
    }

    protected override void DoSendRaw(UnionDataList bufferToSend)
    {
        bufferToSend.Release();
    }
}
