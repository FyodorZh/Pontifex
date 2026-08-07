using System;
using System.Collections.Generic;
using System.Linq;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Base implementation of a RawUnreliable conformance fixture. Owns client and
/// endpoint tracking, conformance-gate reset, and disposal. Variant carriers
/// supply the server, the client construction, and the InitServer mapping.
/// </summary>
public abstract class RawUnreliableConformanceFixture<TServer> : IRawUnreliableConformanceFixture<TServer>
    where TServer : IRawUnreliableTransport
{
    private readonly List<IRawUnreliableClient> _clients = new();
    private readonly List<IRawUnreliableEndpoint> _endpoints = new();
    private readonly object _endpointsLock = new();
    private bool _disposed;

    protected RawUnreliableConformanceFixture(TServer server, ILogger logger, IMemoryRental memory)
    {
        Server = server;
        Logger = logger;
        Memory = memory;
    }

    public TServer Server { get; }

    protected ILogger Logger { get; }

    protected IMemoryRental Memory { get; }

    public bool InitServer(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory)
    {
        if (factory == null!)
            throw new ArgumentNullException(nameof(factory));
        return InitServerCore(factory);
    }

    protected abstract bool InitServerCore(Func<IEndPoint, UnionDataList?, IRawUnreliableHandler?> factory);

    public IRawUnreliableClient CreateClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var client = CreateClientCore();
        _clients.Add(client);
        return client;
    }

    protected abstract IRawUnreliableClient CreateClientCore();

    public void TrackEndpoint(IRawUnreliableEndpoint endpoint)
    {
        lock (_endpointsLock) { _endpoints.Add(endpoint); }
    }

    public IReadOnlyList<IRawUnreliableEndpoint> TrackedEndpoints
    {
        get { lock (_endpointsLock) { return _endpoints.ToArray(); } }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var endpoint in TrackedEndpoints)
            ResetEndpointGates(endpoint);
        ResetGates(Server);
        foreach (var client in _clients)
            ResetGates(client);

        foreach (var client in _clients)
            client.Stop();
        Server.Stop();
    }

    private static void ResetEndpointGates(IRawUnreliableEndpoint endpoint)
    {
        var controls = new List<IControl>();
        endpoint.GetControls(controls);

        foreach (var control in controls)
        {
            if (control is IRawUnreliableEndpointConformanceControl epControl)
            {
                epControl.BeforeEndpointStopStateTransitionGate.Reset();
                epControl.BeforeHandlerStoppedGate.Reset();
                epControl.BeforeSendCommitGate.Reset();
                epControl.AfterSendCommitGate.Reset();
                epControl.AfterReceivedGate.Reset();
            }
        }
    }

    private static void ResetGates(ITransport transport)
    {
        var controls = new List<IControl>();
        transport.GetControls(controls);

        foreach (var control in controls)
        {
            if (control is IConformanceControl transportControl)
            {
                transportControl.BeforeStopStateTransitionGate.Reset();
                transportControl.BeforeStoppedCallbackGate.Reset();
            }

            if (control is IRawUnreliableTransportConformanceControl rawUnreliableControl)
            {
                rawUnreliableControl.BeforeHandlerFactoryGate.Reset();
                rawUnreliableControl.BeforeHandlerStartedGate.Reset();
            }
        }
    }
}
