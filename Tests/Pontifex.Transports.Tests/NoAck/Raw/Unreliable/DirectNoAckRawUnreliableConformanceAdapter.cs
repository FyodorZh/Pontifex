using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.NoAck.Raw.Unreliable;
using Pontifex.NoAck.Raw.Unreliable.Direct;
using Scriba;

namespace Pontifex.Tests.NoAck.Raw.Unreliable;

/// <summary>
/// Conformance topology adapter for the in-process Direct transport.
/// </summary>
public sealed class DirectNoAckRawUnreliableConformanceAdapter : INoAckRawUnreliableConformanceAdapter
{
    private readonly ILogger _logger;

    public DirectNoAckRawUnreliableConformanceAdapter(ILogger? logger = null)
    {
        _logger = logger ?? new Logger([]);
    }

    public INoAckRawUnreliableConformanceFixture CreateFixture(
        NoAckRawUnreliableConformanceFixtureOptions? options = null)
    {
        var memory = options?.MemoryRental ?? MemoryRental.Shared;
        return new Fixture(Guid.NewGuid().ToString("N"), _logger, memory);
    }

    private sealed class Fixture : INoAckRawUnreliableConformanceFixture
    {
        private readonly string _serverName;
        private readonly ILogger _logger;
        private readonly IMemoryRental _memory;
        private readonly List<INoAckRawUnreliableClient> _clients = [];
        private bool _disposed;

        public Fixture(string serverName, ILogger logger, IMemoryRental memory)
        {
            _serverName = serverName;
            _logger = logger;
            _memory = memory;
            Server = new NoAckRawUnreliableDirectServer(_serverName, _logger, _memory);
        }

        public INoAckRawUnreliableServer Server { get; }

        public INoAckRawUnreliableClient CreateClient()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var client = new NoAckRawUnreliableDirectClient(_serverName, _logger, _memory);
            _clients.Add(client);
            return client;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            ResetGates(Server);
            foreach (var client in _clients)
                ResetGates(client);

            foreach (var client in _clients)
                client.Stop();
            Server.Stop();
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

                if (control is INoAckRawUnreliableConformanceControl unreliableControl)
                {
                    unreliableControl.BeforeSendCommitGate.Reset();
                    unreliableControl.AfterSendCommitGate.Reset();
                    unreliableControl.AfterReceivedGate.Reset();
                }
            }
        }
    }
}
