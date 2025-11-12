using System;
using Shared;
using Shared.Buffer;
using Shared.FSM;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using Transport.StopReasons;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.AckRaw
{
    internal class ReliableServerProtocol: ReliableProtocol, IAckRawClientEndpoint
    {
        private class RemoteEp : IRemoteEndPoint
        {
            private readonly INoAckUnreliableRawClientEndpoint mRemoteEp;
            private readonly IEndPoint mClientAddress;

            public RemoteEp(INoAckUnreliableRawClientEndpoint remoteEp, IEndPoint clientAddress)
            {
                mRemoteEp = remoteEp;
                mClientAddress = clientAddress;
            }

            public IEndPoint RemoteEndPoint
            {
                get { return mClientAddress; }
            }

            public int MessageMaxByteSize
            {
                get { return mRemoteEp.MessageMaxByteSize; }
            }

            public SendResult Send(IMacroOwner<Message> messages)
            {
                return mRemoteEp.Send(mClientAddress, messages);
            }
        }

        private readonly Func<ByteArraySegment, IAckRawServerHandler> mHandlerProducer;

        private IAckRawServerHandler mUserHandler;

        private enum ProtocolState
        {
            Constructed, // протокол создан, ничего не известно про клиент
            Connected,   // клиент опознан и считается подключённым
            Stopped      // клиент отключён
        }

        private readonly IConcurrentFSM<ProtocolState> mState;

        public bool IsAcknowledged
        {
            get { return mUserHandler != null; }
        }

        public ReliableServerProtocol(
            INoAckUnreliableRawClientEndpoint serverEp,
            IEndPoint clientAddress,
            TimeSpan disconnectTimeout,
            Message firstMessage,
            Func<ByteArraySegment, IAckRawServerHandler> userHandlerProducer,
            IDateTimeProvider timeProvider,
            ILogger logger,
            IDeliveryControllerSink deliveryController)
            : base(new RemoteEp(serverEp, clientAddress), disconnectTimeout, timeProvider, logger, deliveryController)
        {
            var fsm = new FSM<ProtocolState, int>(ProtocolState.Constructed, state => (int)state);
            fsm.AddTransition(ProtocolState.Constructed, ProtocolState.Connected);
            fsm.AddTransitions(new[]{ProtocolState.Constructed, ProtocolState.Connected}, ProtocolState.Stopped);

            mState = new ConcurrentFSM<ProtocolState>(fsm);

            mHandlerProducer = userHandlerProducer;
            ReceiveUnsafe(firstMessage);
            if (mUserHandler != null)
            {
                mState.SetState(ProtocolState.Connected);
            }
            else
            {
                mState.SetState(ProtocolState.Stopped);
            }
        }

        protected override string ProtocolName
        {
            get { return ReliableInfo.TransportName; }
        }

        protected override bool OnLogicStarted()
        {
            if (mState.State == ProtocolState.Connected)
            {
                Log.i("Opening new session: {0}", "OK");

                try
                {
                    using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                    {
                        bufferAccessor.Buffer.PushArray(new ByteArraySegment(mUserHandler.GetAckResponse()));
                        bufferAccessor.Buffer.PushArray(new ByteArraySegment(ReliableInfo.AckOKResponse));
                        if (Send(bufferAccessor.Acquire()) != SendResult.Ok)
                        {
                            mState.SetState(ProtocolState.Stopped);
                            Fail("Failed to send ACK-OK");
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    mState.SetState(ProtocolState.Stopped);
                    Fail(new ExceptionFail(ProtocolName, e));
                    return false;
                }

                mUserHandler.OnConnected(this);
                return true;
            }

            return false;
        }

        protected override void OnTick()
        {
            // DO NOTHING
        }

        protected override void OnReceived(IMemoryBufferHolder buffer)
        {
            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                switch (mState.State)
                {
                    case ProtocolState.Constructed: // Срабатыват из ReceiveUnsafe() из конструктора
                        ByteArraySegment protocolAck;
                        ByteArraySegment userAck;
                        if (bufferAccessor.Buffer.PopFirst().AsArray(out protocolAck) && bufferAccessor.Buffer.PopFirst().AsArray(out userAck) &&
                            AckUtils.CheckPrefix(protocolAck, ReliableInfo.AckPrefix).IsValid)
                        {
                            try
                            {
                                mUserHandler = mHandlerProducer(userAck);
                            }
                            catch (Exception e)
                            {
                                Log.wtf(e);
                                Fail("User ack handler exception");
                            }
                        }
                        else
                        {
                            Fail("Failed to parse ack request");
                        }

                        break;
                    case ProtocolState.Connected:
                        try
                        {
                            mUserHandler.OnReceived(bufferAccessor.Acquire());
                        }
                        catch (Exception e)
                        {
                            Log.wtf(e);
                            Fail("User handler exception");
                        }

                        break;
                    case ProtocolState.Stopped:
                        // DO NOTHING
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected override void OnStopped(StopReason reason)
        {
            mState.SetState(ProtocolState.Stopped, (state, newState) =>
            {
                if (state == ProtocolState.Connected)
                {
                    var handler = mUserHandler;
                    if (handler != null)
                    {
                        handler.OnDisconnected(reason);
                    }
                }

                return true;
            });
        }
    }
}