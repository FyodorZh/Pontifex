using Actuarius.Collections;
using Shared;
using Shared.Buffer;
using Shared.Concurrent;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Endpoints.Server;
using Transport.Endpoints;
using Transport.StopReasons;

namespace Transport.Transports.Direct
{
    internal class TransportEndPoint : IAckRawServerEndpoint, IAckRawClientEndpoint
    {
        private struct SendReceiveAction: ActionQueue<SendReceiveAction>.IAction
        {
            private readonly IAnyDirectCtl mCtl;
            private IMemoryBufferHolder mBuffer;

            public SendReceiveAction(IMemoryBufferHolder buffer, IAnyDirectCtl ctl)
            {
                mCtl = ctl;
                mBuffer = buffer;
            }

            public void Invoke()
            {
                mCtl.OnReceived(mBuffer);
            }

            public void Fail()
            {
                mBuffer.Release();
                mBuffer = null;
            }
        }

        private readonly DirectTransport mOwner;
        private readonly IAnyDirectCtl mCtl;

        private readonly ActionQueue<SendReceiveAction> mActionSerializer;

        private volatile TransportEndPoint mOther;

        public IEndPoint LocalEndPoint { get; private set; }

        public TransportEndPoint(DirectTransport owner, IEndPoint localEp, IAnyDirectCtl ctl)
        {
            mOwner = owner;
            mCtl = ctl;
            LocalEndPoint = localEp;
            mActionSerializer = new ActionQueue<SendReceiveAction>(new LimitedConcurrentQueue<SendReceiveAction>(DirectInfo.BufferCapacity));
        }

        public void SetOther(TransportEndPoint other)
        {
            mOther = other;
        }

        private SendResult Receive(IMemoryBufferHolder buffer)
        {
            if (!mActionSerializer.Put(new SendReceiveAction(buffer, mCtl)))
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
            mOther = null;
            mCtl.OnDisconnected(reason);
        }

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get
            {
                var other = mOther;
                if (other != null)
                {
                    return other.LocalEndPoint;
                }
                return VoidEndPoint.Instance;
            }
        }

        bool IAckRawBaseEndpoint.IsConnected
        {
            get { return mOther != null; }
        }

        int IAckRawBaseEndpoint.MessageMaxByteSize
        {
            get
            {
                return DirectInfo.MessageMaxByteSize;
            }
        }

        public SendResult Send(IMemoryBufferHolder bufferToSend)
        {
            var other = mOther;
            if (other != null)
            {
                return other.Receive(bufferToSend);
            }
            bufferToSend.Release();
            return SendResult.NotConnected;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            mActionSerializer.Release();
            return mOwner.Disconnect(reason);
        }
    }

}