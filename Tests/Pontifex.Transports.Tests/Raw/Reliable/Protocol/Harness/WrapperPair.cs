using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Protocol.Harness
{
    /// <summary>
    /// A fully linked wrapper client/server pair over the synchronous fake transports.
    /// </summary>
    internal sealed class WrapperPair : IDisposable
    {
        public WrapperPair(
            string clientName = "layer",
            string serverName = "layer",
            short clientVersion = 1,
            short serverVersion = 1,
            int clientMaxByteSize = 128,
            int serverMaxByteSize = 128,
            RecordingWrapperLogic? clientLogic = null,
            RecordingWrapperLogic? serverLogic = null)
        {
            Clock = new ManualTimerFactory();
            var log = new Logger([]);

            ClientLogic = clientLogic ?? new RecordingWrapperLogic(log, MemoryRental.Shared);
            ServerLogic = serverLogic ?? new RecordingWrapperLogic(log, MemoryRental.Shared);

            InnerClient = new FakeReliableClient();
            InnerServer = new FakeReliableServer();

            Server = new RawReliableWrapperServer("server", serverName, InnerServer, ServerLogic,
                serverVersion, serverMaxByteSize, null, Clock);
            Factory = new RecordingServerFactory(_ =>
            {
                var handler = new RecordingRawReliableServerHandler();
                ServerHandler = handler;
                return handler;
            });

            if (!Server.Init(Factory))
            {
                throw new InvalidOperationException("Failed to initialize the wrapper server.");
            }

            Client = new RawReliableWrapperClient("client", clientName, InnerClient, ClientLogic,
                clientVersion, clientMaxByteSize, null, Clock);
            ClientHandler = new RecordingRawReliableClientHandler();
            if (!Client.Init(ClientHandler))
            {
                throw new InvalidOperationException("Failed to initialize the wrapper client.");
            }
        }

        public ManualTimerFactory Clock { get; }

        public RecordingWrapperLogic ClientLogic { get; }

        public RecordingWrapperLogic ServerLogic { get; }

        public RecordingRawReliableClientHandler ClientHandler { get; }

        public RecordingRawReliableServerHandler? ServerHandler { get; private set; }

        public FakeReliableClient InnerClient { get; }

        public FakeReliableServer InnerServer { get; }

        public RawReliableWrapperClient Client { get; }

        public RawReliableWrapperServer Server { get; }

        public RecordingServerFactory Factory { get; }

        public FakeServerSession? ServerSession { get; private set; }

        public StopReason? ClientTransportStopReason { get; private set; }

        public StopReason? ServerTransportStopReason { get; private set; }

        public void Start()
        {
            if (!Server.Start(reason => ServerTransportStopReason = reason))
            {
                throw new InvalidOperationException("Failed to start the wrapper server.");
            }

            ServerSession = InnerServer.Connect();
            ServerSession.Endpoint.Deliver = data => InnerClient.DeliverToClient(data);

            if (!Client.Start(reason => ClientTransportStopReason = reason))
            {
                throw new InvalidOperationException("Failed to start the wrapper client.");
            }

            InnerClient.ConfigureEndpoint = endpoint =>
                endpoint.Deliver = data => ServerSession.DeliverToServer(data);

            InnerClient.Connect();
        }

        public void Dispose()
        {
            try
            {
                Client.Stop();
            }
            finally
            {
                try
                {
                    Server.Stop();
                }
                finally
                {
                    InnerClient.Endpoint?.ReleaseSent();
                    ServerSession?.Endpoint.ReleaseSent();
                    ReleaseTail(ClientLogic.ResponseTail);
                    ClientLogic.ResponseTail = null;
                    ReleaseTail(ServerLogic.ResponseTail);
                    ServerLogic.ResponseTail = null;
                }
            }
        }

        private static void ReleaseTail(UnionDataList? tail)
        {
            tail?.Release();
        }
    }

    /// <summary>
    /// A recording server handler factory. Each accepted session receives a fresh handler.
    /// </summary>
    internal sealed class RecordingServerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;

        public RecordingServerFactory(Func<IEndPoint, IRawReliableServerHandler?> create)
        {
            _create = create;
        }

        public int Calls { get; private set; }

        public List<IEndPoint> Remotes { get; } = new List<IEndPoint>();

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            Calls++;
            Remotes.Add(remote);
            return _create(remote);
        }
    }
}
