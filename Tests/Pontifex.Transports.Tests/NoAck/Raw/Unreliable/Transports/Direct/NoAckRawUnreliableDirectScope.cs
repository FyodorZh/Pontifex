using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.NoAck.Raw;
using Pontifex.NoAck.Raw.Unreliable.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable.Tests
{
    public class NoAckRawUnreliableDirectScope : INoAckRawUnreliableConformanceScope
    {
        private readonly string _serverName;
        private readonly IMemoryRental _memory;
        private readonly ILogger _logger;
        private readonly List<ITransport> _transports = new();
        private NoAckRawUnreliableDirectServer? _backgroundServer;

        public NoAckRawUnreliableDirectScope()
        {
            _memory = MemoryRental.Shared;
            _logger = new Logger();
            _logger.LogFor = Severity.FATAL;
            var id = Guid.NewGuid().ToString("N");
            _serverName = "direct-conformance-test-" + id;
        }

        public INoAckRawUnreliableClient CreateClient()
        {
            EnsureBackgroundServer();
            var client = new NoAckRawUnreliableDirectClient(
                _serverName, _logger, _memory);
            _transports.Add(client);
            return client;
        }

        public INoAckRawUnreliableServer CreateServer()
        {
            var serverName = "direct-srv-" + Guid.NewGuid().ToString("N");
            var server = new NoAckRawUnreliableDirectServer(
                serverName, _logger, _memory);
            _transports.Add(server);
            return server;
        }

        private void EnsureBackgroundServer()
        {
            if (_backgroundServer != null)
                return;
            _backgroundServer = new NoAckRawUnreliableDirectServer(
                _serverName, _logger, _memory);
            _backgroundServer.Start(_ => { });
        }

        public UnionDataList CreateSmallValidMessage(ITransport transport)
        {
            var message = AcquireMessage(transport);
            message.PutFirst((byte)42);
            return message;
        }

        public UnionDataList CreateExactLimitMessage(ITransport transport)
        {
            var maxSize = GetMessageMaxByteSize(transport);
            var message = AcquireMessage(transport);
            var arraySize = SolverArraySize(maxSize - 2);
            message.PutFirst(new MultiRefByteArray(new byte[arraySize]));
            return message;
        }

        public UnionDataList CreateOneByteOverLimitMessage(ITransport transport)
        {
            var maxSize = GetMessageMaxByteSize(transport);
            var message = AcquireMessage(transport);
            message.PutFirst(new StaticReadOnlyByteArray(new byte[maxSize + 10]));
            return message;
        }

        public IEndPoint CreateForeignServerDestination()
        {
            return new StringEndPoint("direct-foreign-test-server");
        }

        public IEnumerable<INoAckRawUnreliableAdditionalNonOkCase> CreateAdditionalNonOkCases()
        {
            return Enumerable.Empty<INoAckRawUnreliableAdditionalNonOkCase>();
        }

        public void Dispose()
        {
            foreach (var transport in _transports)
            {
                try
                {
                    if (transport.IsValid && transport.IsStarted)
                        transport.Stop();
                }
                catch { }
            }
            _transports.Clear();

            if (_backgroundServer != null)
            {
                try
                {
                    if (_backgroundServer.IsValid && _backgroundServer.IsStarted)
                        _backgroundServer.Stop();
                }
                catch { }
                _backgroundServer = null;
            }
        }

        private static UnionDataList AcquireMessage(ITransport transport)
        {
            return transport.Memory.CollectablePool.Acquire<UnionDataList>();
        }

        private static int GetMessageMaxByteSize(ITransport transport)
        {
            if (transport is INoAckRawUnreliableClient client)
                return client.MessageMaxByteSize;
            if (transport is INoAckRawUnreliableServer server)
                return server.MessageMaxByteSize;
            throw new ArgumentException("Unexpected transport type");
        }

        private static int SolverArraySize(int target)
        {
            for (int varintSize = 1; varintSize <= 4; varintSize++)
            {
                var arraySize = target - varintSize;
                if (arraySize >= 0 && ZigZagVarIntSerializer.GetIntEncodedSize(arraySize) == varintSize)
                    return arraySize;
            }
            throw new InvalidOperationException($"Cannot solve array size for target {target}");
        }
    }
}
