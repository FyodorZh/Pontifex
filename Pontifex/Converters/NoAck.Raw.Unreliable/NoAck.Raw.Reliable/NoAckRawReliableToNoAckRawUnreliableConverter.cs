using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.NoAck.Raw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Converters
{
    public class NoAckRawReliableToNoAckRawUnreliableConverter : ITransportConverter
    {
        public TransportType From => TransportType.NoAckRawReliable;
        public TransportType To => TransportType.NoAckRawUnreliable;

        public Func<ITransport> Convert(Func<ITransport> innerTransportCtor, IMemoryRental? memoryOverride = null, ILogger? loggerOverride = null)
        {
            return () =>
            {
                var transport = innerTransportCtor();
                if (transport is INoAckRawReliableClient client)
                    return new UnreliableClientWrapper(client, memoryOverride, loggerOverride);
                if (transport is INoAckRawReliableServer server)
                    return new UnreliableServerWrapper(server, memoryOverride, loggerOverride);

                throw new ArgumentException($"Transport must implement {nameof(INoAckRawReliableClient)} or {nameof(INoAckRawReliableServer)}", nameof(transport));
            };
        }

        private sealed class UnreliableClientWrapper : INoAckRawUnreliableClient
        {
            private readonly INoAckRawReliableClient _inner;
            private readonly ILogger _log;
            private readonly IMemoryRental _memory;
            
            public TransportType Type => TransportType.NoAckRawUnreliable;

            public UnreliableClientWrapper(INoAckRawReliableClient inner, IMemoryRental? memoryOverride, ILogger? loggerOverride)
            {
                _inner = inner;
                _log = loggerOverride ?? inner.Log;
                _memory = memoryOverride ?? inner.Memory;
            }

            public event Action<UnionDataList>? OnReceived
            {
                add => _inner.OnReceived += value;
                remove => _inner.OnReceived -= value;
            }

            public int MessageMaxByteSize => _inner.MessageMaxByteSize;

            public string Name => _inner.Name;

            public bool IsValid => _inner.IsValid;
            public bool IsStarted => _inner.IsStarted;
            public ILogger Log => _log;
            public IMemoryRental Memory => _memory;
            
            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }

            public bool Start(Action<StopReason> onStopped) => _inner.Start(onStopped);
            public bool Stop(StopReason? reason = null) => _inner.Stop(reason);

            public SendResult TrySend(UnionDataList message)
            {
                return _inner.Send(message);
            }
        }

        private sealed class UnreliableServerWrapper : INoAckRawUnreliableServer
        {
            private readonly INoAckRawReliableServer _inner;
            private readonly ILogger _log;
            private readonly IMemoryRental _memory;
            
            public TransportType Type => TransportType.NoAckRawUnreliable;

            public UnreliableServerWrapper(INoAckRawReliableServer inner, IMemoryRental? memoryOverride, ILogger? loggerOverride)
            {
                _inner = inner;
                _log = loggerOverride ?? inner.Log;
                _memory = memoryOverride ?? inner.Memory;
            }

            public event Action<IEndPoint, UnionDataList>? OnReceived
            {
                add => _inner.OnReceived += value;
                remove => _inner.OnReceived -= value;
            }

            public int MessageMaxByteSize => _inner.MessageMaxByteSize;

            public string Name => _inner.Name;

            public bool IsValid => _inner.IsValid;
            public bool IsStarted => _inner.IsStarted;
            public ILogger Log => _log;
            public IMemoryRental Memory => _memory;
            
            public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
            {
            }

            public bool Start(Action<StopReason> onStopped) => _inner.Start(onStopped);
            public bool Stop(StopReason? reason = null) => _inner.Stop(reason);

            public SendResult TrySend(IEndPoint destination, UnionDataList message)
            {
                return _inner.Send(destination, message);
            }
        }
    }
}
