using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableClientTransport{TClientHandler}"/>. It implements the
/// protected connection hooks, records their invocations, and exposes the protected state and
/// transitions the test-set needs. The double never detaches the attached endpoint: the base class
/// owns detachment after <see cref="DestroyTransport"/> completes.
/// </summary>
internal sealed class TestRawReliableClientTransport : RawReliableClientTransport<IRawReliableClientHandler>
{
    public TestRawReliableClientTransport(
        string typeName = "TestRawReliableClientTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared,
            new UnusedRawReliableConformanceControl())
    {
    }

    public override int MessageMaxByteSize => 1024;

    public override TransportType Type => TransportType.RawReliableAck;

    /// <summary>
    /// Gets or sets the result returned from <see cref="BeginConnect"/>.
    /// </summary>
    public Func<bool> BeginConnectBehavior { get; set; } = static () => true;

    /// <summary>
    /// Gets or sets the handler transformation applied by <see cref="SetupHandler"/>.
    /// </summary>
    public Func<IRawReliableClientHandler, IRawReliableClientHandler?> SetupHandlerBehavior { get; set; } =
        static handler => handler;

    public int BeginConnectCalls { get; private set; }

    public int OnReadyToConnectCalls { get; private set; }

    public int DestroyTransportCalls { get; private set; }

    public int SetupHandlerCalls { get; private set; }

    public IRawReliableClientHandler? LastSetupHandlerArgument { get; private set; }

    public StopReason? LastDestroyReason { get; private set; }

    /// <summary>
    /// Gets or sets an exception thrown from <see cref="DestroyTransport"/> after it records the
    /// attached endpoint. Used to verify that base-class detachment still runs from a finally block.
    /// </summary>
    public Exception? DestroyException { get; set; }

    /// <summary>
    /// Gets whether the configured <see cref="DestroyException"/> was actually thrown.
    /// </summary>
    public bool DestroyExceptionThrown { get; private set; }

    /// <summary>
    /// Gets the endpoint observed through <see cref="AttachedEndpoint"/> inside
    /// <see cref="DestroyTransport"/>, before the base class detaches it.
    /// </summary>
    public RawReliableClientEndpoint? EndpointDuringDestroy { get; private set; }

    /// <summary>
    /// Gets the value of <see cref="RawReliableClientTransport{TClientHandler}.IsConnected"/> observed
    /// inside <see cref="DestroyTransport"/>.
    /// </summary>
    public bool? IsConnectedDuringDestroy { get; private set; }

    /// <summary>
    /// Gets the shared event log used to assert callback ordering across the transport hooks, the
    /// recording handler, and the start callback supplied by the test.
    /// </summary>
    public List<string> EventLog { get; } = new();

    public new IRawReliableClientHandler? Handler => base.Handler;

    public RawReliableClientEndpoint? ExposedAttachedEndpoint => AttachedEndpoint;

    public void ExposedConnectionFinished(Action<IRawReliableClientHandler> endpointConstructor)
    {
        ConnectionFinished(endpointConstructor);
    }

    public void ExposedConnectionFailed()
    {
        ConnectionFailed();
    }

    public void ExposedAttachEndpoint(RawReliableClientEndpoint endpoint)
    {
        AttachEndpoint(endpoint);
    }

    protected override IRawReliableClientHandler? SetupHandler(IRawReliableClientHandler handler)
    {
        SetupHandlerCalls++;
        LastSetupHandlerArgument = handler;
        return SetupHandlerBehavior(handler);
    }

    protected override bool BeginConnect()
    {
        BeginConnectCalls++;
        EventLog.Add("beginConnect");
        return BeginConnectBehavior();
    }

    protected override void OnReadyToConnect()
    {
        OnReadyToConnectCalls++;
        EventLog.Add("readyToConnect");
    }

    protected override void DestroyTransport(StopReason reason)
    {
        DestroyTransportCalls++;
        LastDestroyReason = reason;
        EndpointDuringDestroy = AttachedEndpoint;
        IsConnectedDuringDestroy = IsConnected;
        EventLog.Add("destroyTransport");

        if (DestroyException != null)
        {
            DestroyExceptionThrown = true;
            throw DestroyException;
        }
    }

    /// <summary>
    /// A trivial control passed only because the production constructor currently requires a
    /// <see cref="RawReliableConformanceControl"/> instance. It is never armed, inspected, or asserted.
    /// </summary>
    private sealed class UnusedRawReliableConformanceControl : RawReliableConformanceControl
    {
    }
}
