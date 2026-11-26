using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Reconnectable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Reconnectable.Harness
{
    /// <summary>
    /// A fully linked reconnectable client/server pair over synchronous fake physical transports and a
    /// hand-driven periodic driver. Both sides negotiate exactly like the production transports.
    /// </summary>
    internal sealed class ReconnectablePair : IDisposable
    {
        private readonly RawReliableReconnectableServer _server;
        private readonly RawReliableReconnectableClient _client;
        private readonly short _clientVersion;
        private readonly List<FakeReconnectableServerSession> _sessions =
            new List<FakeReconnectableServerSession>();

        public ReconnectablePair(
            short clientVersion = ReconnectableInfo.NegotiationVersion,
            short serverVersion = ReconnectableInfo.NegotiationVersion,
            string? clientName = null,
            string? serverName = null,
            TimeSpan? disconnectTimeout = null)
        {
            Clock = new ManualTimerFactory();
            ClientDriver = new ManualReconnectableDriver();
            ServerDriver = new ManualReconnectableDriver();
            InnerServer = new FakeReconnectableServer();
            _clientVersion = clientVersion;

            _server = new RawReliableReconnectableServer(
                InnerServer,
                disconnectTimeout ?? TimeSpan.FromSeconds(30),
                InnerServer.Log,
                InnerServer.Memory,
                serverVersion,
                serverName,
                ReconnectableInfo.DefaultHandshakeMaxByteSize,
                Clock,
                () => ServerDriver);

            Factory = new ReconnectableServerFactory(_ =>
            {
                var handler = new RecordingReconnectableServerHandler();
                ServerHandlers.Add(handler);
                return handler;
            });

            if (!_server.Init(Factory))
            {
                throw new InvalidOperationException("Failed to initialize the reconnectable server.");
            }

            ClientHandler = new RecordingReconnectableClientHandler();
            _client = new RawReliableReconnectableClient(
                CreateInnerClient,
                disconnectTimeout ?? TimeSpan.FromSeconds(30),
                InnerServer.Log,
                InnerServer.Memory,
                _clientVersion,
                clientName,
                ReconnectableInfo.DefaultHandshakeMaxByteSize,
                Clock,
                _ => ClientDriver,
                () => Clock.Now);

            if (!_client.Init(ClientHandler))
            {
                throw new InvalidOperationException("Failed to initialize the reconnectable client.");
            }
        }

        public ManualTimerFactory Clock { get; }

        public ManualReconnectableDriver ClientDriver { get; }

        public ManualReconnectableDriver ServerDriver { get; }

        public FakeReconnectableServer InnerServer { get; }

        public ReconnectableServerFactory Factory { get; }

        public RecordingReconnectableClientHandler ClientHandler { get; }

        public List<RecordingReconnectableServerHandler> ServerHandlers { get; } =
            new List<RecordingReconnectableServerHandler>();

        public List<FakeReconnectableClient> ClientAttempts { get; } =
            new List<FakeReconnectableClient>();

        public RecordingReconnectableServerHandler? ServerHandler =>
            ServerHandlers.Count == 0 ? null : ServerHandlers[ServerHandlers.Count - 1];

        public FakeReconnectableClient CurrentAttempt =>
            ClientAttempts.Count == 0 ? throw new InvalidOperationException("No client attempt") :
            ClientAttempts[ClientAttempts.Count - 1];

        public FakeReconnectableServerSession? ServerSession { get; private set; }

        public RawReliableReconnectableClient Client => _client;

        public void Start()
        {
            if (!_server.Start(_ => { }))
            {
                throw new InvalidOperationException("Failed to start the reconnectable server.");
            }

            if (!_client.Start(_ => { }))
            {
                throw new InvalidOperationException("Failed to start the reconnectable client.");
            }

            EstablishPhysicalConnection();
        }

        /// <summary>
        /// Starts a fresh physical connection, links both endpoints, and lets the negotiation run.
        /// </summary>
        public void EstablishPhysicalConnection(Action<FakeReconnectableEndpoint>? configureEndpoint = null)
        {
            ClientDriver.Tick();

            var attempt = CurrentAttempt;
            var session = InnerServer.Connect();
            ServerSession = session;
            _sessions.Add(session);

            attempt.ConfigureEndpoint = endpoint =>
            {
                endpoint.Deliver = data => session.DeliverToServer(data);
                configureEndpoint?.Invoke(endpoint);
            };
            session.Endpoint.Deliver = data => attempt.DeliverToClient(data);
            attempt.Connect();

            TickAll();
        }

        /// <summary>
        /// Drops the current physical connection on both sides without stopping either logical transport.
        /// </summary>
        public void DropPhysicalConnection(StopReason reason)
        {
            ServerSession?.Stop(reason);
            CurrentAttempt.DropFromRemote(reason);
            TickAll();
        }

        /// <summary>
        /// Advances past the reconnect period, starts a new physical connection, and lets the client
        /// reattach with its session id and secret.
        /// </summary>
        public void Reconnect(Action<FakeReconnectableEndpoint>? configureEndpoint = null)
        {
            Clock.Advance(ReconnectableInfo.ReconnectionPeriod);
            EstablishPhysicalConnection(configureEndpoint);
        }

        /// <summary>
        /// Runs both periodic drivers: the client drains its send queue, the server drains its receive queue.
        /// </summary>
        public void TickAll()
        {
            // Two rounds let a payload queued by one side reach the other side's driver and then become
            // visible to its application handler. The production driver runs continuously; the manual one
            // needs explicit rounds.
            for (int i = 0; i < 2; i++)
            {
                ClientDriver.Tick();
                ServerDriver.Tick();
            }
        }

        public void Dispose()
        {
            try
            {
                _client.Stop();
            }
            finally
            {
                try
                {
                    _server.Stop();
                }
                finally
                {
                    foreach (var session in _sessions)
                    {
                        session.Release();
                    }

                    foreach (var attempt in ClientAttempts)
                    {
                        attempt.Endpoint?.ReleaseSent();
                    }
                }
            }
        }

        private IRawReliableClient CreateInnerClient()
        {
            var client = new FakeReconnectableClient();
            ClientAttempts.Add(client);
            return client;
        }
    }
}
