using System.Threading;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public abstract class RawReliableEndpoint : RawEndpoint, IRawReliableEndpoint
    {
        private readonly IRawReliableHandler _handler;

        private int _isConnected;

        protected enum MessageType : byte
        {
            InvalidMessage = 0,
            PayloadMessage = 1,
            DisconnectMessage = 2,
            PingMessage = 3,
            PongMessage = 4,
            AckRequestMessage = 5,
            AckResponseMessage = 6
        }

        public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

        protected RawReliableEndpoint(
            IRawReliableTransport owner, IEndPoint remoteEndPoint, IRawReliableHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes)
        {
            _handler = handler;
        }

        protected abstract void OnConnected();
        protected abstract void OnDisconnected();

        protected bool MarkConnected()
        {
            if (Interlocked.CompareExchange(ref _isConnected, 1, 0) == 0)
            {
                OnConnected();
                return true;
            }

            return false;
        }

        protected sealed override void OnStopped(StopReason reason)
        {
            if (Interlocked.CompareExchange(ref _isConnected, 2, 1) == 1)
            {
                lock (_handlerLock)
                {
                    _handler.OnDisconnected(reason);
                }
                OnDisconnected();
            }

            base.OnStopped(reason);
        }

        bool IRawReliableEndpoint.Disconnect(StopReason reason)
        {
            return StopGracefully(reason);
        }

        public override SendResult Send(UnionDataList bufferToSend)
        {
            if (bufferToSend != null! && bufferToSend.IsAlive)
            {
                bufferToSend.PutFirst((byte)MessageType.PayloadMessage);
                return ScheduleToSend(bufferToSend);
            }

            return SendResult.InvalidMessage;
        }

        protected void DisconnectGracefully()
        {
            var buffer = Memory.SmallObjectsPool.Acquire<UnionDataList>().resource;
            buffer.PutFirst((byte)MessageType.DisconnectMessage);
            var res = ScheduleToSend(buffer);
            if (res == SendResult.Ok)
            {
                StopGracefully(new UserIntention(TransportName));
            }
            else
            {
                StopNow(new UserIntention(TransportName));
            }
        }

        protected virtual bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.PayloadMessage:
                    base.ProcessInboundMessage(message);
                    return true;
                case MessageType.DisconnectMessage:
                    StopNow(new GracefulRemoteIntention(TransportName));
                    return true;
                default:
                    return false;
            }
        }

        protected override void ProcessInboundMessage(UnionDataList message)
        {
            if (message.TryPopFirst(out byte msgType))
            {
                if (ProcessInboundMessage((MessageType)msgType, message))
                {
                    return; // ok
                }
            }
            else
            {
                message.Release();
            }
            StopGracefully(new TextFail(TransportName, "Failed to parse incoming message"));
        }
    }
}