using System;
using System.Collections.Generic;
using System.Linq;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Base implementation of a RawReliableAck conformance fixture. Owns client and
/// endpoint tracking, conformance-gate reset, and disposal. Concrete adapters
/// supply the server, client construction, and the InitServer mapping.
/// </summary>
public abstract class RawReliableAckConformanceFixture : IRawReliableAckConformanceFixture
{
    private readonly List<IRawReliableAckClient> _clients = new();
    private readonly List<IRawReliableEndpoint> _endpoints = new();
    private readonly object _endpointsLock = new();
    private bool _disposed;

    protected RawReliableAckConformanceFixture(
        IRawReliableAckServer server, ILogger logger, IMemoryRental memory)
    {
        Server = server;
        Logger = logger;
        Memory = memory;
    }

    public IRawReliableAckServer Server { get; }

    protected ILogger Logger { get; }

    protected IMemoryRental Memory { get; }

    public bool InitServer(
        IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
    {
        if (acknowledger == null!)
            throw new ArgumentNullException(nameof(acknowledger));
        return Server.Init(acknowledger);
    }

    public IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> CreateSimpleAcknowledger(
        Func<UnionDataList, IRawReliableAckServerHandler?> tryAck)
    {
        return new SimpleAcknowledger(tryAck, Logger);
    }

    public IRawReliableAckClient CreateClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var client = CreateClientCore();
        _clients.Add(client);
        return client;
    }

    protected abstract IRawReliableAckClient CreateClientCore();

    public void TrackEndpoint(IRawReliableEndpoint endpoint)
    {
        lock (_endpointsLock) { _endpoints.Add(endpoint); }
    }

    private IReadOnlyList<IRawReliableEndpoint> TrackedEndpoints
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
        ResetTransportGates(Server);
        foreach (var client in _clients)
            ResetTransportGates(client);

        foreach (var client in _clients)
            client.Stop();
        Server.Stop();
    }

    private static void ResetEndpointGates(IRawReliableEndpoint endpoint)
    {
        var controls = new List<IControl>();
        endpoint.GetControls(controls);

        foreach (var control in controls)
        {
            if (control is IRawReliableAckEndpointConformanceControl epControl)
            {
                epControl.BeforeEndpointDisconnectStateTransitionGate.Reset();
                epControl.BeforeHandlerDisconnectedGate.Reset();
                epControl.BeforeHandlerStoppedGate.Reset();
                epControl.BeforeSendCommitGate.Reset();
                epControl.AfterSendCommitGate.Reset();
                epControl.AfterReceivedGate.Reset();
            }
        }
    }

    private static void ResetTransportGates(ITransport transport)
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

            if (control is IRawReliableAckTransportConformanceControl rawReliableControl)
            {
                rawReliableControl.BeforeAcknowledgerGate.Reset();
                rawReliableControl.BeforeAckResponseCommitGate.Reset();
                rawReliableControl.BeforeHandlerConnectedGate.Reset();
            }
        }
    }

    /// <summary>
    /// Simple TryAck wrapper that delegates to a lambda, handles buffer
    /// release, and catches/logs exceptions.
    /// </summary>
    private sealed class SimpleAcknowledger : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        private readonly Func<UnionDataList, IRawReliableAckServerHandler?> _tryAck;
        private readonly ILogger _logger;

        public SimpleAcknowledger(
            Func<UnionDataList, IRawReliableAckServerHandler?> tryAck, ILogger logger)
        {
            _tryAck = tryAck;
            _logger = logger;
        }

        public IRawReliableAckServerHandler? TryAck(UnionDataList ackData)
        {
            try
            {
                return _tryAck(ackData);
            }
            catch (Exception ex)
            {
                _logger.wtf(ex);
                ackData.Release();
                return null;
            }
        }
    }
}
