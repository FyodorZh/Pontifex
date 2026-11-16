using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawUnreliableClientTransport"/>. It implements the protected transport
/// hooks, records their invocations, and lets a test choose the endpoint returned by
/// <see cref="CreateEndpoint"/> or make the factory throw.
/// </summary>
internal sealed class TestRawUnreliableClientTransport : RawUnreliableClientTransport, IDisposable
{
    private readonly List<RawUnreliableEndpoint> _createdEndpoints = new();

    public TestRawUnreliableClientTransport(
        string typeName = "TestRawUnreliableClientTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 1024;

    public override TransportType Type => TransportType.RawUnreliableAck;

    /// <summary>
    /// Gets or sets the endpoint factory. When <see langword="null"/> the double creates a
    /// <see cref="TestRawUnreliableEndpoint"/> for the handler.
    /// </summary>
    public Func<IRawUnreliableHandler, RawUnreliableEndpoint>? EndpointFactory { get; set; }

    /// <summary>
    /// Gets or sets an exception thrown from <see cref="CreateEndpoint"/>, modelling a carrier that cannot
    /// create its endpoint.
    /// </summary>
    public Exception? CreateEndpointException { get; set; }

    /// <summary>
    /// Gets or sets the result returned from <see cref="StartCarrier"/>.
    /// </summary>
    public Func<bool> StartCarrierBehavior { get; set; } = static () => true;

    /// <summary>
    /// Gets or sets an exception thrown from <see cref="StartCarrier"/>.
    /// </summary>
    public Exception? StartCarrierException { get; set; }

    /// <summary>
    /// Gets or sets a probe invoked inside <see cref="StopCarrier"/>, before the call is recorded. A test
    /// uses it to observe what the transport did before it stopped the carrier.
    /// </summary>
    public Action? StopCarrierProbe { get; set; }

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public StopReason? LastStopCarrierReason { get; private set; }

    public int CreateEndpointCalls { get; private set; }

    public IRawUnreliableHandler? LastCreateEndpointHandler { get; private set; }

    public new IRawUnreliableHandler? Handler => base.Handler;

    public RawUnreliableEndpoint? ExposedAttachedEndpoint => AttachedEndpoint;

    public IReadOnlyList<RawUnreliableEndpoint> CreatedEndpoints => _createdEndpoints;

    /// <summary>
    /// Stops and drains the endpoint created by this double. Tests call it after their assertions so no
    /// pending transition outlives them.
    /// </summary>
    public void StopAndDrainAll()
    {
        foreach (RawUnreliableEndpoint endpoint in _createdEndpoints)
        {
            if (endpoint is TestRawUnreliableEndpoint testEndpoint)
            {
                testEndpoint.StopOneShot();
                testEndpoint.DrainAll();
            }
        }
    }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;

        if (StartCarrierException != null)
        {
            throw StartCarrierException;
        }

        return StartCarrierBehavior();
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierProbe?.Invoke();
        StopCarrierCalls++;
        LastStopCarrierReason = reason;
    }

    protected override RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler)
    {
        CreateEndpointCalls++;
        LastCreateEndpointHandler = handler;

        if (CreateEndpointException != null)
        {
            throw CreateEndpointException;
        }

        RawUnreliableEndpoint endpoint = EndpointFactory != null
            ? EndpointFactory(handler)
            : new TestRawUnreliableEndpoint(handler, transportName: Name, memory: Memory, log: Log);
        _createdEndpoints.Add(endpoint);
        return endpoint;
    }

    public void Dispose()
    {
        foreach (RawUnreliableEndpoint endpoint in _createdEndpoints)
        {
            if (endpoint is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _createdEndpoints.Clear();
    }
}
