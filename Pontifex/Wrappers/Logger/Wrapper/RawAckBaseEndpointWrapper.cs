using System;
using System.Collections.Generic;
using Pontifex.Endpoints;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public abstract class RawAckBaseEndpointWrapper : IRawReliableEndpoint
    {
        private volatile IRawReliableEndpoint? _core;
        private readonly Func<IRawReliableEndpoint?, UnionDataList, SendResult> _sender;
        private readonly Func<IRawReliableEndpoint?, StopReason, bool> _disconnector;

        protected RawAckBaseEndpointWrapper(IRawReliableEndpoint? core, 
            Func<IRawReliableEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableEndpoint?, StopReason, bool> disconnector)
        {
            _core = core;
            _sender = sender;
            _disconnector = disconnector;
        }

        // public void SetCore(IRawAckBaseEndpoint core)
        // {
        //     _core = core;
        // }

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

        public virtual void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            _core?.GetControls(dst, predicate);
        }
    }
}