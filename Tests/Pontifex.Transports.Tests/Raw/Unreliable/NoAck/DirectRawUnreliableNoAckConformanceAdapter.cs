using System.Collections.Generic;
using System.Linq;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Raw.Unreliable.NoAck.Direct;
using Scriba;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectRawUnreliableNoAckConformanceAdapter : IRawUnreliableNoAckConformanceAdapter
{
    private readonly ILogger _logger;

    public DirectRawUnreliableNoAckConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public IRawUnreliableNoAckConformanceFixture CreateFixture(
        RawUnreliableNoAckConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        return new Fixture(Guid.NewGuid().ToString("N"), _logger, memory);
    }

    private sealed class Fixture : IRawUnreliableNoAckConformanceFixture
    {
        private readonly string _serverName;
        private readonly ILogger _logger;
        private readonly IMemoryRental _memory;
        private readonly List<IRawUnreliableNoAckClient> _clients = [];
        private readonly List<IRawUnreliableEndpoint> _endpoints = [];
        private readonly object _endpointsLock = new();
        private bool _disposed;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
        {
            _serverName = serverName;
            _logger = logger;
            _memory = memory;
            Server = new RawUnreliableNoAckDirectServer(_serverName, _logger, _memory);
        }

        public IRawUnreliableNoAckServer Server { get; }

        public IRawUnreliableNoAckClient CreateClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var client = new RawUnreliableNoAckDirectClient(_serverName, _logger, _memory);
            _clients.Add(client);
            return client;
        }

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
                if (control is IRawUnreliableNoAckEndpointConformanceControl epControl)
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

                if (control is IRawUnreliableNoAckTransportConformanceControl noAckControl)
                {
                    noAckControl.BeforeHandlerFactoryGate.Reset();
                    noAckControl.BeforeHandlerStartedGate.Reset();
                }
            }
        }
    }
}
