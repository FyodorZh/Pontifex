using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawUnreliableServerTransport{TFactory}"/>. It implements the carrier hooks,
/// records their invocations, exposes the admission surface, and builds
/// <see cref="TestRawUnreliableServerEndpoint"/> sessions that share the transport-owned send budget.
/// </summary>
internal sealed class TestRawUnreliableServerTransport
    : RawUnreliableServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IDisposable
{
    private readonly long _sendBudgetBytes;
    private readonly int _sendBudgetMessages;
    private readonly List<RawUnreliableServerEndpoint> _createdEndpoints = new();

    public TestRawUnreliableServerTransport(
        string typeName = "TestRawUnreliableServerTransport",
        int messageMaxByteSize = 1024,
        long? sendBudgetBytes = null,
        int? sendBudgetMessages = null,
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared)
    {
        MessageMaxByteSize = messageMaxByteSize;
        _sendBudgetMessages = sendBudgetMessages ?? 100;
        _sendBudgetBytes = sendBudgetBytes ?? messageMaxByteSize * (long)_sendBudgetMessages;
    }

    public override int MessageMaxByteSize { get; }

    public override TransportType Type => TransportType.RawUnreliable;

    protected override long SendBudgetBytes => _sendBudgetBytes;

    protected override int SendBudgetMessages => _sendBudgetMessages;

    /// <summary>
    /// Gets or sets the behavior that replaces the bound factory. It receives the source exactly like
    /// <see cref="InvokeFactory"/>.
    /// </summary>
    public Func<IEndPoint, IRawUnreliableHandler?>? FactoryBehavior { get; set; }

    /// <summary>
    /// Gets or sets an exception thrown from <see cref="CreateEndpoint"/>.
    /// </summary>
    public Exception? CreateEndpointException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="CreateEndpoint"/> declines the route by returning
    /// <see langword="null"/> after recording the call.
    /// </summary>
    public bool CreateEndpointReturnsNull { get; set; }

    public Func<bool> StartCarrierBehavior { get; set; } = static () => true;

    public Exception? StartCarrierException { get; set; }

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public StopReason? LastStopCarrierReason { get; private set; }

    public bool? IsStoppingDuringStopCarrier { get; private set; }

    public int InvokeFactoryCalls { get; private set; }

    public int CreateEndpointCalls { get; private set; }

    public List<IEndPoint> FactorySources { get; } = new();

    public List<IRawUnreliableHandler> CreatedHandlers { get; } = new();

    public List<IEndPoint> CreatedSources { get; } = new();

    public List<UnionDataList> CreatedTriggers { get; } = new();

    public List<SendBudget> CreatedSendBudgets { get; } = new();

    public List<long> CreatedLocalBudgetCapacities { get; } = new();

    public IReadOnlyList<RawUnreliableServerEndpoint> CreatedEndpoints => _createdEndpoints;

    public bool ExposedIsStopping => IsStopping;

    public bool ExposedAdmissionLockHeld => AdmissionLockHeldForTests;

    public int ExposedRouteCount => RouteCount;

    public long ExposedSendBudgetBytes => SendBudgetBytes;

    public int ExposedSendBudgetMessages => SendBudgetMessages;

    public SendBudget? ExposedSharedSendBudget => SharedSendBudget;

    public bool ExposedHasStartBeenAttempted => HasStartBeenAttempted;

    public bool ExposedTryInitializeServer(Func<IEndPoint, IRawUnreliableHandler?> factory)
    {
        return TryInitializeServer(factory);
    }

    public void ExposedOnCarrierInbound(IEndPoint source, UnionDataList trigger)
    {
        OnCarrierInbound(source, trigger);
    }

    /// <summary>
    /// Stops and drains every session endpoint created by this double. Tests call it after their
    /// assertions so no trigger or pending transition outlives them.
    /// </summary>
    public void StopAndDrainAll()
    {
        foreach (RawUnreliableServerEndpoint endpoint in _createdEndpoints)
        {
            if (endpoint is TestRawUnreliableServerEndpoint testEndpoint)
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
        StopCarrierCalls++;
        LastStopCarrierReason = reason;
        IsStoppingDuringStopCarrier = IsStopping;
    }

    protected override IRawUnreliableHandler? InvokeFactory(
        Func<IEndPoint, IRawUnreliableHandler?> factory,
        IEndPoint source,
        UnionDataList trigger)
    {
        InvokeFactoryCalls++;
        FactorySources.Add(source);

        return FactoryBehavior != null ? FactoryBehavior(source) : factory(source);
    }

    protected override RawUnreliableServerEndpoint? CreateEndpoint(IRawUnreliableHandler handler,
        IEndPoint source, UnionDataList trigger)
    {
        CreateEndpointCalls++;
        CreatedHandlers.Add(handler);
        CreatedSources.Add(source);
        CreatedTriggers.Add(trigger);

        if (CreateEndpointReturnsNull)
        {
            return null;
        }

        SendBudget budget = SharedSendBudget
                            ?? throw new InvalidOperationException("The shared send budget is missing.");
        CreatedSendBudgets.Add(budget);
        CreatedLocalBudgetCapacities.Add(SendBudgetBytes);

        if (CreateEndpointException != null)
        {
            throw CreateEndpointException;
        }

        var endpoint = new TestRawUnreliableServerEndpoint(
            handler,
            trigger,
            budget,
            remoteEndPoint: source,
            transportName: Name,
            messageMaxByteSize: MessageMaxByteSize,
            bufferCapacityBytes: SendBudgetBytes,
            memory: Memory,
            log: Log);
        _createdEndpoints.Add(endpoint);
        return endpoint;
    }

    public void Dispose()
    {
        foreach (RawUnreliableServerEndpoint endpoint in _createdEndpoints)
        {
            if (endpoint is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _createdEndpoints.Clear();
    }
}
