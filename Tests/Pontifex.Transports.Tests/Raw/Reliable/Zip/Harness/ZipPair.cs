using System;
using System.Collections.Generic;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.Raw.Reliable.Zip;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Zip.Harness
{
    /// <summary>
    /// Records the observable lifecycle and payloads of an application client handler.
    /// </summary>
    internal sealed class RecordingZipClientHandler : IRawReliableClientHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public List<string[]> Received { get; } = new List<string[]>();

        public List<string> Events { get; } = new List<string>();

        public StopReason? StopReason { get; private set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                Received.Add(ZipPayload.Describe(receivedBuffer));
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// Records the observable lifecycle and payloads of an application server session handler.
    /// </summary>
    internal sealed class RecordingZipServerHandler : IRawReliableServerHandler
    {
        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public List<string[]> Received { get; } = new List<string[]>();

        public List<string> Events { get; } = new List<string>();

        public StopReason? StopReason { get; private set; }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            ConnectedCount++;
            Events.Add("connected");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                Received.Add(ZipPayload.Describe(receivedBuffer));
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            Events.Add("disconnected");
        }

        public void OnStopped(StopReason reason)
        {
            StopReason = reason;
            Events.Add("stopped");
        }
    }

    /// <summary>
    /// A recording server handler factory. Each admitted session receives a fresh handler.
    /// </summary>
    internal sealed class ZipServerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;

        public ZipServerFactory(Func<IEndPoint, IRawReliableServerHandler?> create)
        {
            _create = create;
        }

        public int Calls { get; private set; }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            Calls++;
            return _create(remote);
        }
    }

    /// <summary>
    /// A fully linked zip wrapper client/server pair over the synchronous fake transports.
    /// </summary>
    /// <remarks>
    /// The wrapper server receives a logic factory, exactly like the production zip binding: the zip logic
    /// releases its compressor/decompressor when a session disconnects, so every accepted session must get
    /// its own instance. <see cref="ConnectClient"/> connects another client to the same running server.
    /// </remarks>
    internal sealed class ZipPair : IDisposable
    {
        private readonly RawReliableWrapperServer _server;
        private readonly int _compressionLevel;
        private readonly string _clientName;
        private readonly short _clientVersion;
        private readonly List<ZipClientLink> _links = new List<ZipClientLink>();
        private readonly List<FakeZipServerSession> _sessions = new List<FakeZipServerSession>();

        public ZipPair(
            string clientName = ZipInfo.TransportName,
            string serverName = ZipInfo.TransportName,
            short clientVersion = 1,
            short serverVersion = 1,
            int compressionLevel = 9)
        {
            Clock = new ManualTimerFactory();
            InnerServer = new FakeZipServer();
            _compressionLevel = compressionLevel;
            _clientName = clientName;
            _clientVersion = clientVersion;

            _server = new RawReliableWrapperServer("server", serverName, InnerServer,
                () => new RawReliableZipServerLogic(InnerServer.Log, InnerServer.Memory, compressionLevel),
                serverVersion, 128, null, Clock);
            Factory = new ZipServerFactory(_ =>
            {
                var handler = new RecordingZipServerHandler();
                ServerHandlers.Add(handler);
                return handler;
            });

            if (!_server.Init(Factory))
            {
                throw new InvalidOperationException("Failed to initialize the zip wrapper server.");
            }

            FirstClient = CreateClient();
            _links.Add(FirstClient);
        }

        public ManualTimerFactory Clock { get; }

        public RecordingZipClientHandler ClientHandler => FirstClient.Handler;

        public ZipClientLink FirstClient { get; }

        public FakeZipClient InnerClient => FirstClient.InnerClient;

        public List<RecordingZipServerHandler> ServerHandlers { get; } = new List<RecordingZipServerHandler>();

        public RecordingZipServerHandler? ServerHandler =>
            ServerHandlers.Count == 0 ? null : ServerHandlers[ServerHandlers.Count - 1];

        public FakeZipServer InnerServer { get; }

        public ZipServerFactory Factory { get; }

        public FakeZipServerSession? ServerSession { get; private set; }

        public void Start()
        {
            if (!_server.Start(_ => { }))
            {
                throw new InvalidOperationException("Failed to start the zip wrapper server.");
            }

            FirstClient.Connect();
        }

        /// <summary>
        /// Connects one more client to the same running server and returns its link.
        /// </summary>
        public ZipClientLink ConnectClient()
        {
            var link = CreateClient();
            _links.Add(link);
            link.Connect();
            return link;
        }

        public void Dispose()
        {
            foreach (var link in _links)
            {
                link.Stop();
            }

            try
            {
                _server.Stop();
            }
            finally
            {
                foreach (var link in _links)
                {
                    link.InnerClient.Endpoint?.ReleaseSent();
                }

                foreach (var session in _sessions)
                {
                    session.Release();
                }
            }
        }

        private ZipClientLink CreateClient()
        {
            var innerClient = new FakeZipClient();
            var handler = new RecordingZipClientHandler();
            var client = new RawReliableWrapperClient("client", _clientName, innerClient,
                new RawReliableZipClientLogic(innerClient.Log, innerClient.Memory, _compressionLevel),
                _clientVersion, 128, null, Clock);

            if (!client.Init(handler))
            {
                throw new InvalidOperationException("Failed to initialize the zip wrapper client.");
            }

            return new ZipClientLink(this, client, innerClient, handler);
        }

        /// <summary>
        /// One client link: the inner fake transport, the wrapper client, and the application handler.
        /// </summary>
        internal sealed class ZipClientLink
        {
            private readonly ZipPair _pair;
            private readonly RawReliableWrapperClient _client;

            public ZipClientLink(ZipPair pair, RawReliableWrapperClient client, FakeZipClient innerClient,
                RecordingZipClientHandler handler)
            {
                _pair = pair;
                _client = client;
                InnerClient = innerClient;
                Handler = handler;
            }

            public FakeZipClient InnerClient { get; }

            public RecordingZipClientHandler Handler { get; }

            public FakeZipServerSession? Session { get; private set; }

            public void Connect()
            {
                var session = _pair.InnerServer.Connect();
                Session = session;
                _pair.ServerSession = session;
                _pair._sessions.Add(session);

                session.Endpoint.Deliver = data => InnerClient.DeliverToClient(data);

                if (!_client.Start(_ => { }))
                {
                    throw new InvalidOperationException("Failed to start the zip wrapper client.");
                }

                InnerClient.ConfigureEndpoint = endpoint =>
                    endpoint.Deliver = data => session.DeliverToServer(data);
                InnerClient.Connect();
            }

            public void Stop()
            {
                _client.Stop();
            }
        }
    }
}
