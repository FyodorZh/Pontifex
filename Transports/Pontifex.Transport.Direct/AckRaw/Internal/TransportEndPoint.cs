using Actuarius.Collections;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Direct
{
    internal class TransportEndPoint : IAckRawServerEndpoint, IAckRawClientEndpoint
    {
        private struct SendReceiveAction: ActionQueue<SendReceiveAction>.IAction
        {
            private readonly IAnyDirectCtl _ctl;
            private UnionDataList _buffer;

            public SendReceiveAction(UnionDataList buffer, IAnyDirectCtl ctl)
            {
                _ctl = ctl;
                _buffer = buffer;
            }

            public void Invoke()
            {
                _ctl.OnReceived(_buffer);
            }

            public void Fail()
            {
                _buffer.Release();
                _buffer = null!;
            }
        }

        private readonly DirectTransport _owner;
        private readonly IAnyDirectCtl _ctl;

        private readonly ActionQueue<SendReceiveAction> _actionSerializer;

        private volatile TransportEndPoint? _other;

        public IEndPoint LocalEndPoint { get; private set; }

        public TransportEndPoint(DirectTransport owner, IEndPoint localEp, IAnyDirectCtl ctl)
        {
            _owner = owner;
            _ctl = ctl;
            LocalEndPoint = localEp;
            _actionSerializer = new ActionQueue<SendReceiveAction>(new LimitedConcurrentQueue<SendReceiveAction>(DirectInfo.BufferCapacity));
        }

        public void SetOther(TransportEndPoint other)
        {
            _other = other;
        }

        private SendResult Receive(UnionDataList buffer)
        {
            if (!_actionSerializer.Put(new SendReceiveAction(buffer, _ctl)))
            {
                buffer.Release();
                Log.e("Direct transport buffer overflow");
                Disconnect(new TextFail("direct", "Buffer overflow"));
                return SendResult.BufferOverflow;
            }

            return SendResult.Ok;
        }

        public void Disconnect(StopReason reason)
        {
            _other = null;
            _ctl.OnDisconnected(reason);
        }

        IEndPoint? IAckRawBaseEndpoint.RemoteEndPoint => _other?.LocalEndPoint;

        bool IAckRawBaseEndpoint.IsConnected => _other != null;

        int IAckRawBaseEndpoint.MessageMaxByteSize => DirectInfo.MessageMaxByteSize;

        public SendResult Send(UnionDataList bufferToSend)
        {
            var other = _other;
            if (other != null)
            {
                return other.Receive(bufferToSend);
            }
            bufferToSend.Release();
            return SendResult.NotConnected;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            _actionSerializer.Release();
            return _owner.Disconnect(reason);
        }
    }

}