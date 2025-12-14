using System;
using Pontifex.Endpoints;
using Pontifex.Utils;

namespace Pontifex.Abstractions.Endpoints
{
    public abstract class AckRawBaseEndpointWrapper : IAckRawBaseEndpoint
    {
        private volatile IAckRawBaseEndpoint? _core;
        private readonly Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> _sender;
        private readonly Func<IAckRawBaseEndpoint?, StopReason, bool> _disconnector;

        protected AckRawBaseEndpointWrapper(IAckRawBaseEndpoint? core, 
            Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawBaseEndpoint?, StopReason, bool> disconnector)
        {
            _core = core;
            _sender = sender;
            _disconnector = disconnector;
        }

        public void SetCore(IAckRawBaseEndpoint core)
        {
            _core = core;
        }

        public IEndPoint RemoteEndPoint => _core?.RemoteEndPoint ?? VoidEndPoint.Instance;

        public bool IsConnected => _core?.IsConnected ?? false;

        public int MessageMaxByteSize => _core?.MessageMaxByteSize ?? 0;

        public SendResult Send(UnionDataList bufferToSend)
        {
            return _sender(_core, bufferToSend);
        }

        public bool Disconnect(StopReason reason)
        {
            return _disconnector.Invoke(_core, reason);
        }
    }
}